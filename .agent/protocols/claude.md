# Claude Agent Environment Setup Protocol

## Trigger condition

Read this file at READ_ENTRY when the executing agent is Claude (Claude Code on the web / remote execution environment).

## Context

Claude Code on the web runs in an ephemeral container. Docker is installed but the daemon is not running at session start. Docker Hub unauthenticated pull rate limits apply.

## Confirmed Working Setup Sequence

### 1. Start Docker daemon

`service docker start` fails with `ulimit: error setting limit (Operation not permitted)` in this environment. Start dockerd directly instead:

```bash
dockerd --host=unix:///var/run/docker.sock &>/tmp/dockerd.log &
sleep 5
docker info
```

### 2. Pull images via ECR Public mirror

Docker Hub unauthenticated pull rate limits apply. Use AWS ECR Public as the mirror:

```bash
# postgres
docker pull public.ecr.aws/docker/library/postgres:16-alpine
docker tag public.ecr.aws/docker/library/postgres:16-alpine postgres:16-alpine

# nginx (if needed)
docker pull public.ecr.aws/docker/library/nginx:1.27-alpine
docker tag public.ecr.aws/docker/library/nginx:1.27-alpine nginx:1.27-alpine
```

### 3. Run bootstrap

```bash
bash .agent/scripts/bootstrap-local-tools.sh
```

This installs dotnet SDK 8 and deno, then starts the Postgres container via docker compose.

### 4. Verify

```bash
source ~/.topolactor-tools/env.sh
docker info
docker compose version
docker ps
```

## Notes

- PATH changes from bootstrap do not persist across shell invocations. Source `~/.topolactor-tools/env.sh` before running dotnet or deno commands.
- `infra/.env` must exist with a non-empty `DEMO_JWT_SECRET` before bootstrap. Copy from `infra/.env.example` if absent.
- This is a public scaffold repository; `infra/.env` contains only demo values and may be committed.
