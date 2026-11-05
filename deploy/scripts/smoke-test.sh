#!/bin/sh
# Checks a running compose stack through Caddy, the same way a browser reaches it.
# Usage: sh deploy/scripts/smoke-test.sh <patient-host> <api-host> <staff-host> [address]
# The address defaults to 127.0.0.1; certificates are not verified (local CA for *.localhost).
set -eu
patient=$1 api=$2 staff=$3 address=${4:-127.0.0.1}
# request <host> <curl arguments...>: sends the request to Caddy at $address with SNI and Host set to <host>.
request() { host=$1; shift; curl -ksS --max-time 20 --resolve "$host:443:$address" "$@"; }
# GitHub Actions shows ::error lines as annotations, which are readable without opening the log.
fail() { echo "::error title=Container smoke test::$*"; echo "FAIL: $*" >&2; exit 1; }

echo "Waiting for the API through Caddy..."
for attempt in $(seq 1 30); do
  [ "$(request "$api" -o /dev/null -w '%{http_code}' "https://$api/health/ready" 2>/dev/null || true)" = 200 ] && break
  [ "$attempt" = 30 ] && fail "API is not ready"
  sleep 5
done

ready=$(request "$api" "https://$api/health/ready"); [ "$ready" = Healthy ] || fail "API readiness: $ready"
ready=$(request "$staff" "https://$staff/health/ready"); [ "$ready" = Healthy ] || fail "staff panel readiness: $ready"
echo "OK: API and staff panel report a migrated database"

request "$staff" "https://$staff/Account/Login" | grep -q '__RequestVerificationToken' || fail "staff login form"
echo "OK: staff login form renders"

request "$patient" "https://$patient/" | grep -q 'id="app"' || fail "patient app index"
request "$patient" "https://$patient/doctors/search" | grep -q 'id="app"' || fail "patient SPA fallback"
request "$patient" "https://$patient/appsettings.json" | grep -q "https://$api" || fail "patient runtime settings"
request "$patient" -I "https://$patient/" | grep -qi '^content-security-policy:' || fail "patient security headers"
echo "OK: patient app, SPA fallback, runtime settings and headers"

login=$(request "$api" -H 'Content-Type: application/json' \
  -d '{"email":"patient@example.test","password":"DemoPatient2026!"}' "https://$api/api/auth/login")
echo "$login" | grep -q '"token"' || fail "demo patient login: $login"
echo "OK: demo patient can log in through the API"
echo "Smoke test passed."
