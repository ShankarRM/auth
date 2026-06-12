# Keycloak + .NET 8 Web API — Part 1

Source code for the blog series **"Identity Foundations: Keycloak + .NET 8 Web API from Scratch"**.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- Docker + Docker Compose
- `jq` (`brew install jq`)

## Start Keycloak

```bash
docker compose up -d
```

Admin console: `http://localhost:9093` — credentials `admin` / `admin`

### Keycloak setup (one-time)

1. Create realm `demo`
2. Create client `demo-api`
   - Client authentication: **On**
   - Authentication flows: enable **Direct access grants**
3. Create test user `testuser` / `testpass`

## Run the API

```bash
dotnet run
```

API listens on `http://localhost:5050`. Port 5000 is reserved by macOS AirPlay.

## Test

```bash
# All scenarios in one shot
./get-token.sh

# Or use the REST Client file in VS Code / Rider
# Set @token in KeycloakDemo.http, then send requests
```

## Endpoints

| Method | Path      | Auth     | Description                          |
|--------|-----------|----------|--------------------------------------|
| GET    | /health   | None     | Liveness probe                       |
| GET    | /me       | Bearer   | Returns `sub`, `username`, `email`   |
| GET    | /orders   | Bearer   | Returns hardcoded order list         |

## Project structure

```
docker-compose.yml        # Keycloak 26.1.0 on :9093
KeycloakDemo.csproj
Program.cs                # Minimal API, JWT auth wired
appsettings.json          # Keycloak:Authority / Audience / RequireHttpsMetadata
appsettings.Development.json  # HTTPS off, Trace auth logging
KeycloakDemo.http         # VS Code REST Client requests
get-token.sh              # curl-based end-to-end test script
```

## Series

| Part | Topic |
|------|-------|
| **1** | JWT Bearer auth, claim extraction ← you are here |
| 2 | Role-based authorization via `IClaimsTransformation` |
| 3 | Clean Architecture layering |
