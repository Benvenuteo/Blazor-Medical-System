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

# The panel is interactive only when the browser can load the Blazor script and keep a SignalR circuit open.
script=$(request "$staff" -o /dev/null -w '%{http_code} %{content_type}' "https://$staff/_framework/blazor.server.js")
case "$script" in "200 text/javascript"*) ;; *) fail "staff Blazor script: $script" ;; esac
jar=$(mktemp)
token=$(request "$staff" -c "$jar" -b "$jar" "https://$staff/Account/Login" |
  sed -n 's/.*name="__RequestVerificationToken" type="hidden" value="\([^"]*\)".*/\1/p' | head -n 1)
signin=$(request "$staff" -c "$jar" -b "$jar" -o /dev/null -w '%{http_code}' --data-urlencode "__RequestVerificationToken=$token" \
  --data-urlencode 'Email=doctor@example.test' --data-urlencode 'Password=DemoStaff2026!' "https://$staff/Account/Login")
[ "$signin" = 302 ] || fail "demo doctor sign-in to the staff panel: HTTP $signin"
negotiate=$(request "$staff" -b "$jar" -d '' "https://$staff/_blazor/negotiate?negotiateVersion=1")
echo "$negotiate" | grep -q '"WebSockets"' || fail "staff SignalR negotiate: $negotiate"
connection=$(echo "$negotiate" | sed -n 's/.*"connectionToken":"\([^"]*\)".*/\1/p')
# Caddy must pass the WebSocket upgrade through; the open connection then ends with the time limit.
upgrade=$(request "$staff" -b "$jar" --http1.1 -o /dev/null -w '%{http_code}' --max-time 5 \
  -H 'Connection: Upgrade' -H 'Upgrade: websocket' -H 'Sec-WebSocket-Version: 13' -H 'Sec-WebSocket-Key: c21va2UtdGVzdC1rZXkxMg==' \
  "https://$staff/_blazor?id=$connection" 2>/dev/null || true)
rm -f "$jar"
[ "$upgrade" = 101 ] || fail "staff WebSocket upgrade through Caddy: HTTP $upgrade"
echo "OK: staff panel serves the Blazor script and opens a SignalR WebSocket"

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
