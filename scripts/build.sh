#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh
export CI=1 NG_BUILD_MAX_WORKERS=2
dotnet build backend --nologo
cd frontend
node node_modules/@angular/cli/bin/ng.js build --progress=false
