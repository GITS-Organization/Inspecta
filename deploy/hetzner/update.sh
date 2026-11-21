#!/usr/bin/env bash
# Update the running server to the latest `backend` branch (or to a given commit/tag): backup first, then build and restart.
#   ./update.sh            -> latest backend
#   ./update.sh <commit>   -> that commit (also how you go back: `./update.sh <older commit>`)
# The API applies new database migrations by itself when it starts. Roll back the code with a commit, restore the data from a backup.
set -euo pipefail
cd "$(dirname "$0")"

TARGET="${1:-}"
echo "== 1/4 backup =="
./backup.sh
BACKUP_FILE="$(ls -1t backups/inspecta-*.dump | head -n 1)"
echo "Backup before this update: ${BACKUP_FILE} (keep it: it matches the code that is running now: $(git rev-parse --short HEAD))"

echo "== 2/4 code =="
git fetch origin
if [ -n "${TARGET}" ]; then
  git checkout --detach "${TARGET}"
else
  git checkout backend
  git pull --ff-only origin backend
fi
git log --oneline -1

echo "== 3/4 build and restart =="
docker compose up -d --build
docker image prune -f >/dev/null

echo "== 4/4 health =="
for i in $(seq 1 30); do
  if docker compose exec -T api bash -c "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health HTTP/1.0\r\n\r\n' >&3 && head -n 1 <&3 | grep -q 200"; then
    echo "API is healthy. Rollback code: ./update.sh <commit>; data: see RUNBOOK.md (restore)."
    exit 0
  fi
  sleep 4
done
echo "The API did not become healthy: check 'docker compose logs api --tail 100'."
exit 1
