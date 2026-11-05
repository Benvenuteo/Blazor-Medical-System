#!/bin/sh
# Runs on the server (piped over SSH by .github/workflows/deploy.yml):
#   sh -s -- <repository path> <commit sha> <image registry>
# Checks out the compose files of that commit, pulls its images, takes a backup, starts the new version and
# waits for health checks. If the new version does not become healthy, the previous images are started again.
set -eu
repo=$1 sha=$2 registry=$3
cd "$repo"
compose="docker compose -f deploy/docker-compose.yml --env-file deploy/.env"
previous=$(cat deploy/.deployed-tag 2>/dev/null || true)

git fetch --quiet origin
git checkout --quiet --detach "$sha"

export IMAGE_REGISTRY="$registry" IMAGE_TAG="$sha"
$compose --profile tools pull --quiet

backup_dir=$(grep '^BACKUP_DIR=' deploy/.env | cut -d= -f2- || true)
if $compose ps --status running --services 2>/dev/null | grep -q '^api$'; then
  $compose run --rm maintenance backup /data/db/medical.db /data/photos "/backups/pre-deploy-$(date +%Y%m%d-%H%M%S)" \
    || { echo "Backup failed; deployment stopped before changing anything." >&2; exit 1; }
fi

if $compose up -d --no-build --wait --wait-timeout 240; then
  echo "$sha" > deploy/.deployed-tag
  echo "Deployed $sha"
  exit 0
fi

echo "New version is not healthy." >&2
$compose logs --no-color --tail 80 api staff web >&2 || true
if [ -n "$previous" ]; then
  echo "Rolling back to $previous" >&2
  git checkout --quiet --detach "$previous"
  IMAGE_TAG="$previous" $compose up -d --no-build --wait --wait-timeout 240 || true
fi
exit 1
