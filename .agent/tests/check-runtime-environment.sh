#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
COMPOSE_FILE="${REPO_ROOT}/infra/docker-compose.yml"

require_tool() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "ERROR: required tool not found: $1" >&2
    exit 1
  fi
}

require_tool docker
require_tool jq
require_tool curl
require_tool dotnet
require_tool psql

assert_relation_exists() {
  local relation="$1"
  local exists
  exists="$(docker exec topolactor-demo-postgres psql -U topolactor_demo -d topolactor_demo -tA -c "SELECT to_regclass('${relation}') IS NOT NULL;")"
  if [ "${exists}" != "t" ]; then
    echo "ERROR: required relation is missing: ${relation}" >&2
    docker exec topolactor-demo-postgres psql -U topolactor_demo -d topolactor_demo -c "\dt public.*" || true
    exit 1
  fi
  echo "OK: relation exists -> ${relation}"
}

cleanup() {
  compose down -v --remove-orphans || true
  rm -f "${RUNTIME_ENV_FILE}" || true
}
trap cleanup EXIT

cd "${REPO_ROOT}"

RUNTIME_ENV_FILE="$(mktemp)"
cat > "${RUNTIME_ENV_FILE}" <<EOF
DATABASE_URL=Host=postgres;Port=5432;Database=topolactor_demo;Username=topolactor_demo;Password=topolactor_demo
DEMO_JWT_SECRET=topolactor-demo-secret
DEMO_JWT_EXPIRY_HOURS=12
DEMO_JWT_ISSUER=topolactor-demo
EOF

compose() {
  docker compose --env-file "${RUNTIME_ENV_FILE}" -f "${COMPOSE_FILE}" "$@"
}

dump_logs() {
  echo "=== [RUNTIME_ENV] docker compose logs: backend ===" >&2
  compose logs backend >&2 || true
  echo "=== [RUNTIME_ENV] docker compose logs: postgres ===" >&2
  compose logs postgres >&2 || true
}

echo "=== [RUNTIME_ENV] Start postgres/backend via docker compose ==="
compose up -d postgres backend

echo "=== [RUNTIME_ENV] Wait for postgres health ==="
for _ in $(seq 1 30); do
  status="$(docker inspect --format='{{.State.Health.Status}}' topolactor-demo-postgres 2>/dev/null || true)"
  if [ "${status}" = "healthy" ]; then
    echo "Postgres is healthy"
    break
  fi
  sleep 2
done

status="$(docker inspect --format='{{.State.Health.Status}}' topolactor-demo-postgres 2>/dev/null || true)"
if [ "${status}" != "healthy" ]; then
  echo "ERROR: postgres health check failed (status=${status})" >&2
    dump_logs
    exit 1
fi

echo "=== [RUNTIME_ENV] Verify DB connectivity and required schema relations ==="
docker exec topolactor-demo-postgres psql -U topolactor_demo -d topolactor_demo -c "SELECT 1;"
assert_relation_exists "public.manifest"
assert_relation_exists "topology.topology_edit_log"

echo "=== [RUNTIME_ENV] Run UI topology migration regression against live DB ==="
POSTGRES_HOST=127.0.0.1 \
POSTGRES_PORT=5432 \
POSTGRES_DB=topolactor_demo \
POSTGRES_USER=topolactor_demo \
POSTGRES_PASSWORD=topolactor_demo \
  bash .agent/tests/check-migration-ui-topology.sh

echo "=== [RUNTIME_ENV] Run integration tests against live DB ==="
DATABASE_URL='Host=127.0.0.1;Port=5432;Database=topolactor_demo;Username=topolactor_demo;Password=topolactor_demo' \
  dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj \
  --nologo --verbosity minimal

echo "=== [RUNTIME_ENV] Wait for backend health ==="
for _ in $(seq 1 40); do
  status="$(docker inspect --format='{{.State.Health.Status}}' topolactor-demo-backend 2>/dev/null || true)"
  if [ "${status}" = "healthy" ]; then
    echo "Backend is healthy"
    break
  fi
  sleep 2
done

status="$(docker inspect --format='{{.State.Health.Status}}' topolactor-demo-backend 2>/dev/null || true)"
if [ "${status}" != "healthy" ]; then
  echo "ERROR: backend health check failed (status=${status})" >&2
  dump_logs
  exit 1
fi

