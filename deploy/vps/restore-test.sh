#!/usr/bin/env bash
# Proves that the newest backup can really be restored: it restores it into a throwaway database, counts the tables and rows of a few
# key tables, and drops the database again. The live database is never touched. Run it after the first backup, and now and then.
set -euo pipefail
cd "$(dirname "$0")"

envval() { grep -E "^$1=" .env | tail -n 1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' || true; }
USER_NAME="$(envval POSTGRES_USER)"; USER_NAME="${USER_NAME:-inspecta}"
LATEST="$(ls -1t backups/inspecta-*.dump 2>/dev/null | head -n 1 || true)"
if [ -z "${LATEST}" ]; then echo "No backup found in ./backups. Run ./backup.sh first."; exit 1; fi
echo "Restoring ${LATEST} into a throwaway database..."

SCRATCH="restore_test_$(date +%s)"
docker compose exec -T db psql -U "${USER_NAME}" -d postgres -c "CREATE DATABASE ${SCRATCH};"
cleanup() { docker compose exec -T db psql -U "${USER_NAME}" -d postgres -c "DROP DATABASE IF EXISTS ${SCRATCH};" >/dev/null; }
trap cleanup EXIT

docker compose exec -T db pg_restore -U "${USER_NAME}" -d "${SCRATCH}" --no-owner < "${LATEST}"

docker compose exec -T db psql -U "${USER_NAME}" -d "${SCRATCH}" -c \
  "SELECT count(*) AS tables FROM information_schema.tables WHERE table_schema = 'public';"
docker compose exec -T db psql -U "${USER_NAME}" -d "${SCRATCH}" -c \
  "SELECT (SELECT count(*) FROM \"Users\") AS users, (SELECT count(*) FROM \"Requests\") AS requests, (SELECT count(*) FROM \"__EFMigrationsHistory\") AS migrations;"
echo "Restore test finished: the backup can be restored."
