#!/usr/bin/env bash
set -euo pipefail
# Runs once, only when the official image initializes an empty volume.
psql --username "$POSTGRES_USER" --dbname postgres --set=ON_ERROR_STOP=1 \
  --set=app_password="$POSTGRES_APP_PASSWORD" \
  --set=migrator_password="$POSTGRES_MIGRATOR_PASSWORD" \
  --set=test_password="$POSTGRES_TEST_PASSWORD" <<'SQL'
CREATE ROLE split_app LOGIN PASSWORD :'app_password' NOSUPERUSER NOCREATEDB NOCREATEROLE;
CREATE ROLE split_migrator LOGIN PASSWORD :'migrator_password' NOSUPERUSER NOCREATEDB NOCREATEROLE;
CREATE ROLE split_test LOGIN PASSWORD :'test_password' NOSUPERUSER NOCREATEDB NOCREATEROLE;
CREATE DATABASE splittogether OWNER split_migrator;
REVOKE ALL ON DATABASE splittogether FROM PUBLIC;
GRANT CONNECT ON DATABASE splittogether TO split_app;
CREATE DATABASE splittogether_test_local OWNER split_test;
REVOKE ALL ON DATABASE splittogether_test_local FROM PUBLIC;
\connect splittogether
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO split_app;
ALTER DEFAULT PRIVILEGES FOR ROLE split_migrator IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO split_app;
ALTER DEFAULT PRIVILEGES FOR ROLE split_migrator IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO split_app;
SQL
