# topolactor — Public Scaffold Demo Walkthrough

This walkthrough shows how to observe the canonical runtime route in action using the public scaffold demo data.

**This demo uses fake/demo data only. No real domain data is required.**

---


## Environment Routes and Required Variables

This repository supports three operation routes. Keep them separated to avoid mixed backend targets.

### Route A: Local dev (Fresh + backend on host)
- Backend required env: `DATABASE_URL`, `DEMO_JWT_SECRET`, `DEMO_JWT_EXPIRY_HOURS` (`DEMO_JWT_ISSUER` optional, `BACKEND_PORT` optional default 5000 in local dev).
- Frontend required env: `DEMO_BACKEND_URL=http://localhost:5000` (or your backend port).
- If `DEMO_BACKEND_URL` is missing, Fresh proxy routes return explicit 501 (`*_BACKEND_NOT_CONFIGURED`).

### Route B: Docker Compose demo
- Use `infra/.env` (copy from `infra/.env.example`).
- Required: `DATABASE_URL`, `DEMO_JWT_SECRET`, `DEMO_JWT_EXPIRY_HOURS`.
- Internal compose wiring is fixed for consistency: backend `5000`, frontend `8000`, nginx upstream `backend:5000`/`frontend:8000`, backend healthcheck `localhost:5000`.
- `DEMO_BACKEND_URL` defaults to `http://backend:5000` in compose unless overridden.
- nginx handles `/api/*` to backend directly (Fresh proxy bypassed).

### Route C: Production-like reverse proxy
- Same backend required env as Route A/B.
- Reverse proxy routes `/api/*` to backend runtime and `/` to frontend runtime.
- Keep a single canonical backend target to avoid route drift between proxy and app-level forwarding.

### Explicit failures (intended)
- Backend startup throws configuration error when `DATABASE_URL` is missing (no in-memory fallback).
- `/dispatch` and `/admin/*` return 401 when auth token or signing secret validation fails.
- Fresh proxy returns 502 (`*_BACKEND_UNREACHABLE`) when backend cannot be reached.

---

## Prerequisites

1. PostgreSQL running and seeded:
   ```bash
   docker compose -f infra/docker-compose.yml up -d
   ```
   On a **fresh volume**, `docker-entrypoint-initdb.d` automatically applies (in order):
   `schema.sql` → `topology_tables.sql` → `promotion_tables.sql` →
   `context_route_tables.sql` → `seed_empty.sql` → `demo_seed.sql`

   On an **existing volume**, the init scripts do not re-run. Apply the demo seed manually:
   ```bash
   psql -d topolactor_demo -f db/demo_seed.sql
   ```

2. Frontend running: `deno task start` (from repository root)
3. Open `http://localhost:8000/demo` (runtime dispatch page)

> **Note — full stack (Docker Compose):** All five services are defined in
> `infra/docker-compose.yml`: `postgres`, `adminer`, `backend`, `frontend`, `nginx`.
>
> Before starting the stack, create `infra/.env` from the template:
> ```bash
> cp infra/.env.example infra/.env
> # Fill in DEMO_JWT_SECRET (required) and DEMO_JWT_EXPIRY_HOURS (required).
> # Example: DEMO_JWT_SECRET=$(openssl rand -hex 32)
> ```
>
> Then start the full stack:
> ```bash
> docker compose --env-file infra/.env -f infra/docker-compose.yml up -d
> ```
>
> nginx listens on port 80 and routes `/api/*` → backend (port 5000), `/` → frontend (port 8000).
> The backend healthcheck at `GET /health` must pass before frontend and nginx start.
>
> `/dispatch` is always JWT-guarded. When `DEMO_JWT_SECRET` is not set,
> all `/dispatch` calls return `AUTH_JWT_SECRET_NOT_CONFIGURED` — the endpoint is
> never silently unauthenticated.

