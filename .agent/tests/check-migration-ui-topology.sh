#!/usr/bin/env bash
# check-migration-ui-topology.sh — regression tests for UI topology canonical schema migration.
# Verifies:
#   1. Static SQL structure: remapping and validation logic present in migration DDL.
#   2. (Requires DB) Fresh-DB: topology.* tables created, old public UI tables absent.
#   3. (Requires DB) Idempotency: re-running migration on already-migrated DB is a no-op.
#   4. (Requires DB) Conflict case: child FK remapped to canonical UUID when parent row
#      existed in canonical with same business key but different UUID.
#   5. (Requires DB) Tensor self-reference: logical parent remap survives displaced UUIDs;
#      dangling legacy parent references are rejected explicitly.
#
# DB tests require POSTGRES_HOST/PORT/DB/USER/PASSWORD.  If env vars are absent,
# only static checks run and the script exits with the static result.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

FAILURES=0

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

check_content() {
  local file="$REPO_ROOT/$1"
  local term="$2"
  if [ ! -f "$file" ]; then
    fail "Content check skipped (file missing): $1"
    return
  fi
  if grep -qF -- "$term" "$file"; then
    echo "OK  [term] $1 → \"$term\""
  else
    fail "Term not found in $1: \"$term\""
  fi
}

# ─── Static SQL structure checks ─────────────────────────────────────────────

echo ""
echo "=== Static structure checks: ui_topology_to_canonical_schema.sql ==="
MIGRATION="db/migrations/ui_topology_to_canonical_schema.sql"

check_content "$MIGRATION" "v_bad_refs"
check_content "$MIGRATION" "RAISE EXCEPTION"
check_content "$MIGRATION" "package_key = pp.package_key"
check_content "$MIGRATION" "component_key = pr.component_key"
check_content "$MIGRATION" "layout_key = pl.layout_key"
check_content "$MIGRATION" "wiring_key = pw.wiring_key"
check_content "$MIGRATION" "IS NOT DISTINCT FROM"
check_content "$MIGRATION" "parent_tensor_id = NULL"
check_content "$MIGRATION" "Pass 1"
check_content "$MIGRATION" "Pass 2"
check_content "$MIGRATION" "Explicit dangling-reference policy"
check_content "$MIGRATION" "Repair the legacy tensor hierarchy before retrying."
check_content "$MIGRATION" "parent_canonical.route_key   = parent_pub.route_key"
check_content "$MIGRATION" "child_canonical.route_key     = child_pub.route_key"
check_content "$MIGRATION" "logical parent_tensor_id remap"
check_content "$MIGRATION" "DROP TABLE IF EXISTS public.ui_topology_tensor CASCADE"
check_content "$MIGRATION" "DROP TABLE IF EXISTS public.ui_package_component_map CASCADE"
check_content "$MIGRATION" "DROP TABLE IF EXISTS public.ui_component_package CASCADE"
check_content "$MIGRATION" "DROP TABLE IF EXISTS public.ui_component_registry CASCADE"

echo ""
echo "=== Static structure checks: admin_import_topology_manifest_migration.sql ==="
ADMIN_MIGRATION="db/migrations/admin_import_topology_manifest_migration.sql"

check_content "$ADMIN_MIGRATION" "orphaned_count"
check_content "$ADMIN_MIGRATION" "default_manifest_id"
check_content "$ADMIN_MIGRATION" "RAISE EXCEPTION"
check_content "$ADMIN_MIGRATION" "hubs.topology_manifests"
check_content "$ADMIN_MIGRATION" "topology_manifest_id_fkey"

echo ""
echo "=== Static structure checks: manifest_tables.sql ==="
check_content "db/manifest_tables.sql" "admin_import FK retirement"
check_content "db/manifest_tables.sql" "admin_import_topology_manifest_migration.sql"

# ─── DB-backed tests ──────────────────────────────────────────────────────────

