#!/usr/bin/env bash
# Project-local official binaries are optional; Docker Desktop also works.
export PATH="$SPLIT_ROOT/.tools/container/bin:$PATH"
if [[ -x "$SPLIT_ROOT/.tools/container/bin/docker" ]]; then
  export DOCKER_CONFIG="$SPLIT_ROOT/.tools/docker-config"
fi
if [[ -z "${DOCKER_HOST:-}" && -S "${COLIMA_HOME:-$HOME/.colima}/splittogether/docker.sock" ]]; then
  export DOCKER_HOST="unix://${COLIMA_HOME:-$HOME/.colima}/splittogether/docker.sock"
elif [[ -z "${DOCKER_HOST:-}" && -S "$HOME/.docker/run/docker.sock" ]]; then
  export DOCKER_HOST="unix://$HOME/.docker/run/docker.sock"
fi
