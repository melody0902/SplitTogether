#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .cert
if [[ ! -f .cert/localhost.crt || ! -f .cert/localhost.key ]]; then
cat > .cert/openssl.cnf <<'CONF'
[req]
distinguished_name=dn
x509_extensions=v3
prompt=no
[dn]
CN=localhost
[v3]
subjectAltName=DNS:localhost,IP:127.0.0.1
basicConstraints=CA:FALSE
keyUsage=digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
CONF
openssl req -x509 -newkey rsa:2048 -nodes -keyout .cert/localhost.key -out .cert/localhost.crt -days 365 -config .cert/openssl.cnf
chmod 600 .cert/localhost.key
fi
printf 'Local certificate ready. Trust .cert/localhost.crt in your OS/browser for interactive OAuth.\n'
