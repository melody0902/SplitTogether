#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh
source scripts/docker-env.sh
command -v colima >/dev/null || { printf 'Run bash scripts/setup-docker-mac.sh first.\n' >&2; exit 1; }
# A dedicated profile leaves other Docker projects/contexts alone. The engine is
# real Docker inside a native ARM Linux VM, managed by Apple's virtualization.
export XDG_CACHE_HOME="$SPLIT_ROOT/.tools/container-cache"
export LIMA_CACHE_HOME="$SPLIT_ROOT/.tools/container-cache/lima"
mkdir -p "$XDG_CACHE_HOME" "$LIMA_CACHE_HOME"
colima start --profile splittogether --runtime docker --vm-type vz --arch aarch64 --cpus 2 --memory 2 --disk 20 --activate=false --ssh-config=false --mount "$SPLIT_ROOT:w" --downloader curl
export DOCKER_HOST="unix://${COLIMA_HOME:-$HOME/.colima}/splittogether/docker.sock"
docker info --format 'Docker {{.ServerVersion}} is ready ({{.Architecture}}).'
