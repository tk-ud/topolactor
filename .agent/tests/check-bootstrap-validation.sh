#!/usr/bin/env bash
# check-bootstrap-validation.sh — fresh DB bootstrap validation
# Applies init.sql-equivalent SQL chain to a fresh database with ON_ERROR_STOP=1.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

require_tool() {
  if ! command -v "$1" &>/dev/null; then
    echo "ERROR: required tool not found: $1" >&2
    echo "This check was NOT executed — missing tool is not a pass." >&2
    exit 1
  fi
}

require_env() {
  local name="$1"
  if [ -z "${!name:-}" ]; then
    echo "ERROR: required environment variable is not set: $name" >&2
    echo "This check was NOT executed — missing environment is not a pass." >&2
    exit 1
  fi
}

require_tool psql

require_env POSTGRES_HOST
require_env POSTGRES_PORT
require_env POSTGRES_DB
require_env POSTGRES_USER
require_env POSTGRES_PASSWORD

cd "${REPO_ROOT}"

export PGPASSWORD="${POSTGRES_PASSWORD}"
PSQL=(psql -v ON_ERROR_STOP=1 -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}")

SQL_CHAIN=(
  "db/schema.sql"
  "db/topology_tables.sql"
  "db/promotion_tables.sql"
  "db/context_route_tables.sql"
  "db/ui_topology_tables.sql"
  "db/manifest_tables.sql"
  "db/seed_empty.sql"
  "db/demo_seed.sql"
)

echo "=== Verify bootstrap inputs ==="
test -f db/init.sql
for sql in "${SQL_CHAIN[@]}"; do
  test -f "${sql}"
  echo "OK  [file] ${sql}"
done

echo "=== Apply init.sql-equivalent SQL chain to fresh database ==="
for sql in "${SQL_CHAIN[@]}"; do
  echo "[bootstrap] applying ${sql}"
  "${PSQL[@]}" -f "${sql}" >/dev/null
done

echo "=== Verify required bootstrap tables exist ==="
required_tables=(
  "manifest"
  "ui_component_bucket"
  "ui_topology_tensor"
  "context_event"
  "context_hub_recommendation_current"
)

for table_name in "${required_tables[@]}"; do
  exists="$(${PSQL[@]} -tA -c "SELECT to_regclass('public.${table_name}') IS NOT NULL;")"
  if [ "${exists}" != "t" ]; then
    echo "ERROR: required table missing after bootstrap: ${table_name}" >&2
    exit 1
  fi
  echo "OK  [table] ${table_name}"
done

echo "=== Bootstrap validation checks passed ==="