echo "=== [RUNTIME_ENV] Verify seed storage volume read/write ==="
docker exec topolactor-demo-backend sh -lc "echo runtime-seed-check > /storage/runtime-seed-check.txt"
seed_value="$(docker exec topolactor-demo-backend sh -lc "cat /storage/runtime-seed-check.txt")"
if [ "${seed_value}" != "runtime-seed-check" ]; then
  echo "ERROR: seed storage volume verification failed" >&2
  dump_logs
  exit 1
fi

BACKEND_BASE_URL="$(compose port backend 5000 | head -n1)"
if [ -z "${BACKEND_BASE_URL}" ]; then
  echo "ERROR: backend published port is unavailable" >&2
  dump_logs
  exit 1
fi
BACKEND_URL="http://${BACKEND_BASE_URL}"

echo "=== [RUNTIME_ENV] Live API E2E /auth/login -> /dispatch ==="
login_response="$(cat <<'JSON' | curl -sS -X POST "${BACKEND_URL}/auth/login" -H "Content-Type: application/json" --data-binary @-
{"username":"demo_admin","password":"demo_admin_password"}
JSON
)"

token="$(printf '%s' "${login_response}" | jq -r '.token // empty')"
if [ -z "${token}" ] || [ "${token}" = "null" ]; then
  echo "ERROR: login failed; response body:" >&2
  echo "${login_response}" >&2
  dump_logs
  exit 1
fi

mapping_json="$(docker exec topolactor-demo-postgres psql -U topolactor_demo -d topolactor_demo -tA -c "
WITH active_manifests AS (
  SELECT manifest_id, topology
  FROM manifest
  WHERE status = 'active'
),
dispatcher AS (
  SELECT
    am.manifest_id,
    e->>'role' AS role,
    e->>'target' AS target,
    e->>'layer' AS layer,
    e->>'action' AS action
  FROM active_manifests am
  CROSS JOIN LATERAL unnest(am.topology) AS e
  WHERE e->>'type' = 'dispatcher_mapping'
    AND COALESCE(e->>'target','') NOT IN ('demo','admin')
)
SELECT json_build_object(
  'role', d.role,
  'target', d.target,
  'layer', d.layer,
  'action', d.action,
  'structureMapId', sm.structure_map_id::text
)::text
FROM dispatcher d
LEFT JOIN topology.structure_maps sm
  ON lower(sm.attractor_key) = lower(concat_ws(':', d.target, d.layer, d.action))
 AND sm.active = true
ORDER BY d.manifest_id
LIMIT 1;
")"

if [ -z "${mapping_json}" ]; then
  echo "ERROR: no non-override active manifest dispatcher_mapping found for live /dispatch E2E" >&2
  dump_logs
  exit 1
fi

dispatch_target="$(printf '%s' "${mapping_json}" | jq -r '.target // empty')"
dispatch_layer="$(printf '%s' "${mapping_json}" | jq -r '.layer // empty')"
dispatch_action="$(printf '%s' "${mapping_json}" | jq -r '.action // empty')"
dispatch_role="$(printf '%s' "${mapping_json}" | jq -r '.role // empty')"
expected_structure_map_id="$(printf '%s' "${mapping_json}" | jq -r '.structureMapId // empty')"

if [ -z "${dispatch_target}" ] || [ -z "${dispatch_layer}" ] || [ -z "${dispatch_action}" ] || [ -z "${expected_structure_map_id}" ]; then
  echo "ERROR: non-override manifest-backed route is incomplete: ${mapping_json}" >&2
  dump_logs
  exit 1
fi

dispatch_response="$(jq -n \
  --arg target "${dispatch_target}" \
  --arg layer "${dispatch_layer}" \
  --arg action "${dispatch_action}" \
  --arg role "${dispatch_role}" \
  '{operationType:"Search",target:$target,layer:$layer,action:$action,idOrHubId:null,payload:null,context:null,role:(if $role=="" then null else $role end)}' | curl -sS -X POST "${BACKEND_URL}/dispatch" \
  -H "Authorization: Bearer ${token}" \
  -H "Content-Type: application/json" \
  --data-binary @-)"

dispatch_success="$(printf '%s' "${dispatch_response}" | jq -r '.success // false')"
dispatch_structure_map_id="$(printf '%s' "${dispatch_response}" | jq -r '.emission.structureMapId // empty')"
if [ "${dispatch_success}" != "true" ]; then
  echo "ERROR: dispatch returned non-success; response body:" >&2
  echo "${dispatch_response}" >&2
  dump_logs
  exit 1
