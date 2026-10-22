#!/usr/bin/env bash
SPLIT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export PATH="$SPLIT_ROOT/.tools/npm/node_modules/.bin:$SPLIT_ROOT/.tools/node/node_modules/node/bin:$SPLIT_ROOT/.tools/dotnet:$PATH"
if [[ -x "$SPLIT_ROOT/.tools/dotnet/dotnet" ]]; then export DOTNET_ROOT="$SPLIT_ROOT/.tools/dotnet"; fi
export DOTNET_CLI_HOME="$SPLIT_ROOT/.tools/dotnet-home"
export NUGET_PACKAGES="$SPLIT_ROOT/.tools/nuget"
export npm_config_cache="$SPLIT_ROOT/.tools/npm-cache"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_USE_POLLING_FILE_WATCHER=1
export DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
