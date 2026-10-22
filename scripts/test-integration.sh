#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh
source scripts/database-env.sh
: "${SPLIT_TEST_CONNECTION:?Run init-local-env.sh or set a dedicated PostgreSQL test connection}"
exec dotnet test tests/SplitTogether.Tests --nologo
