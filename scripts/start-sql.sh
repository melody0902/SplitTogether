#!/usr/bin/env bash
# Backward-compatible entry point; now starts PostgreSQL.
set -euo pipefail
exec bash "$(dirname "$0")/start-db.sh" "$@"
