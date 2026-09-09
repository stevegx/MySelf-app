#!/usr/bin/env bash
# Back up the MySelf PostgreSQL database to a timestamped, gzipped custom-format dump.
#
#   ./scripts/db-backup.sh [output-dir]
#
# Reads the connection from $DATABASE_URL (postgres://…) if set, otherwise parses
# ConnectionStrings__DefaultConnection out of the repo-root .env. Keeps the newest
# $KEEP dumps (default 14) and prunes the rest.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="${1:-$ROOT/backups}"
KEEP="${KEEP:-14}"
mkdir -p "$OUT_DIR"

if [[ -z "${DATABASE_URL:-}" ]]; then
  conn="$(grep -E '^\s*ConnectionStrings__DefaultConnection=' "$ROOT/.env" | head -1 | cut -d= -f2-)"
  [[ -n "$conn" ]] || { echo "No DATABASE_URL and no ConnectionStrings__DefaultConnection in .env" >&2; exit 1; }
  get() { echo "$conn" | tr ';' '\n' | grep -i "^$1=" | head -1 | cut -d= -f2-; }
  export PGHOST="$(get Host)"
  export PGPORT="$(get Port)"
  export PGDATABASE="$(get Database)"
  export PGUSER="$(get Username)"
  export PGPASSWORD="$(get Password)"
else
  export PGHOST PGPORT PGDATABASE PGUSER PGPASSWORD
  # postgres://user:pass@host:port/db
  proto_removed="${DATABASE_URL#*://}"
  creds="${proto_removed%%@*}"; hostpart="${proto_removed#*@}"
  PGUSER="${creds%%:*}"; PGPASSWORD="${creds#*:}"
  PGDATABASE="${hostpart##*/}"; hostport="${hostpart%%/*}"
  PGHOST="${hostport%%:*}"; PGPORT="${hostport#*:}"
fi

stamp="$(date +%Y%m%d-%H%M%S)"
file="$OUT_DIR/${PGDATABASE}-${stamp}.dump.gz"

echo "Backing up $PGDATABASE@$PGHOST:$PGPORT -> $file"
pg_dump --format=custom --no-owner --no-privileges "$PGDATABASE" | gzip > "$file"
echo "Wrote $(du -h "$file" | cut -f1)"

# Prune old dumps, newest KEEP kept.
mapfile -t old < <(ls -1t "$OUT_DIR"/${PGDATABASE}-*.dump.gz 2>/dev/null | tail -n +$((KEEP + 1)))
if [[ ${#old[@]} -gt 0 ]]; then
  printf 'Pruning %s old dump(s)\n' "${#old[@]}"
  rm -f "${old[@]}"
fi
