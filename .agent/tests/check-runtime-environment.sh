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
require_tool dotnet

cleanup() {
  docker compose -f "${COMPOSE_FILE}" down -v --remove-orphans || true
}
trap cleanup EXIT

cd "${REPO_ROOT}"

echo "=== [RUNTIME_ENV] Start postgres via docker compose ==="
docker compose -f "${COMPOSE_FILE}" up -d postgres

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
  docker compose -f "${COMPOSE_FILE}" logs postgres || true
  exit 1
fi

echo "=== [RUNTIME_ENV] Verify DB connectivity and init migration ==="
docker exec topolactor-demo-postgres psql -U topolactor_demo -d topolactor_demo -c "SELECT 1;"
docker exec topolactor-demo-postgres psql -U topolactor_demo -d topolactor_demo -c "SELECT to_regclass('public.manifest');"
docker exec topolactor-demo-postgres psql -U topolactor_demo -d topolactor_demo -c "SELECT to_regclass('public.topology_edit_log');"

echo "=== [RUNTIME_ENV] Run integration tests against live DB ==="
DATABASE_URL='Host=127.0.0.1;Port=5432;Database=topolactor_demo;Username=topolactor_demo;Password=topolactor_demo' \
  dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj \
  --nologo --verbosity minimal

echo "=== Runtime environment check passed ==="
