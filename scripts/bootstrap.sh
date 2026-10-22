#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .tools
command -v npm >/dev/null || { printf 'Install Node.js with npm first (Node 24 recommended).\n'; exit 1; }
if [[ ! -x .tools/node/node_modules/node/bin/node ]]; then npm install --prefix .tools/node node@24.21.0 --cache .tools/npm-cache --no-audit --no-fund; fi
export PATH="$PWD/.tools/node/node_modules/node/bin:$PATH"
if [[ ! -f .tools/npm/node_modules/npm/bin/npm-cli.js ]]; then npm install --prefix .tools/npm npm@11.6.2 --cache .tools/npm-cache --no-audit --no-fund; fi
if [[ ! -x .tools/dotnet/dotnet ]]; then
 curl -fsSL https://dot.net/v1/dotnet-install.sh -o .tools/dotnet-install.sh
 bash .tools/dotnet-install.sh --version 10.0.401 --install-dir "$PWD/.tools/dotnet" --no-path
fi
source scripts/env.sh
npm ci --prefix frontend --no-audit --no-fund
dotnet restore backend --locked-mode
dotnet tool restore
bash scripts/setup-https.sh