DB_VARS_PRESENT=true
for v in POSTGRES_HOST POSTGRES_PORT POSTGRES_DB POSTGRES_USER POSTGRES_PASSWORD; do
  if [ -z "${!v:-}" ]; then
    DB_VARS_PRESENT=false
    break
  fi
done

if [ "$DB_VARS_PRESENT" = "false" ]; then
  echo ""
  echo "=== DB env vars absent — skipping live DB regression tests ==="
  if [ "$FAILURES" -eq 0 ]; then
    echo "=== Static migration checks passed ==="
    exit 0
  else
    echo "=== Static migration checks failed: $FAILURES failure(s) ===" >&2
    exit 1
  fi
fi

if ! command -v psql >/dev/null 2>&1; then
  fail "psql command not found; cannot run DB regression tests"
  echo "=== Migration check failed: $FAILURES failure(s) ===" >&2
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
    fail "$label (expected 0, got: ${result:-empty})"
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
    fail "$label (expected 1, got: ${result:-empty})"
  fi
}

run_sql_file() {
  local sql_file="$1"
  local full_path="$REPO_ROOT/$sql_file"
  if [ ! -f "$full_path" ]; then
    fail "SQL file missing: $sql_file"
    return
  fi
  if "${PSQL_BASE[@]}" --file "$full_path" >/dev/null; then
    echo "OK  [sql]  $sql_file"
  else
    fail "Failed executing SQL file: $sql_file"
  fi
}

echo ""
echo "=== DB: topology.* tables present after fresh schema bootstrap ==="
query_equals_one "topology.components_bucket exists" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='topology' AND table_name='components_bucket';"
query_equals_one "topology.ui_component_registry exists" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='topology' AND table_name='ui_component_registry';"
query_equals_one "topology.ui_component_package exists" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='topology' AND table_name='ui_component_package';"
query_equals_one "topology.ui_package_component_map exists" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='topology' AND table_name='ui_package_component_map';"
query_equals_one "topology.components_layout_design exists" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='topology' AND table_name='components_layout_design';"
query_equals_one "topology.ui_wiring_registry exists" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='topology' AND table_name='ui_wiring_registry';"
query_equals_one "topology.ui_topology_tensor exists" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='topology' AND table_name='ui_topology_tensor';"

echo ""
echo "=== DB: old public UI tables absent on fresh DB ==="
query_equals_zero "public.ui_component_bucket absent" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='ui_component_bucket';"
query_equals_zero "public.ui_topology_tensor absent" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='ui_topology_tensor';"
query_equals_zero "public.ui_package_component_map absent" \
  "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='ui_package_component_map';"

echo ""
echo "=== DB: migration idempotency (re-run is a no-op) ==="
run_sql_file "db/migrations/ui_topology_to_canonical_schema.sql"
echo "OK  [idempotent] ui_topology_to_canonical_schema.sql re-run succeeded"

echo ""
echo "=== DB: conflict-case simulation (child FK remapped to canonical UUID) ==="
CONFLICT_SIM_SQL="$(mktemp)"
trap 'rm -f "$CONFLICT_SIM_SQL"' EXIT

cat > "$CONFLICT_SIM_SQL" <<'ENDSQL'
BEGIN;

-- Seed a canonical package and component with known UUIDs.
INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, status, package_schema_json)
VALUES ('aaaaaaaa-0000-0000-0000-000000000001', 'sim_pkg', 'ui', 'active', '{}')
ON CONFLICT (package_key) DO NOTHING;

INSERT INTO topology.ui_component_registry (component_id, component_key, component_kind, source_path, props_schema_json, status)
VALUES ('bbbbbbbb-0000-0000-0000-000000000001', 'sim_comp', 'ui', '/sim', '{}', 'active')
ON CONFLICT (component_key) DO NOTHING;

-- Resolve canonical UUIDs (may differ from seeded values if conflict fired).
DO $$
DECLARE
  canon_pkg_id  uuid;
  canon_comp_id uuid;
  old_pkg_id    uuid := gen_random_uuid();
  old_comp_id   uuid := gen_random_uuid();
  old_map_id    uuid := gen_random_uuid();
