# Claude Agent Environment Setup Protocol

## Trigger condition

Read this file once at READ_ENTRY when the executing agent is Claude (Claude Code on the web / remote execution environment).

## Authority boundary

This file is an environment prerequisite only.
It does not own worktype routing, prompt selection, protocol selection, skill selection, checklist selection, or completion judgment.

After applying the required environment setup, mark the Claude environment prerequisite as handled and resume with the next `AGENTS.md` Entry Route step after the Claude trigger.
Do not restart `AGENTS.md` from the beginning and do not re-enter this file from this protocol.

Resume target:

- `.agent/README.md`
- then Tool-first when `.agent/tools/agent-ui-initial-contract` is usable
- otherwise fallback to `.agent/skills/agent-workflow.md` and the matching `.agent/prompt/<work-type>.md`

Do not manually open every prompt/protocol/skill surface from this Claude setup protocol.

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

### 3. Prepare local environment file

`infra/.env` is a local runtime file and must not be committed. Create it from the tracked example when absent:

```bash
[ -f infra/.env ] || cp infra/.env.example infra/.env
```

`bash .agent/scripts/bootstrap-local-postgres.sh` also performs this copy automatically when `infra/.env` is missing.

### 4. Run bootstrap

```bash
bash .agent/scripts/bootstrap-local-tools.sh
```

This installs dotnet SDK 8 and deno, then starts the Postgres container via docker compose.

### 5. Verify

```bash
source ~/.topolactor-tools/env.sh
docker info
docker compose version
docker ps
```

## Notes

- PATH changes from bootstrap do not persist across shell invocations. Source `~/.topolactor-tools/env.sh` before running dotnet or deno commands.
- `infra/.env` must exist with a non-empty `DEMO_JWT_SECRET` before bootstrap; create it from `infra/.env.example` when absent.
- Keep `infra/.env` local-only. Commit only `infra/.env.example`.
