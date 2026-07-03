#!/usr/bin/env bash
# check-bootstrap-validation.sh — postgres bootstrap SQL validation (parse-independent, non-compose execution)
# Executes db/init.sql-derived SQL against a provided Postgres service with ON_ERROR_STOP=1. This script does not run docker compose.

set -euo pipefail
PASS_COUNT=0

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

require_tool() {
  if ! command -v "$1" >/dev/null 2>&1; then
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
NOISE_LOG="$(mktemp)"
exec 3>&1 4>&2 >"$NOISE_LOG" 2>&1
noise_finish() {
  local code=$?
  exec 1>&3 2>&4
  if [ "$code" -eq 0 ]; then
    rm -f "$NOISE_LOG"
    echo "PASS check-bootstrap-validation"
  else
    echo "FAIL check-bootstrap-validation exit=$code" >&2
    cat "$NOISE_LOG" >&2 || true
    rm -f "$NOISE_LOG"
  fi
  return "$code"
}

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
finish() {
  local code=$?
  cleanup || true
  (exit "$code"); noise_finish
}
# legacy guard term: trap cleanup EXIT must not be installed separately; finish combines cleanup + noise_finish
trap finish EXIT

echo "=== Prepare temporary host-path init from db/init.sql ==="
sed 's#/db/#db/#g' db/init.sql > "${tmp_init}"

echo "=== Apply db/init.sql-derived chain to fresh database ==="
"${PSQL[@]}" -f "${tmp_init}"

echo "=== Verify required bootstrap tables exist ==="

required_public_tables=(
  "manifest"
  "context_event"
  "context_hub_recommendation_current"
)

for table_name in "${required_public_tables[@]}"; do
  exists="$(${PSQL[@]} -tA -c "SELECT to_regclass('public.${table_name}') IS NOT NULL;")"
  if [ "${exists}" != "t" ]; then
    echo "ERROR: required public table missing after bootstrap: ${table_name}" >&2
    exit 1
  fi
  PASS_COUNT=$((PASS_COUNT + 1)) # OK
done

required_enum_tables=(
  "items"
  "groups"
  "group_items"
)

for table_name in "${required_enum_tables[@]}"; do
  exists="$(${PSQL[@]} -tA -c "SELECT to_regclass('enum.${table_name}') IS NOT NULL;")"
  if [ "${exists}" != "t" ]; then
    echo "ERROR: required enum table missing after bootstrap: enum.${table_name}" >&2
    exit 1
  fi
  PASS_COUNT=$((PASS_COUNT + 1)) # OK
done

required_topology_tables=(
  "components_bucket"
  "ui_topology_tensor"
)

for table_name in "${required_topology_tables[@]}"; do
  exists="$(${PSQL[@]} -tA -c "SELECT to_regclass('topology.${table_name}') IS NOT NULL;")"
  if [ "${exists}" != "t" ]; then
    echo "ERROR: required topology table missing after bootstrap: ${table_name}" >&2
    exit 1
  fi
  PASS_COUNT=$((PASS_COUNT + 1)) # OK
done

echo "PASS check-bootstrap-validation.sh assertions=${PASS_COUNT}"
