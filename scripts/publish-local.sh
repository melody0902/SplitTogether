#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh
bash scripts/build.sh
dotnet publish backend -c Release -o artifacts/app
mkdir -p artifacts/app/wwwroot
cp -R frontend/dist/split-together/browser/. artifacts/app/wwwroot/
printf 'Local deployment bundle: artifacts/app. Configure HTTPS, AllowedHosts, SQL and Google credentials before running.\n'
