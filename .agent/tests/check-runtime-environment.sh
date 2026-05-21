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
assert_relation_exists "public.topology_edit_log"

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

dispatch_response="$(jq -n \
  '{operationType:"DemoEntity",target:"demo",layer:"entity",action:"list",idOrHubId:null,payload:null,context:null,role:null}' | curl -sS -X POST "${BACKEND_URL}/dispatch" \
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

if [ "${dispatch_structure_map_id}" != "null" ]; then
  echo "ERROR: dispatch structureMapId must be null for demo/entity override route; response body:" >&2
  echo "${dispatch_response}" >&2
  dump_logs
  exit 1
fi

echo "=== Runtime environment check passed ==="
