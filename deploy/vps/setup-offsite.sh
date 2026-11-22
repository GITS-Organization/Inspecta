#!/usr/bin/env bash
# Sets up the off-server backup copy on Backblaze B2: asks for the bucket name, the key ID and the application key (typed hidden, never shown
# or stored anywhere except rclone's own config file with permission 600), tests the connection, writes RCLONE_REMOTE into .env and uploads a first backup.
# Run it ON THE SERVER: `./setup-offsite.sh`. Never paste the application key into a chat.
set -euo pipefail
cd "$(dirname "$0")"

command -v rclone >/dev/null || { echo "rclone is not installed (server-setup.sh installs it)."; exit 1; }
[ -f .env ] || { echo "No .env here: run ./init-env.sh first."; exit 1; }

read -r -p "Bucket name (for example inspecta-backups-02134): " bucket
read -r -p "Application key ID (keyID): " key_id
read -r -s -p "Application key (hidden): " app_key; echo
[ -n "${bucket}" ] && [ -n "${key_id}" ] && [ -n "${app_key}" ] || { echo "All three are required."; exit 1; }

REMOTE_NAME="b2backup"
umask 077
rclone config delete "${REMOTE_NAME}" >/dev/null 2>&1 || true
rclone config create "${REMOTE_NAME}" b2 account "${key_id}" key "${app_key}" >/dev/null
unset app_key
chmod 600 "$(rclone config file | tail -n 1)" 2>/dev/null || true

echo "Testing the connection to the bucket..."
if ! rclone lsd "${REMOTE_NAME}:" >/dev/null 2>&1 && ! rclone ls "${REMOTE_NAME}:${bucket}" --max-depth 1 >/dev/null 2>&1; then
  echo "Could not reach ${REMOTE_NAME}:${bucket}. Check the bucket name and that the key is allowed on this bucket (Read and Write)."
  exit 1
fi

if grep -q '^RCLONE_REMOTE=' .env; then
  sed -i "s|^RCLONE_REMOTE=.*|RCLONE_REMOTE=${REMOTE_NAME}:${bucket}/inspecta|" .env
else
  echo "RCLONE_REMOTE=${REMOTE_NAME}:${bucket}/inspecta" >> .env
fi
echo "RCLONE_REMOTE set to ${REMOTE_NAME}:${bucket}/inspecta"

echo "Running a backup and copying it off the server..."
./backup.sh
echo "Files now in the bucket:"
rclone ls "${REMOTE_NAME}:${bucket}/inspecta"
echo "Done. From now on the nightly backup is also copied to Backblaze."
