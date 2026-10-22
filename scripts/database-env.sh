#!/usr/bin/env bash
# Sourced after scripts/env.sh. Existing explicit connection strings are respected.
if [[ -f "$SPLIT_ROOT/.env" ]]; then
  set -a
  source "$SPLIT_ROOT/.env"
  set +a
fi
if [[ -z "${ConnectionStrings__Default:-}" && -n "${POSTGRES_APP_PASSWORD:-}" ]]; then
  export ConnectionStrings__Default="Host=127.0.0.1;Port=${POSTGRES_PORT:-5432};Database=splittogether;Username=split_app;Password=$POSTGRES_APP_PASSWORD;SSL Mode=Disable"
fi
if [[ -z "${SPLIT_MIGRATION_CONNECTION:-}" && -n "${POSTGRES_MIGRATOR_PASSWORD:-}" ]]; then
  export SPLIT_MIGRATION_CONNECTION="Host=127.0.0.1;Port=${POSTGRES_PORT:-5432};Database=splittogether;Username=split_migrator;Password=$POSTGRES_MIGRATOR_PASSWORD;SSL Mode=Disable"
fi
if [[ -z "${SPLIT_TEST_CONNECTION:-}" && -n "${POSTGRES_TEST_PASSWORD:-}" ]]; then
  export SPLIT_TEST_CONNECTION="Host=127.0.0.1;Port=${POSTGRES_PORT:-5432};Database=splittogether_test_local;Username=split_test;Password=$POSTGRES_TEST_PASSWORD;SSL Mode=Disable"
fi