> **Note — demo login:** The JWT login scaffold is at `/login`. The login form calls
> `/api/auth/login` (Fresh route → nginx → `POST /auth/login` on the backend).
> Backend auth is implemented in `AuthEndpoint.cs` (wired via `backend/Program.cs`).
> Demo credentials are stored as bcrypt hashes in `function_parameters`
> (`demo_auth / demo_users`) via `db/demo_seed.sql`.

> **Note — log retention:** The backend runs a `RetentionScheduler` background service
> that calls `LogRetentionRuntime` to clean up `context_event` rows older than `cold_days`
> (and outside the `hot_days` safety window, if set).
> Retention policy (`enabled`, `cold_days`, `hot_days`, `archive_strategy`, `batch_size`,
> `schedule_interval_hours`) is stored in `function_parameters`
> (`context_event_retention / retention_policy`) and seeded by `db/seed_empty.sql`.
>
> `archive_strategy` controls how eligible rows are removed:
> - `"delete"` — rows are permanently purged from `context_event` (default seed value).
> - `"archive"` — rows are moved to the `context_event_cold` cold table before being removed from `context_event`.
>
> `hot_days`, if set to a positive integer, acts as a safety floor: events within that window
> are never deleted or archived regardless of `cold_days`.
>
> Set `enabled: false` in the policy row to disable cleanup. The scheduler logs an
> explicit `Disabled` status rather than silently skipping.

---

## Demo Login → Dispatch Flow

To observe the backend canonical flow end-to-end:

1. **Log in:** Open `/login`, enter demo credentials (seeded by `db/demo_seed.sql`).
   - On success, the JWT token is saved to browser `sessionStorage` under `demo_jwt_token`.
   - A "Go to dispatch panel" link is shown.
2. **Open `/`** (the dispatch panel). The OperationPanel reads the token from `sessionStorage`
   and shows "Authenticated." status.
3. **Submit a dispatch operation** (e.g. target `default`, layer `entity`, action `Search`).
   - The panel sends `POST /api/dispatch` with `Authorization: Bearer <token>`.
   - The backend JwtGuard validates the token, RuntimeExecutor runs the canonical flow,
     and the emission is returned and displayed by EmissionView.

**Routing — two paths depending on how you access the frontend:**

| Access method | `/api/dispatch` routing |
|---|---|
| Docker Compose / nginx (port 80) | nginx `location /api/` → `backend:5000 POST /dispatch` directly. Fresh proxy is bypassed by nginx. |
| Fresh standalone (port 8000, `deno task start`) | Fresh route `frontend/routes/api/dispatch.ts` → `DEMO_BACKEND_URL/dispatch`. Requires `DEMO_BACKEND_URL` to be set. |

**Without login:** the OperationPanel shows "Not logged in" status and any dispatch attempt
returns `AUTH_TOKEN_MISSING` from the backend — explicit, not silent.

**Fresh standalone only — `DEMO_BACKEND_URL` not configured:** `/api/dispatch` returns
`DISPATCH_BACKEND_NOT_CONFIGURED` (501). This error is from the Fresh proxy and does not
apply when nginx routes the request directly to the backend.

---

## What the Demo Shows

The `/demo` route is the **runtime dispatch entrypoint**. It pre-fills the dispatch panel
with demo defaults (`target=demo`, `layer=hub`, `action=overview`) and dispatches to the
backend on submit:

```
login → POST /api/dispatch (JWT) → backend RuntimeExecutor
→ attractor_resolve  (demo:hub:overview attractor_key)
→ structure_map_resolve  (structure_maps row 00000000-…-0018)
→ package_resolve  (demo_hub_overview_package)
→ schema_resolve  (demo_entity_schema)
→ component_expand  (demo-hub-overview, demo-entity-table, etc.)
→ emission → EmissionView (frontend projection)
```

No synthetic data is used. The emission displayed is the real backend response.

