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

**Two setup paths — pick one:**

**Path A — realm import (quick start):** `docker compose up -d` auto-imports `keycloak/demo-realm.json` on first boot. Realm, clients, users, and roles are ready immediately.

**Path B — Admin REST API (full Part 4 setup):**
```bash
docker compose up -d
KC_WORKER_SECRET=s3cr3t KC_API_SECRET=apisecret ./keycloak-setup.sh
```
Creates all clients (`dotnet-api`, `web-frontend`, `background-worker`), users, and roles via the Keycloak Admin API. Use this if you want to follow the provisioning steps in detail.

**Users seeded (both paths):**

| Username | Password | Role       | Orders visible   |
|----------|----------|------------|------------------|
| `alice`  | `Test1234!`  | api-reader | own orders (1–3) |
| `bob`    | `Admin1234!` | api-admin  | all orders (1–5) |

**Service accounts:**

| Client             | Grant type         | Role       |
|--------------------|--------------------|------------|
| `background-worker`| Client Credentials | api-reader |

## Run the API

```bash
dotnet run --project src/KeycloakDemo.Api
```

API listens on `http://localhost:5050`.

## Run the Worker

The `background-worker` client is **not** in `demo-realm.json` — it must be provisioned via `keycloak-setup.sh` before starting the worker. The script is idempotent: existing clients are skipped, so it is safe to re-run against a realm already bootstrapped from the JSON import:

```bash
KC_WORKER_SECRET=s3cr3t KC_API_SECRET=apisecret ./keycloak-setup.sh
```

Then start the worker, passing the secret as an env var:

```bash
Keycloak__ClientSecret=s3cr3t dotnet run --project src/KeycloakDemo.Worker
```

`Keycloak__ClientSecret` must match `KC_WORKER_SECRET` used above. The secret is intentionally absent from `appsettings.json` — never store client secrets in source control.

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
| GET    | /orders           | Bearer (`api-reader`)     | 200      | Reader sees own orders; admin sees all             |
| GET    | /orders/service   | Bearer (`background-worker` azp + `api-reader`) | 200 | Service account only; human token → 403 |
| GET    | /admin/users      | Bearer (`api-admin`)      | 200      | Requires `api-admin` role                          |
| GET    | /audit/logs       | Bearer (`api-admin` + `audit.read` scope) | 200 | Requires role AND scope              |

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
      KeycloakOptions.cs                  # Audience + AdditionalAudiences config
      KeycloakWorkerOptions.cs            # Worker client credentials config
      KeycloakRoleClaimsTransformation.cs # Maps realm/client roles → ClaimTypes.Role
      ServiceAccountCurrentUser.cs        # ICurrentUser for hosted services (no HttpContext)
      TokenValidationConfig.cs            # ValidAudiences list (multi-audience support)
    DependencyInjection.cs                # AddInfrastructure — JWT config inline (no options.Audience)
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

## Key design decisions

### Multi-audience: why `options.Audience` is not set

Phase 3 used a single audience (`dotnet-api`). Phase 4 must accept tokens from three clients:

| Client | Token `aud` claim | Token `azp` claim | Who uses it |
|--------|-------------------|-------------------|-------------|
| `dotnet-api` | `dotnet-api` | `dotnet-api` | direct API calls |
| `web-frontend` | `web-frontend` | `web-frontend` | browser SPA users |
| `background-worker` | `dotnet-api` | `background-worker` | M2M worker |

Setting `options.Audience = "dotnet-api"` internally sets `TokenValidationParameters.ValidAudiences = ["dotnet-api"]`, overwriting any list set afterwards. Instead, `options.Audience` is left unset in `AddJwtBearer()` and `TokenValidationConfig.Build()` owns the full list:

```csharp
ValidAudiences = [options.Audience, ..options.AdditionalAudiences]
// → ["dotnet-api", "web-frontend"]
```

`AdditionalAudiences` is configured in `appsettings.json` — add new clients without touching code.

### `aud` vs `azp` — two different claims, two different questions

| Claim | Question it answers | Set by |
|-------|---------------------|--------|
| `aud` | Which resource is this token **for**? | Keycloak audience mapper |
| `azp` | Which client **requested** this token? | Keycloak (always) |

The `/orders/service` endpoint uses `azp` to restrict access to the background worker specifically — even if another client had `api-reader` role and a valid `aud`, it would be rejected because its `azp` ≠ `background-worker`.

Audience validation (`ValidateAudience = true`) is kept enabled. Disabling it (and relying solely on `azp`) would allow tokens issued for unrelated APIs to be replayed here — a weaker security posture.

## Series

| Part | Topic |
|------|-------|
| 1 | JWT Bearer auth, claim extraction |
| 2 | Role-based authorization via `IClaimsTransformation` |
| 3 | Clean Architecture — use cases, domain roles, testable business logic |
| **4** | Machine-to-machine auth: client credentials, worker service, multi-audience, `azp` policy ← you are here |
| 5 | Token lifecycle: refresh, rotation, revocation, blocklist |
