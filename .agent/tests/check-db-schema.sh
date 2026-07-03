#!/usr/bin/env bash
# check-db-schema.sh — executes DB schema scripts and validates required seed rows.
# Exits non-zero on any failure.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0
PASS_COUNT=0

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

require_env() {
  local var_name="$1"
  if [ -z "${!var_name:-}" ]; then
    fail "Required environment variable is not set: $var_name"
  else
    PASS_COUNT=$((PASS_COUNT + 1)) # OK
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

NOISE_LOG="$(mktemp)"
exec 3>&1 4>&2 >"$NOISE_LOG" 2>&1
noise_finish() {
  local code=$?
  exec 1>&3 2>&4
  if [ "$code" -eq 0 ]; then
    rm -f "$NOISE_LOG"
    echo "PASS check-db-schema"
  else
    echo "FAIL check-db-schema exit=$code" >&2
    cat "$NOISE_LOG" >&2 || true
    rm -f "$NOISE_LOG"
  fi
  return "$code"
}
trap noise_finish EXIT

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
  if "${PSQL_BASE[@]}" --file "$full_path" ; then
    PASS_COUNT=$((PASS_COUNT + 1)) # OK
  else
    fail "Failed executing SQL file: $sql_file"
  fi
}

query_equals_zero() {
  local label="$1"
  local sql="$2"

  local result
  if ! result=$("${PSQL_BASE[@]}" --tuples-only --no-align --command "$sql"); then
    fail "Query failed: $label"
    return
  fi

  result="$(echo "$result" | tr -d '[:space:]')"
  if [ "$result" = "0" ]; then
    PASS_COUNT=$((PASS_COUNT + 1)) # OK
  else
    fail "$label (expected count=0, got: ${result:-empty})"
  fi
}

query_equals_one() {
  local label="$1"
  local sql="$2"

  local result
  if ! result=$("${PSQL_BASE[@]}" --tuples-only --no-align --command "$sql"); then
    fail "Query failed: $label"
    return
  fi

  result="$(echo "$result" | tr -d '[:space:]')"
  if [ "$result" = "1" ]; then
    PASS_COUNT=$((PASS_COUNT + 1)) # OK
  else
    fail "$label (expected count=1, got: ${result:-empty})"
  fi
}

echo "=== Validating topology_tables.sql bootstrap safety ==="
if rg -n "DROP TABLE IF EXISTS.*CASCADE" "$REPO_ROOT/db/topology_tables.sql" ; then
  fail "db/topology_tables.sql must not contain destructive DROP TABLE ... CASCADE"
else
  PASS_COUNT=$((PASS_COUNT + 1)) # OK
fi

HUB_REL_MIGRATION="$REPO_ROOT/db/legacy_utils/hub_relations_legacy_to_manifest_scoped.sql"
if [ ! -f "$HUB_REL_MIGRATION" ]; then
  fail "hub_relations legacy migration SQL missing: db/legacy_utils/hub_relations_legacy_to_manifest_scoped.sql"
else
  PASS_COUNT=$((PASS_COUNT + 1)) # OK
fi
if rg -n "DROP TABLE IF EXISTS.*CASCADE|DROP TABLE .* CASCADE" "$HUB_REL_MIGRATION" ; then
  fail "hub_relations migration must not use DROP TABLE ... CASCADE"
else
  PASS_COUNT=$((PASS_COUNT + 1)) # OK
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

echo "=== Validating hub_relations legacy migration idempotency ==="
run_sql_file "db/legacy_utils/hub_relations_legacy_to_manifest_scoped.sql"
query_equals_one "hub_relations legacy shape detection function present" \
  "SELECT COUNT(*) FROM pg_proc p JOIN pg_namespace n ON p.pronamespace = n.oid WHERE n.nspname = 'hubs' AND p.proname = 'hub_relations_has_legacy_shape';"
query_equals_one "hub_relations canonical shape after idempotent migration pass" \
  "SELECT CASE WHEN hubs.hub_relations_has_canonical_shape() AND hubs.hub_relations_legacy_columns_absent() THEN 1 ELSE 0 END;"

echo "=== Simulating legacy hub_relations shape and re-migrating (transaction rollback) ==="
LEGACY_MIGRATION_SIM_SQL="$(mktemp)"
cat > "$LEGACY_MIGRATION_SIM_SQL" <<EOF
BEGIN;

ALTER TABLE hubs.hub_relations RENAME TO hub_relations_canonical_sim_backup;

ALTER TABLE hubs.hub_relations_canonical_sim_backup
    DROP CONSTRAINT IF EXISTS hub_relations_topology_manifest_id_sequence_position_key;
ALTER TABLE hubs.hub_relations_canonical_sim_backup
    DROP CONSTRAINT IF EXISTS hub_relations_topology_manifest_id_fkey;
ALTER TABLE hubs.hub_relations_canonical_sim_backup
    DROP CONSTRAINT IF EXISTS hub_relations_related_hub_id_fkey;