fi

if [ "${dispatch_structure_map_id}" != "${expected_structure_map_id}" ]; then
  echo "ERROR: dispatch structureMapId mismatch (expected=${expected_structure_map_id}, actual=${dispatch_structure_map_id}); response body:" >&2
  echo "${dispatch_response}" >&2
  dump_logs
  exit 1
fi

echo "=== [RUNTIME_ENV] Live verification: AdminRuntime.ExecuteDataAsync (admin route) ==="
admin_response="$(jq -n \
  '{operationType:"List",target:"admin",layer:"context_token_registry",action:"list",idOrHubId:null,payload:null,context:null}' | curl -sS -X POST "${BACKEND_URL}/dispatch" \
  -H "Authorization: Bearer ${token}" \
  -H "Content-Type: application/json" \
  --data-binary @-)"

admin_success="$(printf '%s' "${admin_response}" | jq -r '.success // false')"
if [ "${admin_success}" != "true" ]; then
  echo "ERROR: AdminRuntime.ExecuteDataAsync live verification failed; response body:" >&2
  echo "${admin_response}" >&2
  dump_logs
  exit 1
fi
echo "OK: AdminRuntime.ExecuteDataAsync returned success"

echo "=== [RUNTIME_ENV] Live verification: OutputLaneRouter db_notify -> pg_notify -> LISTEN -> scheduler -> SSE ==="
notify_channel="topolactor_topology_changed"
seed_manifest_id="$(docker exec topolactor-demo-postgres psql -U topolactor_demo -d topolactor_demo -tA -c "
SELECT manifest_id::text
FROM manifest
WHERE status = 'active'
  AND EXISTS (
    SELECT 1
    FROM unnest(topology) AS e
    WHERE e->>'type' = 'db_notify_projection_mapping'
      AND COALESCE(e->>'runtime_destination','') = 'sse_projection_runtime'
  )
ORDER BY manifest_id
LIMIT 1;
")"
if [ -z "${seed_manifest_id}" ]; then
  echo "ERROR: no active manifest with db_notify_projection_mapping -> sse_projection_runtime found" >&2
  dump_logs
  exit 1
fi

probe_id="runtime-env-live-e2e-$(date +%s)-$RANDOM"
notify_payload="$(jq -nc \
  --arg table_id "runtime-env-live-e2e" \
  --arg table_registry_id "runtime-env-live-e2e" \
  --arg manifest_id "${seed_manifest_id}" \
  --arg probe_id "${probe_id}" \
  '{table_id:$table_id,table_registry_id:$table_registry_id,manifest_id:$manifest_id,probe_id:$probe_id}')"

sse_output_file="$(mktemp)"
curl -sS -N \
  -H "Authorization: Bearer ${token}" \
  "${BACKEND_URL}/sse" > "${sse_output_file}" &
sse_pid=$!
sleep 1

if ! kill -0 "${sse_pid}" 2>/dev/null; then
  echo "ERROR: failed to start SSE subscription client" >&2
  dump_logs
  exit 1
fi

cleanup_sse() {
  if kill -0 "${sse_pid}" 2>/dev/null; then
    kill "${sse_pid}" 2>/dev/null || true
    wait "${sse_pid}" 2>/dev/null || true
  fi
  rm -f "${sse_output_file}" || true
}
trap cleanup_sse RETURN

docker exec topolactor-demo-postgres psql -U topolactor_demo -d topolactor_demo -v ON_ERROR_STOP=1 -c \
  "SELECT pg_notify('${notify_channel}', \$\$${notify_payload}\$\$);"

event_observed="false"
for _ in $(seq 1 40); do
  if grep -Fq "${probe_id}" "${sse_output_file}"; then
    event_observed="true"
    break
  fi
  sleep 1
done

if [ "${event_observed}" != "true" ]; then
  echo "ERROR: live SSE E2E probe timed out; channel=${notify_channel} probe_id=${probe_id} was not observed on SSE stream" >&2
  echo "=== [RUNTIME_ENV] SSE output snapshot ===" >&2
  tail -n 120 "${sse_output_file}" >&2 || true
  dump_logs
  exit 1
fi

echo "OK: observed live SSE projection event for channel=${notify_channel} probe_id=${probe_id}"
cleanup_sse
trap - RETURN

echo "=== Runtime environment check passed ==="
