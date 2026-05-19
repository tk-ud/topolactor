#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
ENV_FILE="${REPO_ROOT}/infra/.env"
ENV_EXAMPLE="${REPO_ROOT}/infra/.env.example"
COMPOSE_FILE="${REPO_ROOT}/infra/docker-compose.yml"

if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR: docker is required to start local postgres." >&2
  exit 1
fi

if [ ! -f "${ENV_FILE}" ]; then
  if [ ! -f "${ENV_EXAMPLE}" ]; then
    echo "ERROR: missing ${ENV_EXAMPLE}; cannot create ${ENV_FILE}." >&2
    exit 1
  fi
  cp "${ENV_EXAMPLE}" "${ENV_FILE}"
  echo "Created infra/.env from infra/.env.example"
fi

if ! grep -Eq '^DEMO_JWT_SECRET=.+' "${ENV_FILE}"; then
  echo "ERROR: DEMO_JWT_SECRET must be non-empty in infra/.env before bootstrap." >&2
  exit 1
fi

cd "${REPO_ROOT}"
docker compose --env-file infra/.env -f infra/docker-compose.yml up -d postgres

container_id="$(docker compose --env-file infra/.env -f infra/docker-compose.yml ps -q postgres)"
if [ -z "${container_id}" ]; then
  echo "ERROR: postgres container id not found." >&2
  exit 1
fi

echo "Waiting for postgres health..."
for _ in $(seq 1 60); do
  health="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}unknown{{end}}' "${container_id}" 2>/dev/null || true)"
  if [ "${health}" = "healthy" ]; then
    echo "postgres is healthy"
    exit 0
  fi
  sleep 2
done

echo "ERROR: postgres did not become healthy within timeout." >&2
exit 1
