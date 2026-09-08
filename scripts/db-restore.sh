#!/usr/bin/env bash
# Restore a MySelf database dump made by db-backup.sh.
#
#   ./scripts/db-restore.sh backups/myself-20260908-120000.dump.gz
#
# Connection is resolved the same way as db-backup.sh ($DATABASE_URL, else .env).
# This DROPs and recreates every object in the target database — it will refuse to
# run unless CONFIRM=yes is set.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DUMP="${1:-}"
[[ -f "$DUMP" ]] || { echo "Usage: $0 <dump.gz>" >&2; exit 1; }

if [[ -z "${DATABASE_URL:-}" ]]; then
  conn="$(grep -E '^\s*ConnectionStrings__DefaultConnection=' "$ROOT/.env" | head -1 | cut -d= -f2-)"
  [[ -n "$conn" ]] || { echo "No DATABASE_URL and no ConnectionStrings__DefaultConnection in .env" >&2; exit 1; }
  get() { echo "$conn" | tr ';' '\n' | grep -i "^$1=" | head -1 | cut -d= -f2-; }
  export PGHOST="$(get Host)" PGPORT="$(get Port)" PGDATABASE="$(get Database)"
  export PGUSER="$(get Username)" PGPASSWORD="$(get Password)"
else
  proto_removed="${DATABASE_URL#*://}"; creds="${proto_removed%%@*}"; hostpart="${proto_removed#*@}"
  export PGUSER="${creds%%:*}" PGPASSWORD="${creds#*:}" PGDATABASE="${hostpart##*/}"
  hostport="${hostpart%%/*}"; export PGHOST="${hostport%%:*}" PGPORT="${hostport#*:}"
fi

if [[ "${CONFIRM:-}" != "yes" ]]; then
  echo "This will overwrite every table in $PGDATABASE@$PGHOST:$PGPORT from $DUMP." >&2
  echo "Re-run with CONFIRM=yes to proceed." >&2
  exit 1
fi

echo "Restoring $DUMP -> $PGDATABASE@$PGHOST:$PGPORT"
gunzip -c "$DUMP" | pg_restore --clean --if-exists --no-owner --no-privileges --dbname "$PGDATABASE"
echo "Done."
