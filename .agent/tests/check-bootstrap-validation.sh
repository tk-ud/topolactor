#!/usr/bin/env bash
# check-bootstrap-validation.sh — postgres bootstrap SQL validation (parse-independent, non-compose execution)
# Executes db/init.sql-derived SQL against a provided Postgres service with ON_ERROR_STOP=1. This script does not run docker compose.

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

echo "=== Verify bootstrap inputs (no docker compose execution) ==="
test -f db/init.sql
require_tool mktemp
require_tool sed

tmp_init="$(mktemp)"
cleanup() {
  rm -f "${tmp_init}"
}
trap cleanup EXIT

echo "=== Prepare temporary host-path init from db/init.sql ==="
sed 's#/db/#db/#g' db/init.sql > "${tmp_init}"

echo "=== Apply db/init.sql-derived chain to fresh database ==="
"${PSQL[@]}" -f "${tmp_init}" >/dev/null

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
