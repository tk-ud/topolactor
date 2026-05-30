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

query_equals_zero() {
  local label="$1"
  local sql="$2"

  local result
  if ! result=$("${PSQL_BASE[@]}" --tuples-only --no-align --command "$sql" 2>/dev/null); then
    fail "Query failed: $label"
    return
  fi

  result="$(echo "$result" | tr -d '[:space:]')"
  if [ "$result" = "0" ]; then
    echo "OK  [data] $label"
  else
    fail "$label (expected count=0, got: ${result:-empty})"
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

echo "=== Validating topology_tables.sql bootstrap safety ==="
if rg -n "DROP TABLE IF EXISTS.*CASCADE" "$REPO_ROOT/db/topology_tables.sql" >/dev/null; then
  fail "db/topology_tables.sql must not contain destructive DROP TABLE ... CASCADE"
else
  echo "OK  [sql] topology_tables.sql destructive DROP TABLE CASCADE absent"
fi

echo "=== Executing schema SQL files ==="
run_sql_file "db/schema.sql"
run_sql_file "db/topology_tables.sql"
run_sql_file "db/promotion_tables.sql"
run_sql_file "db/context_route_tables.sql"
run_sql_file "db/ui_topology_tables.sql"
run_sql_file "db/manifest_tables.sql"
run_sql_file "db/sql_attention_logs_tables.sql"
run_sql_file "db/seed_empty.sql"

echo "=== Validating table existence ==="
query_equals_one "table exists: structure_maps" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'structure_maps' AND table_schema = 'topology';"
query_equals_one "table exists: ui_component_bucket" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'ui_component_bucket' AND table_schema = 'public';"
query_equals_one "table exists: ui_topology_tensor" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'ui_topology_tensor' AND table_schema = 'public';"
query_equals_one "table exists: package_registry" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'package_registry' AND table_schema = 'topology';"
query_equals_one "table exists: schema_registry" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'schema_registry' AND table_schema = 'topology';"
query_equals_one "table exists: relation_registry" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'relation_registry' AND table_schema = 'topology';"
query_equals_one "table exists: state_registry" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'state_registry' AND table_schema = 'topology';"
query_equals_one "table exists: manifest" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'manifest' AND table_schema = 'public';"
query_equals_one "table exists: context_token_registry" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'context_token_registry' AND table_schema = 'public';"
query_equals_one "table exists: components" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'components' AND table_schema = 'public';"
query_equals_one "table exists: design" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'design' AND table_schema = 'public';"
query_equals_one "table exists: packages" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'packages' AND table_schema = 'public';"

echo "=== Validating required default rows ==="
query_equals_one "structure_maps contains attractor_key='default:entity:search'" \
  "SELECT COUNT(*) FROM topology.structure_maps WHERE attractor_key = 'default:entity:search';"
query_equals_one "package_registry contains name='default_package'" \
  "SELECT COUNT(*) FROM topology.package_registry WHERE name = 'default_package';"
query_equals_one "schema_registry contains name='default_schema'" \
  "SELECT COUNT(*) FROM topology.schema_registry WHERE name = 'default_schema';"

echo "=== Validating admin attractor keys ==="
query_equals_one "structure_maps contains attractor_key='admin:context_token_registry:list'" \
  "SELECT COUNT(*) FROM topology.structure_maps WHERE attractor_key = 'admin:context_token_registry:list';"
query_equals_one "structure_maps contains attractor_key='admin:ui_component_bucket:list'" \
  "SELECT COUNT(*) FROM topology.structure_maps WHERE attractor_key = 'admin:ui_component_bucket:list';"
query_equals_one "structure_maps contains attractor_key='admin:registry_vector:validate'" \
  "SELECT COUNT(*) FROM topology.structure_maps WHERE attractor_key = 'admin:registry_vector:validate';"
query_equals_one "structure_maps contains attractor_key='admin:package_generator:generate'" \
  "SELECT COUNT(*) FROM topology.structure_maps WHERE attractor_key = 'admin:package_generator:generate';"

echo "=== Validating hubs.hub_relations FK chain ==="
query_equals_one "column exists: hubs.hub_relations.topology_manifest_id" \
  "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'hubs' AND table_name = 'hub_relations' AND column_name = 'topology_manifest_id';"
query_equals_one "column exists: hubs.hub_relations.related_hub_id" \
  "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'hubs' AND table_name = 'hub_relations' AND column_name = 'related_hub_id';"
query_equals_zero "column absent: hubs.hub_relations.hub_id" \
  "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'hubs' AND table_name = 'hub_relations' AND column_name = 'hub_id';"
query_equals_zero "column absent: hubs.hub_relations.target_hub_id" \
  "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'hubs' AND table_name = 'hub_relations' AND column_name = 'target_hub_id';"
query_equals_zero "column absent: hubs.hub_relations.relation_registry_id" \
  "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'hubs' AND table_name = 'hub_relations' AND column_name = 'relation_registry_id';"
query_equals_one "unique constraint: hub_relations(topology_manifest_id, sequence_position)" \
  "SELECT COUNT(*) FROM pg_constraint c JOIN pg_class t ON c.conrelid = t.oid JOIN pg_namespace n ON t.relnamespace = n.oid WHERE n.nspname = 'hubs' AND t.relname = 'hub_relations' AND c.contype = 'u' AND pg_get_constraintdef(c.oid) LIKE '%topology_manifest_id%' AND pg_get_constraintdef(c.oid) LIKE '%sequence_position%';"

if [ "$FAILURES" -eq 0 ]; then
  echo "=== DB schema check passed ==="
  exit 0
else
  echo "=== DB schema check failed: $FAILURES failure(s) ===" >&2
  exit 1
fi
