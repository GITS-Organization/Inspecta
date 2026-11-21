#!/usr/bin/env bash
# Daily database backup: a compressed pg_dump kept in ./backups (inside the db container as /backups too), the newest BACKUP_KEEP_DAYS days.
# If RCLONE_REMOTE is set in .env (for example a Hetzner Storage Box configured with `rclone config`), each dump is also copied there.
# Run by hand or by cron (see RUNBOOK.md). Exit code is not 0 when the backup failed.
set -euo pipefail
cd "$(dirname "$0")"

set -a; . ./.env; set +a
KEEP="${BACKUP_KEEP_DAYS:-14}"
STAMP="$(date +%Y%m%d-%H%M%S)"
FILE="backups/inspecta-${STAMP}.dump"
mkdir -p backups

docker compose exec -T db pg_dump -U "${POSTGRES_USER:-inspecta}" -d "${POSTGRES_DB:-inspecta}" -Fc > "${FILE}.partial"
# A dump that is almost empty means something went wrong: do not keep it as a good backup.
if [ "$(stat -c %s "${FILE}.partial")" -lt 2048 ]; then
  echo "Backup is suspiciously small, keeping it as ${FILE}.partial and failing."
  exit 1
fi
mv "${FILE}.partial" "${FILE}"
echo "Backup written: ${FILE} ($(du -h "${FILE}" | cut -f1))"

if [ -n "${RCLONE_REMOTE:-}" ]; then
  rclone copy "${FILE}" "${RCLONE_REMOTE}:inspecta-backups/" && echo "Copied to ${RCLONE_REMOTE}"
fi

# Keep the newest KEEP days here.
find backups -name 'inspecta-*.dump' -mtime +"${KEEP}" -delete