BEGIN
  SELECT package_id   INTO canon_pkg_id  FROM topology.ui_component_package  WHERE package_key   = 'sim_pkg';
  SELECT component_id INTO canon_comp_id FROM topology.ui_component_registry  WHERE component_key = 'sim_comp';

  -- Create synthetic old public tables for the simulation.
  CREATE TEMP TABLE sim_old_pkg (package_id uuid, package_key text, package_kind text, status text, package_schema_json jsonb,
    created_at timestamptz DEFAULT now(), updated_at timestamptz DEFAULT now()) ON COMMIT DROP;
  CREATE TEMP TABLE sim_old_reg (component_id uuid, component_key text, component_kind text, source_path text,
    props_schema_json jsonb, status text, created_at timestamptz DEFAULT now(), updated_at timestamptz DEFAULT now()) ON COMMIT DROP;
  CREATE TEMP TABLE sim_old_map (map_id uuid, package_id uuid, component_id uuid, slot_key text,
    order_index int, props_override_json jsonb, created_at timestamptz DEFAULT now()) ON COMMIT DROP;

  -- Old public rows: same business key but DIFFERENT UUIDs from canonical.
  INSERT INTO sim_old_pkg VALUES (old_pkg_id,  'sim_pkg',  'ui', 'active', '{}');
  INSERT INTO sim_old_reg VALUES (old_comp_id, 'sim_comp', 'ui', '/sim',   '{}', 'active');
  INSERT INTO sim_old_map VALUES (old_map_id,  old_pkg_id, old_comp_id, NULL, 1, '{}');

  -- Simulate the remapping logic: resolve via business key and insert into canonical map.
  INSERT INTO topology.ui_package_component_map (map_id, package_id, component_id, slot_key, order_index, props_override_json, created_at)
  SELECT
    m.map_id,
    cp.package_id,
    cr.component_id,
    m.slot_key,
    m.order_index,
    m.props_override_json,
    m.created_at
  FROM sim_old_map m
  JOIN sim_old_pkg pp ON pp.package_id = m.package_id
  JOIN topology.ui_component_package cp ON cp.package_key = pp.package_key
  JOIN sim_old_reg pr ON pr.component_id = m.component_id
  JOIN topology.ui_component_registry cr ON cr.component_key = pr.component_key
  ON CONFLICT (map_id) DO NOTHING;

  -- Verify: inserted row uses canonical UUIDs, not old public UUIDs.
  IF NOT EXISTS (
    SELECT 1 FROM topology.ui_package_component_map
    WHERE map_id = old_map_id
      AND package_id   = canon_pkg_id
      AND component_id = canon_comp_id
  ) THEN
    RAISE EXCEPTION 'conflict-case simulation FAILED: child row not remapped to canonical UUIDs';
  END IF;

  IF EXISTS (
    SELECT 1 FROM topology.ui_package_component_map
    WHERE map_id = old_map_id
      AND (package_id = old_pkg_id OR component_id = old_comp_id)
  ) THEN
    RAISE EXCEPTION 'conflict-case simulation FAILED: child row still references old public UUIDs';
  END IF;

  RAISE NOTICE 'conflict-case simulation PASSED: child FK remapped to canonical UUID correctly';
END;
$$;

-- Tensor parent self-reference regression: canonical parent and child tensors already
-- exist under displaced UUIDs, while old child points to the old public parent UUID.
INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind)
VALUES ('cccccccc-0000-0000-0000-000000000001', 'sim_layout', 'stack')
ON CONFLICT (layout_key) DO NOTHING;

INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface)
VALUES ('dddddddd-0000-0000-0000-000000000001', 'sim_wiring', 'bind', 'sim')
ON CONFLICT (wiring_key) DO NOTHING;

