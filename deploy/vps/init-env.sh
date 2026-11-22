#!/usr/bin/env bash
# Creates `.env` for you: the database password, the JWT key and the mail encryption key are generated here and never printed;
# you only type the public address, your e-mail, and the first administrator (name, e-mail and a password you choose).
# Run it ON THE SERVER, once: `./init-env.sh`. Nothing secret is shown, so nothing secret goes into a chat or a screenshot.
set -euo pipefail
cd "$(dirname "$0")"

if [ -f .env ]; then
  read -r -p ".env already exists. Overwrite it (the old values are lost)? [y/N] " answer
  [ "${answer:-N}" = "y" ] || { echo "Left .env as it is."; exit 0; }
fi

umask 077
cp .env.example .env

# Replace the value of KEY= in .env (any characters are fine in the value).
set_kv() {
  local key="$1" value="$2" escaped
  escaped="$(printf '%s' "$value" | sed -e 's/[\\|&]/\\&/g')"
  sed -i "s|^${key}=.*|${key}=${escaped}|" .env
}

read -r -p "Public address of this server (for example api2.gitsplatform.com): " domain
read -r -p "Your real e-mail (for the HTTPS certificate): " acme
read -r -p "First administrator e-mail: " admin_email
read -r -p "First administrator full name: " admin_name

while true; do
  read -r -s -p "First administrator password (at least 12 characters with upper case, lower case, a digit and a symbol): " admin_password; echo
  read -r -s -p "Again: " again; echo
  if [ "${admin_password}" != "${again}" ]; then echo "The two passwords differ, try again."; continue; fi
  if [ "${#admin_password}" -lt 12 ] || ! [[ "${admin_password}" =~ [A-Z] ]] || ! [[ "${admin_password}" =~ [a-z] ]] \
     || ! [[ "${admin_password}" =~ [0-9] ]] || ! [[ "${admin_password}" =~ [^A-Za-z0-9] ]]; then
    echo "Too weak: use at least 12 characters with upper case, lower case, a digit and a symbol."; continue
  fi
  break
done

set_kv API_DOMAIN "${domain}"
set_kv ACME_EMAIL "${acme}"
set_kv BOOTSTRAP_ADMIN_EMAIL "${admin_email}"
set_kv BOOTSTRAP_ADMIN_FULL_NAME "\"${admin_name}\""
set_kv BOOTSTRAP_ADMIN_PASSWORD "${admin_password}"
set_kv POSTGRES_PASSWORD "$(openssl rand -hex 24)"
set_kv JWT_SIGNING_KEY "$(openssl rand -hex 48)"
set_kv IMAP_ENCRYPTION_KEY "$(openssl rand -base64 32)"

chmod 600 .env
mkdir -p -m 700 backups

echo
echo "Done: .env created (permissions 600). Secrets were generated and are not shown."
echo "IMPORTANT: open the file once on YOUR screen (nano .env) and copy IMAP_ENCRYPTION_KEY and POSTGRES_PASSWORD to your password manager."
echo "If IMAP_ENCRYPTION_KEY is lost, saved mailbox passwords cannot be read again. Never paste these values into a chat."
echo "The first administrator is ${admin_email}; after the first sign-in change the password and clear BOOTSTRAP_ADMIN_* (all three) in .env."
