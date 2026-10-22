#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh
source scripts/docker-env.sh
exec docker compose --env-file .env -f infra/compose.yml stop
