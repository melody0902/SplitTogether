#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
[[ "$(uname -s)" == Darwin && "$(uname -m)" == arm64 ]] || {
  printf 'This bundled installer targets Apple Silicon. On other systems install Docker Desktop or Docker Engine.\n' >&2
  exit 1
}
mkdir -p .tools/downloads .tools/container/bin .tools/container/cli-plugins .tools/docker-config/cli-plugins
fetch_checked() {
  local url="$1" target="$2" expected="$3"
  if [[ ! -f "$target" ]] || [[ "$(shasum -a 256 "$target" | cut -d ' ' -f 1)" != "$expected" ]]; then
    curl -fLsS --retry 3 "$url" -o "$target.part"
    [[ "$(shasum -a 256 "$target.part" | cut -d ' ' -f 1)" == "$expected" ]] || { printf 'Checksum failed: %s\n' "$target" >&2; return 1; }
    mv "$target.part" "$target"
  fi
}
fetch_checked 'https://github.com/abiosoft/colima/releases/download/v0.10.3/colima-Darwin-arm64' \
  .tools/container/bin/colima '980ad8bf61a4ca370243f4cb41401a61276dcd2c2502bee7b9b86f9250169f34'
fetch_checked 'https://github.com/lima-vm/lima/releases/download/v2.2.0/lima-2.2.0-Darwin-arm64.tar.gz' \
  .tools/downloads/lima-2.2.0-Darwin-arm64.tar.gz 'bbdef91774885a0d05f7b048c4eb89ae2bcf3a0c252ae7ca7934e63df76d93c3'
fetch_checked 'https://github.com/docker/compose/releases/download/v5.5.1/docker-compose-darwin-aarch64' \
  .tools/container/cli-plugins/docker-compose '998735c9b6fe68a4f05895e6ea73d71ad06f9fc7046383ad89e47346781b6af5'
tar -xzf .tools/downloads/lima-2.2.0-Darwin-arm64.tar.gz -C .tools/container
if ! tar -tzf .tools/downloads/docker-29.8.2.tgz >/dev/null 2>&1; then
  curl -fLsS --retry 3 'https://download.docker.com/mac/static/stable/aarch64/docker-29.8.2.tgz' -o .tools/downloads/docker-29.8.2.tgz
fi
tar -xzf .tools/downloads/docker-29.8.2.tgz -C .tools/container/bin --strip-components=1 docker/docker
chmod +x .tools/container/bin/colima .tools/container/bin/docker .tools/container/cli-plugins/docker-compose
ln -sfn "$PWD/.tools/container/cli-plugins/docker-compose" .tools/docker-config/cli-plugins/docker-compose
printf 'Official ARM tools installed inside .tools. Next: bash scripts/start-docker-mac.sh\n'
