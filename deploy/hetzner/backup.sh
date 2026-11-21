#!/usr/bin/env bash
# Daily database backup: a compressed pg_dump kept in ./backups (the newest BACKUP_KEEP_DAYS days).
# If RCLONE_REMOTE is set in .env (for example a Hetzner Storage Box configured with `rclone config`), each dump is also copied there.
# Run by hand or by cron (see RUNBOOK.md). The exit code is not 0 when the backup or the off-server copy failed.
set -euo pipefail
umask 077
cd "$(dirname "$0")"

# Read only the keys we need from .env (sourcing it would break on a value with spaces).
envval() { grep -E "^$1=" .env | tail -n 1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' || true; }
POSTGRES_USER="$(envval POSTGRES_USER)"; POSTGRES_USER="${POSTGRES_USER:-inspecta}"
POSTGRES_DB="$(envval POSTGRES_DB)"; POSTGRES_DB="${POSTGRES_DB:-inspecta}"
KEEP="$(envval BACKUP_KEEP_DAYS)"; KEEP="${KEEP:-14}"
RCLONE_REMOTE="$(envval RCLONE_REMOTE)"

STAMP="$(date +%Y%m%d-%H%M%S)"
FILE="backups/inspecta-${STAMP}.dump"
mkdir -p -m 700 backups

docker compose exec -T db pg_dump -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -Fc > "${FILE}.partial"
# A dump that is almost empty means something went wrong: do not keep it as a good backup.
if [ "$(stat -c %s "${FILE}.partial")" -lt 2048 ]; then
  echo "Backup is suspiciously small (${FILE}.partial), failing."
  exit 1
fi
mv "${FILE}.partial" "${FILE}"
echo "Backup written: ${FILE} ($(du -h "${FILE}" | cut -f1))"

if [ -n "${RCLONE_REMOTE}" ]; then
  # Not part of an && list: a failed copy must fail the whole script.
  rclone copy "${FILE}" "${RCLONE_REMOTE}:inspecta-backups/"
  echo "Copied to ${RCLONE_REMOTE}"
  rclone delete "${RCLONE_REMOTE}:inspecta-backups/" --min-age "${KEEP}d" || echo "Could not prune old copies on ${RCLONE_REMOTE} (not fatal)."
fi

# Keep the newest KEEP days here, and drop failed partial files older than a day.
find backups -name 'inspecta-*.dump' -mtime +"${KEEP}" -delete
find backups -name 'inspecta-*.partial' -mtime +1 -delete
