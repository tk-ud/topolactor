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

for tbl in ui_component_bucket ui_topology_tensor context_token_registry; do
  docker compose --env-file infra/.env -f infra/docker-compose.yml exec -T postgres     psql -U topolactor_demo -d topolactor_demo -tAc "SELECT to_regclass('public.${tbl}') IS NOT NULL;" | grep -q t || {
      echo "ERROR: table '${tbl}' not found after init" >&2
      exit 1
    }
done

echo "docker-compose/bootstrap + db-init verification: PASS"
