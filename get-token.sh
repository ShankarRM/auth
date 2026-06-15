#!/usr/bin/env bash
# get-token.sh — Exercise the Keycloak + .NET 8 API from the terminal.
#
# Prerequisites:
#   - docker compose up -d
#   - dotnet run (or the API running on port 5050)
#   - A Keycloak realm named "demo" with:
#     - A confidential client "demo-api" (direct grants enabled)
#     - A test user "testuser" / "testpass"
#   - jq installed: brew install jq
#
# Usage: chmod +x get-token.sh && ./get-token.sh

set -euo pipefail

KC_URL="http://localhost:9093"
REALM="demo"
CLIENT_ID="demo-api"
USERNAME="testuser"
PASSWORD="testpass"
API_URL="http://localhost:5050"

SEPARATOR="────────────────────────────────────────────────────────────"

echo "$SEPARATOR"
echo "1. Fetch access token (Resource Owner Password Grant)"
echo "$SEPARATOR"

# Resource Owner Password Credentials grant is disabled by default in Keycloak.
# Enable it in the client settings: Authentication flows → Direct access grants.
# In production you'd use Authorization Code + PKCE instead.
TOKEN_RESPONSE=$(curl -s -X POST \
  "${KC_URL}/realms/${REALM}/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "grant_type=password" \
  --data-urlencode "client_id=${CLIENT_ID}" \
  --data-urlencode "username=${USERNAME}" \
  --data-urlencode "password=${PASSWORD}")

# Bail early with the raw error so the reader sees exactly what Keycloak said.
if echo "$TOKEN_RESPONSE" | jq -e '.error' > /dev/null 2>&1; then
  echo "Keycloak error:"
  echo "$TOKEN_RESPONSE" | jq .
  exit 1
fi

# Decode the JWT payload (middle segment) so the reader can inspect raw claims.
echo "$TOKEN_RESPONSE" | jq '{
  token_type,
  expires_in,
  access_token: (.access_token | split(".")[1] | @base64d | fromjson)
}'

ACCESS_TOKEN=$(echo "$TOKEN_RESPONSE" | jq -r '.access_token')

echo ""
echo "$SEPARATOR"
echo "2. GET /health — anonymous (no token)"
echo "$SEPARATOR"
curl -si "${API_URL}/health"
echo ""

echo ""
echo "$SEPARATOR"
echo "3. GET /me — valid token → 200"
echo "$SEPARATOR"
curl -s \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  "${API_URL}/me" | jq .

echo ""
echo "$SEPARATOR"
echo "4. GET /orders — valid token → 200"
echo "$SEPARATOR"
curl -s \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  "${API_URL}/orders" | jq .

echo ""
echo "$SEPARATOR"
echo "5. GET /me — no token → 401"
echo "$SEPARATOR"
# -i shows response headers; grep the status line only
curl -si "${API_URL}/me" | head -n 1

echo ""
echo "$SEPARATOR"
echo "6. GET /me — tampered token → 401"
echo "$SEPARATOR"
# Appending characters invalidates the signature. The API verifies the RS256
# signature against Keycloak's public key (fetched from the JWKS endpoint at
# startup). Any modification to header, payload, or signature = rejected.
TAMPERED="${ACCESS_TOKEN}TAMPERED"
curl -si \
  -H "Authorization: Bearer ${TAMPERED}" \
  "${API_URL}/me" | head -n 1
