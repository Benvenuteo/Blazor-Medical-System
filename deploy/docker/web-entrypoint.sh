#!/bin/sh
# Prepares runtime configuration from the container environment, so one image works for any domain:
# - the patient app's appsettings.json (precompressed copies are removed, otherwise Caddy would
#   serve the build-time file);
# - the Caddyfile, with an ACME account email only when one is configured (an empty `email`
#   option is a Caddyfile syntax error).
set -eu
: "${PATIENT_HOST:?Set PATIENT_HOST}"
: "${API_HOST:?Set API_HOST}"
: "${STAFF_HOST:?Set STAFF_HOST}"

settings=/srv/patient/appsettings.json
rm -f "$settings.br" "$settings.gz"
demo=false
[ "${DEMO_DATA:-false}" = true ] && [ -n "${SEED_PATIENT_PASSWORD:-}" ] && demo=true
# The demo password is deliberately public (shown on the login page); it is JSON-escaped here.
demo_password=$(printf '%s' "${SEED_PATIENT_PASSWORD:-}" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g')
cat > "$settings" <<EOF
{
  "MedicalBookingSystemAPIUrl": "https://${API_HOST}",
  "MedicalBookingSystemServerUrl": "https://${STAFF_HOST}",
  "Google": { "ClientId": "${GOOGLE_CLIENT_ID:-}" },
  "Demo": {
    "Enabled": ${demo},
    "PatientEmail": "patient@example.test",
    "PatientPassword": "$( [ "$demo" = true ] && printf '%s' "$demo_password" )"
  }
}
EOF

{
  if [ -n "${ACME_EMAIL:-}" ]; then
    printf '{\n\temail %s\n}\n\n' "$ACME_EMAIL"
  fi
  cat /etc/caddy/sites.caddy
} > /etc/caddy/Caddyfile

exec "$@"
