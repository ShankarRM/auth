# Keycloak + .NET 9 Web API — Part 3

Source code for the blog series **"Identity Foundations: Keycloak + .NET 9 Web API from Scratch"**.

## What's new in Part 3

| Change | Detail |
|--------|--------|
| Clean Architecture scaffold | `src/` split into Domain / Application / Infrastructure / Api layers |
| Role-based order filtering | Readers see own orders (1–3); admins see all (1–5) |
| `GetOrdersUseCase` | Business logic isolated from HTTP — unit-testable without ASP.NET Core |
| `sub` null fix | `.NET 9 JsonWebTokenHandler` stores `sub` as `ClaimTypes.NameIdentifier`; `/me` checks both |
| JWT config stays inline | Extracted class triggered a named-options bug (`IConfigureOptions<T>` skipped for named schemes); reverted to `AddJwtBearer(options => { ... })` |

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- Docker + Docker Compose
- `jq` (`brew install jq`)

## Start Keycloak

```bash
docker compose up -d
```

Admin console: `http://localhost:9093` — credentials `admin` / `admin`

Realm, client (`demo-api`), and users are imported automatically from `keycloak/demo-realm.json` on first boot. No manual setup required.

> **Volume note:** If you change `demo-realm.json`, Keycloak won't re-import while the old volume exists.
> Run `docker compose down -v && docker compose up -d` to wipe and re-import, then restart the API.

**Users seeded:**

| Username | Password | Role       | Orders visible      |
|----------|----------|------------|---------------------|
| `alice`  | `alice`  | api-reader | own orders (1–3)    |
| `bob`    | `bob`    | api-admin  | all orders (1–5)    |

## Run the API

```bash
dotnet run
```

API listens on `http://localhost:5050`.

## Run tests

```bash
dotnet test
```

## End-to-end test

```bash
./get-token.sh
```

Runs 13 scenarios: token fetches for alice and bob, `/health`, `/me`, `/orders` (reader sees 3, admin sees 5), `/admin/users`, `/audit/logs`, and 401/403 cases.

## Endpoints

| Method | Path          | Auth          | Response | Description                              |
|--------|---------------|---------------|----------|------------------------------------------|
| GET    | /health       | None          | 200      | Liveness probe                           |
| GET    | /me           | Bearer        | 200      | Returns `sub`, `username`, `email`       |
| GET    | /orders       | Bearer        | 200      | Reader sees own; admin sees all          |
| GET    | /admin/users  | Bearer        | 200      | Requires `api-admin` role                |
| GET    | /audit/logs   | Bearer        | 200      | Requires `api-admin` + `audit.read` scope|
| GET    | /orders       | No token      | 401      | Missing `Authorization` header           |
| GET    | /orders       | Invalid token | 401      | Expired, tampered, or wrong audience     |
| GET    | /admin/users  | Reader token  | 403      | Insufficient role                        |
| GET    | /audit/logs   | No scope      | 403      | Admin token without `audit.read` scope   |

## Project structure

```
docker-compose.yml                          # Keycloak 26.1.0 on :9093
get-token.sh                                # 13-step curl end-to-end test script
keycloak/demo-realm.json                    # Realm export — auto-imported on first boot
src/                                        # Clean Architecture scaffold (Part 3 focus)
  KeycloakDemo.Api/                         # Minimal API, endpoints, Program.cs
  KeycloakDemo.Application/                 # Use cases, ICurrentUser, IOrderRepository
  KeycloakDemo.Domain/                      # Order entity, DomainRole enum
  KeycloakDemo.Infrastructure/              # CurrentUserService, InMemoryOrderRepository
Tests/
  KeycloakRoleClaimsTransformationTests.cs  # Claims transformation unit tests
  KeycloakDemo.Application.Tests/           # Use case tests — no ASP.NET Core dependency
```

## Series

| Part | Topic |
|------|-------|
| 1 | JWT Bearer auth, claim extraction |
| 2 | Role-based authorization via `IClaimsTransformation` |
| **3** | Clean Architecture — use cases, domain roles, testable business logic ← you are here |
| 4 | Machine-to-machine auth: client credentials & worker service |
| 5 | Token lifecycle: refresh, rotation, revocation, blocklist |
