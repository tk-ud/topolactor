#!/usr/bin/env bash
set -euo pipefail

if ! command -v docker >/dev/null 2>&1; then
  echo "WARN: docker not available (REQUIRED_NOT_EXECUTED, not PASS)" >&2
  exit 2
fi

if [ ! -f infra/.env ]; then
  cp infra/.env.example infra/.env
fi

if ! grep -Eq '^DEMO_JWT_SECRET=.+' infra/.env; then
  echo 'DEMO_JWT_SECRET=topolactor_demo_local_secret' >> infra/.env
fi

docker compose --env-file infra/.env -f infra/docker-compose.yml config >/dev/null
bash .agent/scripts/bootstrap-local-postgres.sh

docker compose --env-file infra/.env -f infra/docker-compose.yml ps postgres | grep -q healthy

echo "docker-compose/bootstrap verification: PASS"