DO $$
DECLARE
  canon_pkg_id            uuid;
  canon_layout_id         uuid;
  canon_wiring_id         uuid;
  canon_parent_tensor_id  uuid := gen_random_uuid();
  canon_child_tensor_id   uuid := gen_random_uuid();
  old_pkg_id              uuid := gen_random_uuid();
  old_layout_id           uuid := gen_random_uuid();
  old_wiring_id           uuid := gen_random_uuid();
  old_parent_tensor_id    uuid := gen_random_uuid();
  old_child_tensor_id     uuid := gen_random_uuid();
  dangling_tensor_id      uuid := gen_random_uuid();
  dangling_parent_id      uuid := gen_random_uuid();
  dangling_rejected       boolean := false;
  bad_refs                integer;
BEGIN
  SELECT package_id INTO canon_pkg_id FROM topology.ui_component_package WHERE package_key = 'sim_pkg';
  SELECT layout_id  INTO canon_layout_id FROM topology.components_layout_design WHERE layout_key = 'sim_layout';
  SELECT wiring_id  INTO canon_wiring_id FROM topology.ui_wiring_registry WHERE wiring_key = 'sim_wiring';

  INSERT INTO topology.ui_topology_tensor
    (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index)
  VALUES
    (canon_parent_tensor_id, 'sim_parent_route', canon_pkg_id, canon_layout_id, canon_wiring_id, 'parent', 1),
    (canon_child_tensor_id,  'sim_child_route',  canon_pkg_id, canon_layout_id, canon_wiring_id, 'child',  2);

  CREATE TEMP TABLE sim_old_layout (layout_id uuid, layout_key text) ON COMMIT DROP;
  CREATE TEMP TABLE sim_old_wiring (wiring_id uuid, wiring_key text) ON COMMIT DROP;
  CREATE TEMP TABLE sim_old_tensor (
    tensor_id uuid, route_key text, package_id uuid, layout_id uuid, wiring_id uuid,
    parent_tensor_id uuid, slot_key text, order_index int
  ) ON COMMIT DROP;

  INSERT INTO sim_old_pkg VALUES (old_pkg_id, 'sim_pkg', 'ui', 'active', '{}');
  INSERT INTO sim_old_layout VALUES (old_layout_id, 'sim_layout');
  INSERT INTO sim_old_wiring VALUES (old_wiring_id, 'sim_wiring');
  INSERT INTO sim_old_tensor VALUES
    (old_parent_tensor_id, 'sim_parent_route', old_pkg_id, old_layout_id, old_wiring_id, NULL,                 'parent', 1),
    (old_child_tensor_id,  'sim_child_route',  old_pkg_id, old_layout_id, old_wiring_id, old_parent_tensor_id, 'child',  2);

  -- Mirror migration Pass 2: resolve the legacy parent through its remapped logical
  -- tuple and target the canonical child through its own logical tuple.
  UPDATE topology.ui_topology_tensor child_canonical
  SET parent_tensor_id = parent_canonical.tensor_id
  FROM sim_old_tensor child_pub
  JOIN sim_old_pkg child_pp ON child_pp.package_id = child_pub.package_id
  JOIN topology.ui_component_package child_cp ON child_cp.package_key = child_pp.package_key
  JOIN sim_old_layout child_pl ON child_pl.layout_id = child_pub.layout_id
  JOIN topology.components_layout_design child_cl ON child_cl.layout_key = child_pl.layout_key
  JOIN sim_old_wiring child_pw ON child_pw.wiring_id = child_pub.wiring_id
  JOIN topology.ui_wiring_registry child_cw ON child_cw.wiring_key = child_pw.wiring_key
  JOIN sim_old_tensor parent_pub ON parent_pub.tensor_id = child_pub.parent_tensor_id
  JOIN sim_old_pkg parent_pp ON parent_pp.package_id = parent_pub.package_id
  JOIN topology.ui_component_package parent_cp ON parent_cp.package_key = parent_pp.package_key
  JOIN sim_old_layout parent_pl ON parent_pl.layout_id = parent_pub.layout_id
  JOIN topology.components_layout_design parent_cl ON parent_cl.layout_key = parent_pl.layout_key
  JOIN sim_old_wiring parent_pw ON parent_pw.wiring_id = parent_pub.wiring_id
  JOIN topology.ui_wiring_registry parent_cw ON parent_cw.wiring_key = parent_pw.wiring_key
  JOIN topology.ui_topology_tensor parent_canonical
    ON parent_canonical.route_key   = parent_pub.route_key
   AND parent_canonical.package_id  = parent_cp.package_id
   AND parent_canonical.layout_id   = parent_cl.layout_id
   AND parent_canonical.wiring_id   = parent_cw.wiring_id
   AND parent_canonical.slot_key    IS NOT DISTINCT FROM parent_pub.slot_key
   AND parent_canonical.order_index = parent_pub.order_index
  WHERE child_pub.parent_tensor_id    IS NOT NULL
    AND child_canonical.route_key     = child_pub.route_key
    AND child_canonical.package_id    = child_cp.package_id
    AND child_canonical.layout_id     = child_cl.layout_id
    AND child_canonical.wiring_id     = child_cw.wiring_id
    AND child_canonical.slot_key      IS NOT DISTINCT FROM child_pub.slot_key
    AND child_canonical.order_index   = child_pub.order_index
    AND child_canonical.parent_tensor_id IS NULL;

  IF NOT EXISTS (
    SELECT 1 FROM topology.ui_topology_tensor
    WHERE tensor_id = canon_child_tensor_id
      AND parent_tensor_id = canon_parent_tensor_id
  ) THEN
    RAISE EXCEPTION 'tensor parent remap simulation FAILED: child does not reference canonical parent tensor UUID';
  END IF;

  IF EXISTS (
    SELECT 1 FROM topology.ui_topology_tensor
    WHERE tensor_id = canon_child_tensor_id
      AND parent_tensor_id = old_parent_tensor_id
  ) THEN
    RAISE EXCEPTION 'tensor parent remap simulation FAILED: child still references old public parent tensor UUID';
  END IF;

  -- Fix the explicit dangling-reference policy: a non-NULL ref with no old parent
  -- row must be rejected rather than silently retained as NULL.
  INSERT INTO sim_old_tensor VALUES
    (dangling_tensor_id, 'sim_dangling_route', old_pkg_id, old_layout_id, old_wiring_id, dangling_parent_id, 'dangling', 3);
  BEGIN
    SELECT COUNT(*) INTO bad_refs
    FROM sim_old_tensor child_pub
    WHERE child_pub.parent_tensor_id IS NOT NULL
      AND NOT EXISTS (
        SELECT 1 FROM sim_old_tensor parent_pub
        WHERE parent_pub.tensor_id = child_pub.parent_tensor_id
      );
    IF bad_refs > 0 THEN
      RAISE EXCEPTION 'expected dangling legacy parent rejection';
    END IF;
  EXCEPTION WHEN OTHERS THEN
    dangling_rejected := true;
  END;

  IF NOT dangling_rejected THEN
    RAISE EXCEPTION 'tensor dangling parent simulation FAILED: missing old parent row was not rejected';
  END IF;

  RAISE NOTICE 'tensor parent remap simulation PASSED: logical parent resolved and dangling ref rejected';
END;
$$;

ROLLBACK;
ENDSQL

if "${PSQL_BASE[@]}" --file "$CONFLICT_SIM_SQL" >/dev/null; then
  echo "OK  [sim] conflict-case FK and tensor parent remapping simulation passed (rolled back)"
else
  fail "conflict-case FK and tensor parent remapping simulation failed"
fi

if [ "$FAILURES" -eq 0 ]; then
  echo ""
  echo "=== Migration regression checks passed ==="
  exit 0
else
  echo ""
  echo "=== Migration regression checks failed: $FAILURES failure(s) ===" >&2
  exit 1
fi
