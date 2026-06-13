# Token Flows — Part 4: Multi-Client Topology

## Authorization Code + PKCE (web-frontend → API)

Used when a human user logs in via a browser-based SPA.

```
Browser (SPA)                  Keycloak                    .NET API
     |                             |                            |
     |--- (1) Login click -------->|                            |
     |    code_challenge sent      |                            |
     |                             |                            |
     |<-- (2) Login page ----------|                            |
     |                             |                            |
     |--- (3) username/password -->|                            |
     |                             |                            |
     |<-- (4) auth_code -----------|                            |
     |    (302 redirect to SPA)    |                            |
     |                             |                            |
     |--- (5) auth_code +          |                            |
     |    code_verifier ---------->|                            |
     |                             |                            |
     |<-- (6) access_token --------|                            |
     |        refresh_token        |                            |
     |        id_token             |                            |
     |                             |                            |
     |--- (7) GET /orders ---------|--------------------------->|
     |    Authorization: Bearer    |             validates JWT  |
     |    <access_token>           |          checks audience   |
     |                             |          checks roles      |
     |<--------------------------- |--------------------------- |
     |    200 OK [ orders ]        |                            |
```

### Why PKCE?

Public clients (SPAs, mobile apps) cannot store a client secret — the code runs in
an environment the user controls. An attacker could:

1. Intercept the `auth_code` in a redirect (e.g. via a malicious registered URI).
2. Exchange the `auth_code` for a token without a secret (public client, no secret check).

PKCE breaks step 2:

- **Before the auth request**: the client generates a random `code_verifier` (43-128 chars).
- **Auth request**: client sends `code_challenge = BASE64URL(SHA256(code_verifier))`.
- **Token request**: client must send the original `code_verifier`.
- **Keycloak verifies**: `SHA256(code_verifier) == code_challenge` → token issued.

The attacker who intercepted the `auth_code` does not have `code_verifier` — they cannot
complete the exchange. `pkce.code.challenge.method = S256` enforces SHA-256 hashing;
the `plain` method offers no real protection.

---

## Client Credentials (background-worker → API)

Used when a machine authenticates as itself — no user is involved.

```
background-worker              Keycloak                    .NET API
     |                             |                            |
     |--- (1) client_id +          |                            |
     |    client_secret ---------->|                            |
     |    grant_type=              |                            |
     |    client_credentials       |                            |
     |                             |                            |
     |<-- (2) access_token --------|                            |
     |    (no refresh_token,       |                            |
     |     no id_token)            |                            |
     |                             |                            |
     |    [cache token until       |                            |
     |     expiry - 30s]           |                            |
     |                             |                            |
     |--- (3) GET /orders/service -|--------------------------->|
     |    Authorization: Bearer    |             validates JWT  |
     |    <access_token>           |          checks azp claim  |
     |                             |          checks roles      |
     |<--------------------------- |--------------------------- |
     |    200 OK [ orders ]        |                            |
     |                             |                            |
     |    [30 seconds later...]    |                            |
     |                             |                            |
     |--- (4) GET /orders/service  |                            |
     |    (same cached token) -----|--------------------------->|
```

### Why Client Credentials?

Background services have no user. They cannot redirect to a browser login page.
The service knows its own identity (`client_id`) and proves it with a `client_secret`.
Keycloak issues a token for the service account associated with that client.

Critically, there is **no refresh token** — when the access token expires, the worker
fetches a new one by repeating the Client Credentials grant. `KeycloakTokenService`
automates this with a cache.

---

## Token Audiences in a Multi-Client Setup

### `aud` vs `azp`

| Claim | Meaning | Example value |
|-------|---------|---------------|
| `aud` | **Audience** — the resource the token is *intended for* | `["dotnet-api", "account"]` |
| `azp` | **Authorized Party** — the *client that requested* the token | `"web-frontend"` |

### Scenario: User logs in via web-frontend, calls the API

```json
{
  "aud": ["web-frontend", "account"],
  "azp": "web-frontend",
  "preferred_username": "testuser",
  "realm_access": { "roles": ["api-reader"] }
}
```

The API must accept `aud = "web-frontend"` — that's what `AdditionalAudiences` is for.
`azp = "web-frontend"` tells you the token came from a browser user, not a service.

### Scenario: background-worker fetches its own token

```json
{
  "aud": ["dotnet-api", "account"],
  "azp": "background-worker",
  "preferred_username": "service-account-background-worker",
  "realm_access": { "roles": ["api-reader"] }
}
```

`aud` still contains `dotnet-api` (Keycloak includes the target resource as audience
for service account tokens when you configure the audience mapper on the client).
`azp = "background-worker"` is what the `ServiceAccountOnly` policy checks.

**Key insight**: `aud` says *what* the token is for; `azp` says *who got it*.
They answer different security questions.

---

## Client Topology Decision Guide

| Scenario | Client Type | Flow | Why |
|----------|-------------|------|-----|
| Browser SPA | Public | Authorization Code + PKCE | Can't store secrets — PKCE prevents code interception |
| Server-side web app | Confidential | Authorization Code | Server can store secret securely |
| Mobile app | Public | Authorization Code + PKCE | Can't store secrets — same as SPA |
| Background service | Confidential | Client Credentials | No user context — authenticates as itself |
| Service-to-service | Confidential | Client Credentials | Machine identity, no browser |
| CLI tool (no browser) | Public | Device Authorization | User approves on a separate device |