The `/demo-static` route contains the **static structure diagram**: it runs the
frontend-only pipeline (`resolveOperationVector → lookupStructureMap → renderEmission`)
against `defaultStructureMap` and `defaultComponentRegistry` without any backend API call.
It is a developer reference diagram and is not presented as a runtime result.

---

## Observable Scenarios

Each scenario shows how a single Registry or policy change propagates through the runtime.

### Scenario A — Token value change → recommendation score change

1. Query the current token state:
   ```sql
   SELECT token_id, label, value FROM context_token_registry
   WHERE "group" = 'status';
   ```
2. Change the `warning` token value from `0.0` to `0.5`:
   ```sql
   UPDATE context_token_registry
   SET value = 0.5, updated_at = now()
   WHERE token_id = '00000000-0000-0000-0000-000000000022';
   ```
3. Re-run the recommendation resolver for a session with token `warning` active.
   The event vector changes → cosine similarity to historical prefixes changes →
   recommendation scores shift.
4. **Why it works:** token `value` is the meaning direction component used to build
   the sparse event vector. The resolver reads `context_token_registry` on every call;
   no cache invalidation needed.

> **Note:** This change affects runtime resolution via the dispatch API, not the static `/demo` route.

### Scenario B — context_route_policy_ref change → different policy loads

1. Inspect the current demo structure_map state_policy:
   ```sql
   SELECT state_policy FROM structure_maps
   WHERE structure_map_id = '00000000-0000-0000-0000-000000000018';
   -- Returns: {"context_route_policy_ref":"demo_policy"}
   ```
2. Add a new scoped policy to `function_parameters`:
   ```sql
   INSERT INTO function_parameters (function_name, parameter_key, parameter_value, active)
   VALUES (
     'context_route_recommendation_resolve',
     'strict_policy',
     '{"min_similarity":0.3,"top_k":10,"min_neighbors":20,"recent_days":30,"max_candidates_shown":2,"baseline_weight":0.6,"neighbor_weight":0.4,"transition_aggregation":{"aggregation_limit":1000,"prefer_recent":true,"recent_days":30}}',
     true
   );
   ```
3. Switch the structure_map to use the new policy:
   ```sql
   UPDATE structure_maps
   SET state_policy = '{"context_route_policy_ref":"strict_policy"}'
   WHERE structure_map_id = '00000000-0000-0000-0000-000000000018';
   ```
4. The resolver now loads `strict_policy` instead of `demo_policy`. Higher `min_similarity`
   and `min_neighbors` thresholds → more `InsufficientHistory` results until enough history
   accumulates.
5. **Why it works:** `ResolvePolicyKey()` reads `context_route_policy_ref` from
   `structure_maps.state_policy` on every call. No code deployment needed.

> **Note:** This change affects the recommendation resolver when called via the dispatch API, not the static seed-reference display in `/demo`.

### Scenario C — transition_aggregation.aggregation_limit change → windowed scope changes

1. Inspect the current demo_policy:
   ```sql
   SELECT parameter_value FROM function_parameters
   WHERE function_name = 'context_route_recommendation_resolve'
     AND parameter_key = 'demo_policy';
   ```
2. Reduce the aggregation window to 100 events:
   ```sql
   UPDATE function_parameters
   SET parameter_value = parameter_value || '{"transition_aggregation":{"aggregation_limit":100,"prefer_recent":true,"recent_days":null}}'
   WHERE function_name = 'context_route_recommendation_resolve'
     AND parameter_key = 'demo_policy';
   ```
3. The next recommendation call uses only the 100 most recent events as the
   source for windowed transition stat computation. Older transitions are excluded
   from `prob01` calculation.
4. **Why it works:** `GetWindowedTransitionStatsAsync` queries `context_event`
   directly with `LIMIT @aggregation_limit ORDER BY created_at DESC`.
   The policy-missing case returns an explicit error, not a silent fallback.

