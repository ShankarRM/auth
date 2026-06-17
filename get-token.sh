#!/usr/bin/env bash
# get-token.sh — Exercise the Keycloak + .NET API from the terminal.
#
# Prerequisites:
#   - docker compose up -d  (Keycloak running on port 9093)
#   - dotnet watch run      (API running on port 5050)
#   - jq installed: brew install jq
#
# Users seeded by demo-realm.json:
#   alice  / alice  → api-reader role
#   bob   / bob   → api-admin role
#
# Usage: chmod +x get-token.sh && ./get-token.sh

set -euo pipefail

KC_URL="http://localhost:9093"
REALM="demo"
CLIENT_ID="demo-api"
API_URL="http://localhost:5050"

SEP="────────────────────────────────────────────────────────────"

# ── Helper ─────────────────────────────────────────────────────────────────────

get_token() {
  local user="$1" pass="$2" scope="${3:-}"
  local data="grant_type=password&client_id=${CLIENT_ID}&username=${user}&password=${pass}"
  [ -n "$scope" ] && data="${data}&scope=${scope}"

  curl -s -X POST \
    "${KC_URL}/realms/${REALM}/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "$data"
}

decode_token() {
  echo "$1" | jq -r '.access_token | split(".")[1] | @base64d | fromjson'
}

# ── 1. Reader token ────────────────────────────────────────────────────────────
echo "$SEP"
echo "1. Fetch reader token (alice → api-reader role)"
echo "$SEP"

READER_RESP=$(get_token "alice" "alice")
if echo "$READER_RESP" | jq -e '.error' > /dev/null 2>&1; then
  echo "Keycloak error:"; echo "$READER_RESP" | jq .; exit 1
fi

echo "Raw response:"
echo "$READER_RESP" | jq .
echo "Decoded payload:"
decode_token "$READER_RESP" | jq '{scope, realm_access, resource_access}'
READER_TOKEN=$(echo "$READER_RESP" | jq -r '.access_token')

# ── 2. Admin token ─────────────────────────────────────────────────────────────
echo ""
echo "$SEP"
echo "2. Fetch admin token (bob → api-admin role)"
echo "$SEP"

ADMIN_RESP=$(get_token "bob" "bob")
if echo "$ADMIN_RESP" | jq -e '.error' > /dev/null 2>&1; then
  echo "Keycloak error:"; echo "$ADMIN_RESP" | jq .; exit 1
fi

echo "Raw response:"
echo "$ADMIN_RESP" | jq .
echo "Decoded payload:"
decode_token "$ADMIN_RESP" | jq '{scope, realm_access, resource_access}'
ADMIN_TOKEN=$(echo "$ADMIN_RESP" | jq -r '.access_token')

# ── 3. Admin + audit.read scope ────────────────────────────────────────────────
echo ""
echo "$SEP"
echo "3. Fetch admin token WITH audit.read scope"
echo "$SEP"

AUDIT_RESP=$(get_token "bob" "bob" "openid profile email audit.read")
if echo "$AUDIT_RESP" | jq -e '.error' > /dev/null 2>&1; then
  echo "Keycloak error:"; echo "$AUDIT_RESP" | jq .; exit 1
fi

echo "Raw response:"
echo "$AUDIT_RESP" | jq .
echo "Decoded payload (scope field should include 'audit.read'):"
decode_token "$AUDIT_RESP" | jq '{scope, realm_access}'
AUDIT_TOKEN=$(echo "$AUDIT_RESP" | jq -r '.access_token')

# ── 4. /health ─────────────────────────────────────────────────────────────────
echo ""
echo "$SEP"
echo "4. GET /health — anonymous → 200"
echo "$SEP"
curl -si "${API_URL}/health"
echo ""

# ── 5. /me ─────────────────────────────────────────────────────────────────────
echo ""
echo "$SEP"
echo "5. GET /me — reader token → 200"
echo "$SEP"
curl -s -H "Authorization: Bearer ${READER_TOKEN}" "${API_URL}/me" | jq .

# ── 6. /orders ─────────────────────────────────────────────────────────────────
echo ""
echo "$SEP"
echo "6. GET /orders — reader token → 200 (ReadAccess policy)"
echo "$SEP"
curl -s -H "Authorization: Bearer ${READER_TOKEN}" "${API_URL}/orders" | jq .

echo ""
echo "$SEP"
echo "7. GET /orders — admin token → 200 (all 5 orders)"
echo "$SEP"
curl -s -H "Authorization: Bearer ${ADMIN_TOKEN}" "${API_URL}/orders" | jq .

echo ""
echo "$SEP"
echo "8. GET /orders — no token → 401"
echo "$SEP"
curl -si "${API_URL}/orders" | head -n 1

# ── 9. /admin/users ────────────────────────────────────────────────────────────
echo ""
echo "$SEP"
echo "9. GET /admin/users — admin token → 200 (AdminAccess policy)"
echo "$SEP"
curl -s -H "Authorization: Bearer ${ADMIN_TOKEN}" "${API_URL}/admin/users" | jq .

echo ""
echo "$SEP"
echo "10. GET /admin/users — reader token → 403 (api-reader cannot admin)"
echo "$SEP"
curl -si -H "Authorization: Bearer ${READER_TOKEN}" "${API_URL}/admin/users" | head -n 1

# ── 11. /audit/logs ────────────────────────────────────────────────────────────
echo ""
echo "$SEP"
echo "11. GET /audit/logs — admin + audit.read scope → 200 (AuditAccess policy)"
echo "$SEP"
curl -s -H "Authorization: Bearer ${AUDIT_TOKEN}" "${API_URL}/audit/logs" | jq .

echo ""
echo "$SEP"
echo "12. GET /audit/logs — admin token, NO audit.read scope → 403"
echo "$SEP"
curl -si -H "Authorization: Bearer ${ADMIN_TOKEN}" "${API_URL}/audit/logs" | head -n 1

echo ""
echo "$SEP"
echo "13. GET /me — tampered token → 401"
echo "$SEP"
curl -si -H "Authorization: Bearer ${READER_TOKEN}TAMPERED" "${API_URL}/me" | head -n 1
