#!/usr/bin/env bash
# Prints fresh random values for the secrets of `.env`. Run it ON THE SERVER and paste the values into `.env` yourself;
# do not copy them into chat, tickets or git.
set -euo pipefail

echo "POSTGRES_PASSWORD=$(openssl rand -hex 24)"
echo "JWT_SIGNING_KEY=$(openssl rand -hex 48)"
echo "IMAP_ENCRYPTION_KEY=$(openssl rand -base64 32)"
# The API refuses a password without upper case, lower case, a digit and a symbol (at least 12 characters): this one always has all four.
echo "BOOTSTRAP_ADMIN_PASSWORD=$(openssl rand -hex 12)-Aa1"
