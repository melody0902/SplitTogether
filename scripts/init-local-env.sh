#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ -f .env ]]; then
  printf '.env already exists; preserved without changes.\n'
  exit 0
fi
umask 077
# Hex avoids shell, Compose interpolation and connection-string escaping issues.
admin_password=$(openssl rand -hex 24)
app_password=$(openssl rand -hex 24)
migration_password=$(openssl rand -hex 24)
test_password=$(openssl rand -hex 24)
(set -o noclobber
printf '%s\n' \
  '# Local development secrets; never commit this file.' \
  'POSTGRES_PORT=5432' \
  "POSTGRES_PASSWORD='$admin_password'" \
  "POSTGRES_APP_PASSWORD='$app_password'" \
  "POSTGRES_MIGRATOR_PASSWORD='$migration_password'" \
  "POSTGRES_TEST_PASSWORD='$test_password'" \
  "Authentication__Google__ClientId=''" \
  "Authentication__Google__ClientSecret=''" > .env)
printf 'Created .env with unique PostgreSQL passwords. Google credentials are empty.\n'
