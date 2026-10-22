#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh
[[ -f .cert/localhost.crt ]] || bash scripts/setup-https.sh
export CI=1
export NG_BUILD_MAX_WORKERS=2
cd frontend
exec node node_modules/@angular/cli/bin/ng.js serve --poll 2000 --host localhost --port 4200 --ssl --ssl-cert ../.cert/localhost.crt --ssl-key ../.cert/localhost.key --proxy-config proxy.conf.json
