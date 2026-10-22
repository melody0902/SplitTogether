#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh
export DOTNET_USE_POLLING_FILE_WATCHER=1 DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
source scripts/database-env.sh
[[ -f .cert/localhost.crt ]] || bash scripts/setup-https.sh
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://127.0.0.1:5043
export DataProtection__KeyPath="$PWD/.keys"
mkdir -p .keys
exec dotnet run --project backend --no-launch-profile