> **Note:** This change affects the recommendation resolver when called via the dispatch API, not the static seed-reference display in `/demo`.

### Scenario E — context fields in dispatch panel → recommendation results visible

`db/demo_seed.sql` seeds pre-computed prefix vectors and a windowed transition from
`demo:hub:overview` → `demo:entity:list`. With the demo policy (`min_neighbors=1`), a
single matching prefix is enough to return a recommendation.

1. **Log in** at `/login` with demo credentials.
2. **Open `/demo`** (runtime dispatch page — pre-filled with target=demo, layer=hub, action=overview).
   Alternatively use the dispatch panel at `/`.
3. **Expand "Context fields (optional)"** in the operation form.
4. Enter the demo session ID in *Context Session ID*:
   ```
   00000000-0000-0000-0000-000000000031
   ```
5. Enter the demo token ID in *Context Token IDs* (comma-separated):
   ```
   00000000-0000-0000-0000-000000000021
   ```
6. Submit.
7. The emission's `context_route_recommendation` section shows:
   - `status: "ok"`
   - `nextOperations: [{"value": "demo:entity:list", "score": ~0.6, ...}]`

**Why it works:** `demo_seed.sql` inserts fixed-UUID events and their pre-computed prefix
vectors (`context_prefix_vector_cache`). The resolver loads prefix candidates first (before
appending the current dispatch), so prefix_index=0 finds `next_operation=demo:entity:list`
cleanly. Neighbor voting at similarity=1.0 with `neighbor_weight=0.6` gives score=0.6.
Prefix_index=1 has `next_operation=NULL` at that point (no event after it yet) and does not vote.

**Route identity:** the resolver uses the full attractor key (`demo:hub:overview`) as `currentOperation`,
which matches the `operation` column in seeded `context_event` rows. `tableName` is not used as
a filter — prefix candidates are scoped by `session_id` and `recent_days`, not by `table_name`.

**Ordering guarantee:** All recommendation reads (`LoadRecentPrefixVectorsAsync`, transition stats)
complete before `AppendContextEventAsync`. This preserves the prefix → next_operation relationship:
at candidate read time, prefix_index=1's `last_event_id` has no successor, so it holds `next_operation=NULL`
and does not vote. The append runs on every path — including cold-start — so that history grows across
subsequent dispatches and the session eventually reaches `Ok` status.

> **Cold start (no context fields):** Without a `ContextSessionId`, the resolver returns
> `InsufficientHistory — NO_SESSION_ID`. This is expected when submitting from `/demo` without
> filling in the context fields — it is a real backend response, not a static placeholder.

### Scenario D — structure_map or componentRegistry change → /demo-static projection changes

1. Open `frontend/structure_map.ts` and modify the `"demo:hub:overview"` entry — for example, change `componentIds` to include an additional component ID, or update `packageId`/`schemaId`.
2. Reload `http://localhost:8000/demo-static`.
3. The OperationVector block, the `ProjectionView` resolved StructureMap entry, and the Expanded ComponentSpecs list all update to reflect the new entry.
4. Alternatively, open `frontend/registry/componentRegistry.ts` and add or modify an entry for a component ID referenced in `defaultStructureMap`. The rendered ComponentSpec type and definition update on next page load.
5. **Why it works:** `/demo-static` calls `resolveOperationVector → lookupStructureMap → renderEmission` entirely in the frontend at render time. No DB or backend API is involved. The pipeline runs against `defaultStructureMap` and `defaultComponentRegistry` as imported — changing those modules changes what `/demo-static` resolves and displays.

> **Note:** This is the only scenario that changes `/demo-static` output without a backend API call. Scenarios A, B, C, and E affect backend resolution via the dispatch API and are observable at `/demo` (runtime dispatch page) or `/` (dispatch panel).

---

## Architecture Constraints Maintained

