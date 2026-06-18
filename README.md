# Keycloak + .NET 9 Web API — Part 4

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

Realm, clients, and users are imported automatically from `keycloak/demo-realm.json` on first boot. No manual setup required.

**Users seeded:**

| Username | Password | Role       | Orders visible   |
|----------|----------|------------|------------------|
| `alice`  | `alice`  | api-reader | own orders (1–3) |
| `bob`    | `bob`    | api-admin  | all orders (1–5) |

**Service accounts:**

| Client             | Grant type         | Role       |
|--------------------|--------------------|------------|
| `background-worker`| Client Credentials | api-reader |

## Keycloak setup (automated)

```bash
./keycloak-setup.sh
```

Creates the `background-worker` client, assigns the `api-reader` role to its service account, and configures multi-audience token support. Run once after Keycloak is up.

## Run the API

```bash
dotnet run --project src/KeycloakDemo.Api
```

API listens on `http://localhost:5050`.

## Run the Worker

```bash
dotnet run --project src/KeycloakDemo.Worker
```

Polls `GET /orders` every 30 seconds using the Client Credentials flow. Token is cached and auto-refreshed.

## Test

```bash
./get-token.sh
```

## Run integration tests

```bash
dotnet test Tests/KeycloakDemo.Api.Tests
```

No running Keycloak required — tests use `WebApplicationFactory` with self-signed JWTs.

## Endpoints

| Method | Path              | Auth                      | Response | Description                                        |
|--------|-------------------|---------------------------|----------|----------------------------------------------------|
| GET    | /health           | None                      | 200      | Liveness probe                                     |
| GET    | /me               | Bearer                    | 200      | Returns `sub`, `username`, `email`                 |
| GET    | /orders           | Bearer (`api-reader`)     | 200      | Reader sees own; admin sees all                    |
| GET    | /orders/service   | Bearer (`background-worker` azp + `api-reader`) | 200 | Service account only |
| GET    | /admin/users      | Bearer (`api-admin`)      | 200      | Requires `api-admin` role                          |
| GET    | /audit/logs       | Bearer (`api-admin`)      | 200      | Requires `api-admin` + `audit.read` scope          |
| GET    | /orders           | No token                  | 401      | Missing `Authorization` header                     |
| GET    | /orders/service   | Human user token          | 403      | `azp` is not `background-worker`                   |

## Project structure

```
docker-compose.yml                        # Keycloak 26.1.0 on :9093
get-token.sh                              # curl end-to-end test script
keycloak-setup.sh                         # Automated Keycloak client/role setup
keycloak/demo-realm.json                  # Realm export — auto-imported on first boot
TokenFlows.md                             # Visual reference for all OAuth flows used
src/
  KeycloakDemo.Api/                       # Minimal API, endpoints, Program.cs
  KeycloakDemo.Application/               # Use cases, ICurrentUser, IOrderRepository
  KeycloakDemo.Domain/                    # Order entity, DomainRole enum
  KeycloakDemo.Infrastructure/            # Auth, token service, HTTP handler, DI extensions
    Auth/
      AuthorizationPolicies.cs            # ReadAccess, AdminAccess, ServiceAccountOnly
      ConfigureJwtBearerOptions.cs        # JWT bearer setup (no options.Audience)
      KeycloakOptions.cs                  # Audience + AdditionalAudiences config
      KeycloakWorkerOptions.cs            # Worker client credentials config
      ServiceAccountCurrentUser.cs        # ICurrentUser for hosted services (no HttpContext)
      TokenValidationConfig.cs            # ValidAudiences list (multi-audience support)
    Http/
      AuthenticatedHttpClientHandler.cs   # DelegatingHandler — injects Bearer token
      KeycloakTokenService.cs             # Client Credentials token fetch + cache
  KeycloakDemo.Worker/                    # BackgroundService — polls /orders every 30s
Tests/
  KeycloakDemo.Api.Tests/                 # Integration tests — WebApplicationFactory + JwtTokenBuilder
  KeycloakDemo.Application.Tests/         # Use case unit tests
```

## What's new in Part 4

| | Part 3 | Part 4 |
|---|---|---|
| Auth flows | Human users only (ROPC) | Human + service account (Client Credentials) |
| `ICurrentUser` implementations | 1 (reads `HttpContext`) | 2 (reads `HttpContext` or config) |
| Audience validation | Single audience | `ValidAudiences` list, `AdditionalAudiences` in config |
| `/orders/service` | Not present | Restricted to `background-worker` `azp` + `api-reader` |
| Token lifecycle | Manual (`get-token.sh`) | Cached + auto-refresh in `KeycloakTokenService` |
| Tests | Use case unit tests | Integration tests with self-signed JWTs |

## Series

| Part | Topic |
|------|-------|
| 1 | JWT Bearer auth, claim extraction |
| 2 | Role-based authorization via `IClaimsTransformation` |
| 3 | Clean Architecture — use cases, domain roles, testable business logic |
| **4** | Machine-to-machine auth: client credentials, worker service, multi-audience, `azp` policy ← you are here |
| 5 | Token lifecycle: refresh, rotation, revocation, blocklist |
