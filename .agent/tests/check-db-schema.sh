#!/usr/bin/env bash
# check-db-schema.sh — executes DB schema scripts and validates required seed rows.
# Exits non-zero on any failure.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

require_env() {
  local var_name="$1"
  if [ -z "${!var_name:-}" ]; then
    fail "Required environment variable is not set: $var_name"
  else
    echo "OK  [env]  $var_name"
  fi
}

# Required connection variables
require_env POSTGRES_HOST
require_env POSTGRES_PORT
require_env POSTGRES_DB
require_env POSTGRES_USER
require_env POSTGRES_PASSWORD

if [ "$FAILURES" -ne 0 ]; then
  echo "Aborting due to missing environment variables." >&2
  exit 1
fi

if ! command -v psql >/dev/null 2>&1; then
  echo "FAIL: psql command not found. Install PostgreSQL client tools." >&2
  exit 1
fi

export PGPASSWORD="$POSTGRES_PASSWORD"
PSQL_BASE=(
  psql
  --host "$POSTGRES_HOST"
  --port "$POSTGRES_PORT"
  --username "$POSTGRES_USER"
  --dbname "$POSTGRES_DB"
  --set ON_ERROR_STOP=1
  --no-psqlrc
)

run_sql_file() {
  local sql_file="$1"
  local full_path="$REPO_ROOT/$sql_file"

  if [ ! -f "$full_path" ]; then
    fail "SQL file missing: $sql_file"
    return
  fi

  echo "Running SQL: $sql_file"
  if "${PSQL_BASE[@]}" --file "$full_path" >/dev/null; then
    echo "OK  [sql]  $sql_file"
  else
    fail "Failed executing SQL file: $sql_file"
  fi
}

query_equals_one() {
  local label="$1"
  local sql="$2"

  local result
  if ! result=$("${PSQL_BASE[@]}" --tuples-only --no-align --command "$sql" 2>/dev/null); then
    fail "Query failed: $label"
    return
  fi

  result="$(echo "$result" | tr -d '[:space:]')"
  if [ "$result" = "1" ]; then
    echo "OK  [data] $label"
  else
    fail "$label (expected count=1, got: ${result:-empty})"
  fi
}

echo "=== Executing schema SQL files ==="
run_sql_file "db/schema.sql"
run_sql_file "db/topology_tables.sql"
run_sql_file "db/promotion_tables.sql"
run_sql_file "db/seed_empty.sql"

echo "=== Validating required default rows ==="
query_equals_one "structure_maps contains attractor_key='default:entity:search'" \
  "SELECT COUNT(*) FROM structure_maps WHERE attractor_key = 'default:entity:search';"
query_equals_one "package_registry contains name='default_package'" \
  "SELECT COUNT(*) FROM package_registry WHERE name = 'default_package';"
query_equals_one "schema_registry contains name='default_schema'" \
  "SELECT COUNT(*) FROM schema_registry WHERE name = 'default_schema';"

if [ "$FAILURES" -eq 0 ]; then
  echo "=== DB schema check passed ==="
  exit 0
else
  echo "=== DB schema check failed: $FAILURES failure(s) ===" >&2
  exit 1
fi
