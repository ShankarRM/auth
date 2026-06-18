#!/usr/bin/env bash
# =============================================================================
# Part 4: Multi-client Keycloak realm setup using Admin REST API
# Tested against Keycloak 26.x
#
# Usage:
#   KC_ADMIN_PASSWORD=admin KC_WORKER_SECRET=s3cr3t KC_API_SECRET=apisecret ./keycloak-setup.sh
#
# Required env vars:
#   KC_ADMIN_PASSWORD  — Keycloak admin password (default: admin)
#   KC_WORKER_SECRET   — client secret for background-worker (no default)
#   KC_API_SECRET      — client secret for dotnet-api (no default)
#
# Optional env vars:
#   KEYCLOAK_URL       — base URL of Keycloak (default: http://localhost:9093)
#   KC_ADMIN           — admin username (default: admin)
# =============================================================================
set -euo pipefail

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:9093}"
REALM="demo"
ADMIN_USER="${KC_ADMIN:-admin}"
ADMIN_PASS="${KC_ADMIN_PASSWORD:-admin}"
WORKER_SECRET="${KC_WORKER_SECRET:?KC_WORKER_SECRET env var is required}"
API_SECRET="${KC_API_SECRET:?KC_API_SECRET env var is required}"

# jq is required for JSON parsing
command -v jq >/dev/null 2>&1 || { echo "jq is required but not installed. Aborting." >&2; exit 1; }

echo "==> Connecting to Keycloak at $KEYCLOAK_URL"

# ── 1. Obtain admin token ──────────────────────────────────────────────────────
# The master realm's admin-cli client issues tokens for Admin API calls.
# We use the Resource Owner Password Credentials grant here because this is a
# setup script running with known admin credentials — never do this in application code.
ADMIN_TOKEN=$(curl -s -f -X POST \
  "$KEYCLOAK_URL/realms/master/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=admin-cli&username=$ADMIN_USER&password=$ADMIN_PASS" \
  | jq -r '.access_token')

echo "✓ Admin token obtained"

# Convenience wrapper: every Admin API call needs this header.
kc_post() { curl -s -f -X POST -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" "$@"; }
kc_get()  { curl -s -f -X GET  -H "Authorization: Bearer $ADMIN_TOKEN" "$@"; }
kc_put()  { curl -s -f -X PUT  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" "$@"; }

BASE="$KEYCLOAK_URL/admin/realms"

# ── 2. Create realm ────────────────────────────────────────────────────────────
# A realm is an isolated authentication domain. All three clients live in "demo".
# registrationAllowed: false — users are created by admins only, not self-registered.
kc_post "$BASE" -d "{
  \"realm\":               \"$REALM\",
  \"enabled\":             true,
  \"registrationAllowed\": false,
  \"loginTheme\":          \"keycloak\",
  \"accessTokenLifespan\": 300
}" || echo "  (realm may already exist, continuing)"
echo "✓ Realm '$REALM' created"

# ── 3. Create dotnet-api client ────────────────────────────────────────────────
# Confidential client for the .NET API server. Enables Authorization Code flow
# so human users can authenticate through a browser login page.
# directAccessGrantsEnabled: true — allows Resource Owner Password (dev/test only).
kc_post "$BASE/$REALM/clients" -d "{
  \"clientId\":                   \"dotnet-api\",
  \"name\":                       \".NET Demo API\",
  \"enabled\":                    true,
  \"protocol\":                   \"openid-connect\",
  \"publicClient\":               false,
  \"standardFlowEnabled\":        true,
  \"directAccessGrantsEnabled\":  true,
  \"serviceAccountsEnabled\":     false,
  \"secret\":                     \"$API_SECRET\",
  \"redirectUris\":               [\"http://localhost:5000/*\"],
  \"webOrigins\":                 [\"+\"]
}" || echo "  (dotnet-api may already exist, continuing)"
echo "✓ Client 'dotnet-api' created (confidential, Authorization Code)"

# ── 4. Create web-frontend client ──────────────────────────────────────────────
# Public client for the browser SPA. Public clients cannot store a secret —
# the code is downloaded to the browser where any user can inspect it.
# PKCE (Proof Key for Code Exchange) mitigates the auth code interception attack:
#   1. Client generates a random code_verifier.
#   2. Client sends SHA256(code_verifier) = code_challenge to Keycloak with the auth request.
#   3. Attacker intercepts the auth code but doesn't have code_verifier.
#   4. Keycloak rejects the token exchange if code_verifier doesn't match.
# pkceCodeChallengeMethod: S256 enforces SHA-256 — "plain" method offers no real security.
kc_post "$BASE/$REALM/clients" -d '{
  "clientId":                   "web-frontend",
  "name":                       "Web Frontend (SPA)",
  "enabled":                    true,
  "protocol":                   "openid-connect",
  "publicClient":               true,
  "standardFlowEnabled":        true,
  "directAccessGrantsEnabled":  false,
  "serviceAccountsEnabled":     false,
  "attributes": {
    "pkce.code.challenge.method": "S256"
  },
  "redirectUris":               ["http://localhost:5173/*"],
  "webOrigins":                 ["http://localhost:5173"]
}' || echo "  (web-frontend may already exist, continuing)"
echo "✓ Client 'web-frontend' created (public, PKCE)"

