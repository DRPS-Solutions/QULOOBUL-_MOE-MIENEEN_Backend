#!/usr/bin/env bash
# Post-deploy smoke test: the slot starts, reaches its database, and answers an authenticated read.
# Usage: smoke-test.sh <base-url>, with SMOKE_EMAIL / SMOKE_PASSWORD in the environment.
set -euo pipefail
base="$1"
curl -fsS --retry 5 --retry-all-errors --retry-delay 10 "$base/health"
body=$(jq -n --arg e "$SMOKE_EMAIL" --arg p "$SMOKE_PASSWORD" '{email: $e, password: $p}')
token=$(curl -fsS -H 'Content-Type: application/json' -d "$body" "$base/api/auth/login" | jq -r .token)
curl -fsS -o /dev/null -H "Authorization: Bearer $token" "$base/api/users"
echo "Smoke test passed: health, login and an authenticated read."
