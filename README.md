# Keycloak + .NET 9 Web API — Part 3

Source code for the blog series **"Identity Foundations: Keycloak + .NET 9 Web API from Scratch"**.

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

**Users seeded:**

| Username | Password | Role       | Orders visible      |
|----------|----------|------------|---------------------|
| `alice`  | `alice`  | api-reader | own orders (1–3)    |
| `bob`    | `bob`    | api-admin  | all orders (1–5)    |

## Run the API

```bash
dotnet run --project src/KeycloakDemo.Api
```

API listens on `http://localhost:5050`.

## Test

```bash
./get-token.sh
```

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
docker-compose.yml                    # Keycloak 26.1.0 on :9093
get-token.sh                          # curl end-to-end test script
keycloak/demo-realm.json              # Realm export — auto-imported on first boot
src/
  KeycloakDemo.Api/                   # Minimal API, endpoints, Program.cs
  KeycloakDemo.Application/           # Use cases, ICurrentUser, IOrderRepository
  KeycloakDemo.Domain/                # Order entity, DomainRole enum
  KeycloakDemo.Infrastructure/        # CurrentUserService, InMemoryOrderRepository
Tests/
  KeycloakDemo.Application.Tests/     # Use case tests — no ASP.NET Core dependency
```

## Series

| Part | Topic |
|------|-------|
| 1 | JWT Bearer auth, claim extraction |
| 2 | Role-based authorization via `IClaimsTransformation` |
| **3** | Clean Architecture — use cases, domain roles, testable business logic ← you are here |
| 4 | Machine-to-machine auth: client credentials & worker service |
| 5 | Token lifecycle: refresh, rotation, revocation, blocklist |
