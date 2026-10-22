#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh
source scripts/docker-env.sh
bash scripts/init-local-env.sh
if ! command -v docker >/dev/null; then
  printf 'Docker is missing. Run bash scripts/setup-docker-mac.sh or install Docker Desktop.\n' >&2
  exit 1
fi
if ! docker info >/dev/null 2>&1; then
  printf 'Docker is not running. Start Docker Desktop or run bash scripts/start-docker-mac.sh.\n' >&2
  exit 1
fi
docker compose --env-file .env -f infra/compose.yml up -d --wait --wait-timeout 120
printf 'PostgreSQL is ready. Next: bash scripts/migrate.sh\n'
