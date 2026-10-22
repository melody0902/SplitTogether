#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh
source scripts/database-env.sh
export DOTNET_USE_POLLING_FILE_WATCHER=1 DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
: "${SPLIT_TEST_CONNECTION:?Set dedicated PostgreSQL test connection}"
[[ -f .cert/localhost.crt ]] || bash scripts/setup-https.sh
export ASPNETCORE_URLS=http://127.0.0.1:5043
export DataProtection__KeyPath="$PWD/.keys-test"
mkdir -p .keys-test
exec dotnet run --project tests/TestHost --no-launch-profile
