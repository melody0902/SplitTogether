#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh
source scripts/database-env.sh
: "${SPLIT_MIGRATION_CONNECTION:?Set PostgreSQL migration credentials via init-local-env.sh or SPLIT_MIGRATION_CONNECTION}"
export ConnectionStrings__Default="$SPLIT_MIGRATION_CONNECTION"
dotnet tool restore
exec dotnet ef database update --project backend