CREATE TABLE hubs.hub_relations (
    hub_relation_id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id                UUID NOT NULL REFERENCES hubs.hub (hub_id) ON DELETE CASCADE,
    target_hub_id         UUID NOT NULL REFERENCES hubs.hub (hub_id) ON DELETE CASCADE,
    relation_registry_id  UUID,
    sequence_position     INTEGER NOT NULL DEFAULT 0,
    status                TEXT NOT NULL DEFAULT 'active'
                          CHECK (status IN ('active', 'deprecated')),
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO hubs.hub_relations (hub_id, target_hub_id, relation_registry_id, sequence_position, status)
SELECT
    tm.hub_id,
    hr.related_hub_id,
    NULLIF(hr.relation_config -> 'legacy' ->> 'relation_registry_id', '')::uuid,
    hr.sequence_position,
    hr.status
FROM hubs.hub_relations_canonical_sim_backup hr
JOIN hubs.topology_manifests tm ON tm.topology_manifest_id = hr.topology_manifest_id;

DO \$\$
BEGIN
    IF NOT hubs.hub_relations_has_legacy_shape() THEN
        RAISE EXCEPTION 'legacy simulation failed: legacy shape not detected';
    END IF;
END;
\$\$;

\\i $REPO_ROOT/db/legacy_utils/hub_relations_legacy_to_manifest_scoped.sql

DO \$\$
BEGIN
    IF NOT (hubs.hub_relations_has_canonical_shape() AND hubs.hub_relations_legacy_columns_absent()) THEN
        RAISE EXCEPTION 'legacy simulation failed: canonical shape not restored';
    END IF;
END;
\$\$;

ROLLBACK;
EOF

if "${PSQL_BASE[@]}" --file "$LEGACY_MIGRATION_SIM_SQL" ; then
  PASS_COUNT=$((PASS_COUNT + 1)) # OK
else
  fail "hub_relations legacy migration simulation failed"
fi
rm -f "$LEGACY_MIGRATION_SIM_SQL"

echo "=== Validating table existence ==="
query_equals_one "table exists: structure_maps" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'structure_maps' AND table_schema = 'topology';"
query_equals_one "table exists: topology.components_bucket" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'components_bucket' AND table_schema = 'topology';"
query_equals_one "table exists: topology.ui_topology_tensor" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'ui_topology_tensor' AND table_schema = 'topology';"
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
query_equals_one "table exists: topology.ui_builder_components" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'ui_builder_components' AND table_schema = 'topology';"
query_equals_one "table exists: topology.components_style_design" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'components_style_design' AND table_schema = 'topology';"
query_equals_one "table exists: topology.components_package_design" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'components_package_design' AND table_schema = 'topology';"

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
query_equals_one "structure_maps contains attractor_key='admin:ui_topology:layout_candidates'" \
  "SELECT COUNT(*) FROM topology.structure_maps WHERE attractor_key = 'admin:ui_topology:layout_candidates';"
query_equals_one "structure_maps contains attractor_key='admin:ui_topology:promoted_palette'" \
  "SELECT COUNT(*) FROM topology.structure_maps WHERE attractor_key = 'admin:ui_topology:promoted_palette';"
query_equals_one "structure_maps contains attractor_key='admin:package_generator:promote'" \
  "SELECT COUNT(*) FROM topology.structure_maps WHERE attractor_key = 'admin:package_generator:promote';"
query_equals_one "structure_maps contains attractor_key='admin:package_generator:promote_package'" \
  "SELECT COUNT(*) FROM topology.structure_maps WHERE attractor_key = 'admin:package_generator:promote_package';"
query_equals_one "manifest dispatcher_mapping for package_generator promote_package" \
  "SELECT COUNT(*) FROM manifest m, unnest(m.topology) e WHERE m.status='active' AND e->>'type'='dispatcher_mapping' AND e->>'role'='admin' AND e->>'target'='admin' AND e->>'layer'='package_generator' AND e->>'action'='promote_package';"
query_equals_one "manifest dispatcher_mapping for ui_topology layout_candidates" \
  "SELECT COUNT(*) FROM manifest m, unnest(m.topology) e WHERE m.status='active' AND e->>'type'='dispatcher_mapping' AND e->>'role'='admin' AND e->>'target'='admin' AND e->>'layer'='ui_topology' AND e->>'action'='layout_candidates';"
query_equals_one "manifest dispatcher_mapping for ui_topology promoted_palette" \
  "SELECT COUNT(*) FROM manifest m, unnest(m.topology) e WHERE m.status='active' AND e->>'type'='dispatcher_mapping' AND e->>'role'='admin' AND e->>'target'='admin' AND e->>'layer'='ui_topology' AND e->>'action'='promoted_palette';"

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
  echo "PASS check-db-schema.sh assertions=${PASS_COUNT}"
  exit 0
else
  echo "=== DB schema check failed: $FAILURES failure(s) ===" >&2
  exit 1
fi
