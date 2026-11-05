#!/bin/sh
# Creates the staff panel's Data Protection certificate and fills missing secrets in deploy/.env.
# Existing values and files are never overwritten. Run from the repository root: sh deploy/scripts/create-secrets.sh
set -eu
cd "$(dirname "$0")/.."
[ -f .env ] || { cp .env.example .env; echo "Created deploy/.env from the example - set the host names in it."; }
mkdir -p secrets
chmod 700 secrets

set_if_empty() {
  if grep -q "^$1=$" .env; then
    sed -i.bak "s|^$1=$|$1=$2|" .env && rm -f .env.bak
    echo "Generated $1."
  fi
}
set_if_empty JWT_SECRET_KEY "$(openssl rand -base64 48 | tr -d '\n')"
set_if_empty DATAPROTECTION_CERT_PASSWORD "$(openssl rand -hex 24)"

if [ ! -f secrets/dataprotection.pfx ]; then
  password=$(grep '^DATAPROTECTION_CERT_PASSWORD=' .env | cut -d= -f2-)
  work=$(mktemp -d)
  trap 'rm -rf "$work"' EXIT
  openssl req -x509 -newkey rsa:3072 -nodes -days 3650 -subj "/CN=MedicalBookingSystem Data Protection" \
    -keyout "$work/key.pem" -out "$work/cert.pem" 2>/dev/null
  openssl pkcs12 -export -inkey "$work/key.pem" -in "$work/cert.pem" -out secrets/dataprotection.pfx -passout "pass:$password"
  # The panel runs as a non-root user (UID 1654 in .NET images) and reads the file through a bind mount.
  chmod 644 secrets/dataprotection.pfx
  echo "Created deploy/secrets/dataprotection.pfx - back it up together with .env."
fi
chmod 600 .env