# ── 5. Create background-worker client ────────────────────────────────────────
# Confidential client for the background service. Uses Client Credentials flow:
# no user is involved — the service authenticates as itself using client_id + client_secret.
# standardFlowEnabled: false    — no browser redirects needed
# directAccessGrantsEnabled: false — no Resource Owner Password
# serviceAccountsEnabled: true  — creates the service-account-background-worker user
#                                  that we will assign realm roles to
kc_post "$BASE/$REALM/clients" -d "{
  \"clientId\":                   \"background-worker\",
  \"name\":                       \"Background Worker Service\",
  \"enabled\":                    true,
  \"protocol\":                   \"openid-connect\",
  \"publicClient\":               false,
  \"standardFlowEnabled\":        false,
  \"directAccessGrantsEnabled\":  false,
  \"serviceAccountsEnabled\":     true,
  \"secret\":                     \"$WORKER_SECRET\"
}" || echo "  (background-worker may already exist, continuing)"
echo "✓ Client 'background-worker' created (confidential, Client Credentials)"

# ── 6. Create realm roles ──────────────────────────────────────────────────────
# Realm roles are available across all clients in the realm. Client roles are
# client-specific. We use realm roles so the background-worker service account
# (which is a realm-level concept) can hold the same role as human users.
for ROLE in api-reader api-admin api-supervisor; do
  kc_post "$BASE/$REALM/roles" -d "{\"name\": \"$ROLE\"}"
  echo "✓ Realm role '$ROLE' created"
done

# ── 7. Assign api-reader to background-worker service account ─────────────────
# The service account user created for the background-worker client needs the
# api-reader role so it can call GET /orders on the API.

# Step 7a: get the background-worker client's internal UUID.
WORKER_CLIENT_ID=$(kc_get "$BASE/$REALM/clients?clientId=background-worker" \
  | jq -r '.[0].id')
echo "  background-worker client UUID: $WORKER_CLIENT_ID"

# Step 7b: get the service account user's UUID (created automatically when
#          serviceAccountsEnabled = true).
SA_USER_ID=$(kc_get "$BASE/$REALM/clients/$WORKER_CLIENT_ID/service-account-user" \
  | jq -r '.id')
echo "  Service account user UUID: $SA_USER_ID"

# Step 7c: get the api-reader role representation (UUID + name required for assignment).
READER_ROLE=$(kc_get "$BASE/$REALM/roles/api-reader")
echo "  api-reader role: $(echo "$READER_ROLE" | jq -c '.')"

# Step 7d: assign the role to the service account user via realm-level role mapping.
kc_post "$BASE/$REALM/users/$SA_USER_ID/role-mappings/realm" \
  -d "[$(echo "$READER_ROLE" | jq -c '{id,name}')]"
echo "✓ api-reader assigned to background-worker service account"

# ── 8. Create test users ───────────────────────────────────────────────────────
# Helper: create a user, set password, and assign a realm role.
create_user() {
  local USERNAME="$1" PASSWORD="$2" ROLE="$3"

  # Create the user. emailVerified: true skips the email confirmation step.
  kc_post "$BASE/$REALM/users" -d "{
    \"username\":      \"$USERNAME\",
    \"enabled\":       true,
    \"emailVerified\": true,
    \"email\":         \"$USERNAME@example.com\"
  }"

  # Get the user's UUID by querying by username.
  local USER_ID
  USER_ID=$(kc_get "$BASE/$REALM/users?username=$USERNAME&exact=true" | jq -r '.[0].id')

  # Set a permanent password. temporary: false means the user is not forced to change it.
  kc_put "$BASE/$REALM/users/$USER_ID/reset-password" -d "{
    \"type\":      \"password\",
    \"value\":     \"$PASSWORD\",
    \"temporary\": false
  }"

  # Get the role representation and assign it to the user.
  local ROLE_REP
  ROLE_REP=$(kc_get "$BASE/$REALM/roles/$ROLE")
  kc_post "$BASE/$REALM/users/$USER_ID/role-mappings/realm" \
    -d "[$(echo "$ROLE_REP" | jq -c '{id,name}')]"

  echo "✓ User '$USERNAME' created with role '$ROLE'"
}

create_user "alice" "Test1234!"  "api-reader"
create_user "bob"   "Admin1234!" "api-admin"

echo ""
echo "==> Setup complete."
echo ""
echo "Client Credentials test:"
echo "  curl -s -X POST $KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token \\"
echo "    -d 'grant_type=client_credentials&client_id=background-worker&client_secret=\$KC_WORKER_SECRET' \\"
echo "    | jq -r '.access_token'"
echo ""
echo "Resource Owner Password test (dev only):"
echo "  curl -s -X POST $KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token \\"
echo "    -d 'grant_type=password&client_id=dotnet-api&client_secret=\$KC_API_SECRET&username=alice&password=Test1234!' \\"
echo "    | jq -r '.access_token'"
