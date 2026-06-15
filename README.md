# Keycloak + .NET 9 Web API — Part 2

Source code for the blog series **"Identity Foundations: Keycloak + .NET 9 Web API from Scratch"**.

Part 2 adds `IClaimsTransformation`, domain roles, and policy-based authorization on top of the JWT baseline from Part 1.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- Docker + Docker Compose
- `jq` (`brew install jq`)

## Start Keycloak

```bash
docker compose up -d
```

Keycloak boots on `http://localhost:9093` and auto-imports `keycloak/demo-realm.json` on first start.  
Admin console credentials: `admin` / `admin`

> **Re-importing after realm changes:** the `--import-realm` flag only runs when the realm is absent.  
> To pick up changes to `demo-realm.json`, wipe the volume and restart:
> ```bash
> docker compose down -v && docker compose up -d
> ```

## Run the API

```bash
dotnet run
# or for file-watch hot reload:
dotnet watch run
```

API listens on `http://localhost:5050`.

## Test

```bash
# All 12 scenarios in one shot
./get-token.sh

# Or use the REST Client file in VS Code / Rider
# Set @token in KeycloakDemo.http, then send requests
```

## Users & roles

Seeded automatically by `keycloak/demo-realm.json`:

| Username    | Password    | Realm role   |
|-------------|-------------|--------------|
| `testuser`  | `testpass`  | `api-reader` |
| `adminuser` | `adminpass` | `api-admin`  |

## Endpoints

| Method | Path           | Policy        | Expected result                          |
|--------|----------------|---------------|------------------------------------------|
| GET    | `/health`      | None          | 200 — `"OK"`                             |
| GET    | `/me`          | Bearer        | 200 — `sub`, `username`, `email`         |
| GET    | `/orders`      | ReadAccess    | 200 — order list (api-reader or higher)  |
| GET    | `/admin/users` | AdminAccess   | 200 — user list (api-admin only)         |
| GET    | `/audit/logs`  | AuditAccess   | 200 — audit log (api-admin + audit.read scope) |

### Policy matrix

| Policy        | Requires role | Requires scope  |
|---------------|---------------|-----------------|
| `ReadAccess`  | `api-reader`  | —               |
| `AdminAccess` | `api-admin`   | —               |
| `AuditAccess` | `api-admin`   | `audit.read`    |

`audit.read` is an optional client scope — request it explicitly:

```bash
scope=openid profile email audit.read
```

## Project structure

```
docker-compose.yml                  # Keycloak 26.1.0 on :9093, auto-imports realm
keycloak/demo-realm.json            # Realm, client, users, roles, scopes — full config
KeycloakDemo.csproj
Program.cs                          # Minimal API, JWT auth, policies wired
AuthorizationPolicies.cs            # ReadAccess / AdminAccess / AuditAccess policy definitions
DomainRole.cs                       # Typed domain role constants
KeycloakRoleClaimsTransformation.cs # IClaimsTransformation — maps realm_access.roles → ClaimsIdentity
appsettings.json                    # Keycloak Authority / Audience / RequireHttpsMetadata
appsettings.Development.json        # HTTPS off, Trace auth logging
KeycloakDemo.http                   # VS Code REST Client requests
get-token.sh                        # curl-based end-to-end test (12 scenarios)
```

## Series

| Part | Topic |
|------|-------|
| 1 | JWT Bearer auth baseline, claim extraction |
| **2** | `IClaimsTransformation`, domain roles, policy-based auth ← you are here |
| 3 | Clean Architecture layering |