- **canonical runtime route preserved:** `/demo` dispatches to backend runtime; no frontend-only resolution presented as runtime result.
- **no synthetic runtime display:** `/demo` uses `OperationPanel` backed by `POST /api/dispatch`; synthetic emission is isolated to `/demo-static` and clearly labelled as static.
- **no silent fallback:** broken policy refs → `CONTEXT_ROUTE_POLICY_NOT_FOUND` error; backend unreachable → explicit fetch error in EmissionView.
- **no hardcoded runtime policy:** all scoring, thresholds, and aggregation window values are in `function_parameters`.
- **frontend is projection only:** demo components accept resolved data as props; no local computation.
- **static diagram separated:** `/demo-static` clearly labels output as a static structure diagram, not a runtime result.
- **demo data is fake:** `db/demo_seed.sql` contains no real domain data.

---

## Files Reference

| File | Role in demo |
|---|---|
| `db/demo_seed.sql` | Source of all demo topology data (backend/DB scenarios A–C, E) |
| `frontend/routes/demo.tsx` | Runtime dispatch entrypoint — backend emission via OperationPanel |
| `frontend/routes/demo-static.tsx` | Static structure diagram — frontend-only pipeline, no backend call (Scenario D) |
| `frontend/structure_map.ts` | `defaultStructureMap` — change entries to change `/demo-static` resolution (Scenario D) |
| `frontend/registry/componentRegistry.ts` | `defaultComponentRegistry` — change entries to change `/demo-static` ComponentSpecs (Scenario D) |
| `frontend/runtime/resolveOperationVector.ts` | Converts UserOperation → OperationVector + attractorKey |
| `frontend/runtime/renderEmission.ts` | Resolves componentIds through ComponentRegistry → ComponentSpec[] |
| `frontend/components/ProjectionView.tsx` | Renders resolved StructureMapEntry and Emission data |
| `frontend/components/EmissionView.tsx` | Surfaces all fields of a backend Emission for inspection |
| `frontend/components/RecommendationPanel.tsx` | Recommendation status projection component |
| `frontend/components/ContextTokenBadgeList.tsx` | Token registry badge projection component (seed reference display) |
| `frontend/islands/OperationPanel.tsx` | Interactive dispatch island — sends POST /api/dispatch, shows EmissionView |
| `frontend/package/demoPackage.ts` | Demo package definitions |
| `frontend/schema/demoSchema.ts` | Demo schema definitions |
| `backend/runtime/ContextRouteRecommendationResolver.cs` | Recommendation resolver (policy from function_parameters) |
| `backend/repository/NpgsqlContextRouteRepository.cs` | Windowed transition stats query |

## Scenario F: demo entity state loop
- Dispatch `target=demo, layer=entity, action=detail|advance|create` with payload `{ "entityId": "<uuid>", "title": "..." }`.
- Runtime applies DB-backed transition (`entities` + `demo_state_transitions`) and returns emission data with DB-backed `items` (list), `detail`, and `history` (no frontend fallback).
- Invalid payload/transition/missing entity are explicit errors (`INVALID_PAYLOAD`, `INVALID_TRANSITION`, `STATE_NOT_FOUND`).

- `action=detail` requires payload `entityId` UUID; missing/malformed is `INVALID_PAYLOAD`, unknown action is `INVALID_OPERATION`.

- Runtime reachability: DB seed contains structure_maps for `demo:entity:list`, `demo:entity:detail`, `demo:entity:create`, `demo:entity:advance`, so these operations pass attractor/structure-map resolution before state-loop execution.

## CI runtime-meaning checks

- CI/local should run `bash .agent/tests/check-runtime-semantics.sh` to validate runtime semantics (dispatch/recommendation/admin proxy), not only structure checks.
- Runtime-meaning check is considered complete only after execution succeeds in an environment with both dotnet and deno available.
- Docker Compose full E2E smoke remains an optional local check due to runtime/cost; it is not a blocking CI gate in this phase.

