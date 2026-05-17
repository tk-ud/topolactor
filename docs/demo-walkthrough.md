# topolactor — Public Scaffold Demo Walkthrough

This walkthrough shows how to observe the canonical runtime route in action using the public scaffold demo data.

**This demo uses fake/demo data only. No real business data is required.**

---

## Prerequisites

1. PostgreSQL running (e.g. via `docker compose -f infra/docker-compose.yml up -d`)
2. Schema and seed applied:
   ```bash
   psql -d topolactor_demo -f db/schema.sql
   psql -d topolactor_demo -f db/topology_tables.sql
   psql -d topolactor_demo -f db/promotion_tables.sql
   psql -d topolactor_demo -f db/context_route_tables.sql
   psql -d topolactor_demo -f db/seed_empty.sql
   psql -d topolactor_demo -f db/demo_seed.sql
   ```
3. Frontend running: `deno task start` (from repository root)
4. Open `http://localhost:8000/demo`

---

## What the Demo Shows

The `/demo` route renders a static scaffold projection using hardcoded demo data.
It illustrates the following canonical pipeline steps:

```
stored_topology_data (demo_seed.sql)
→ attractor_resolve  (demo:hub:overview attractor_key)
→ structure_map_resolve  (structure_maps row 00000000-…-0018)
→ package_resolve  (demo_hub_overview_package)
→ schema_resolve  (demo_entity_schema)
→ component_expand  (demo-hub-overview, demo-entity-table, etc.)
→ emission_or_projection  (frontend /demo route projection)
```

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

---

## Architecture Constraints Maintained

- **canonical runtime route preserved:** `/demo` is a projection entrypoint, not business logic.
- **no silent fallback:** broken policy refs → `CONTEXT_ROUTE_POLICY_NOT_FOUND` error.
- **no hardcoded runtime policy:** all scoring, thresholds, and aggregation window values are in `function_parameters`.
- **frontend is projection only:** demo components accept resolved data as props; no local computation.
- **demo data is fake:** `db/demo_seed.sql` contains no real business data.

---

## Files Reference

| File | Role in demo |
|---|---|
| `db/demo_seed.sql` | Source of all demo topology data |
| `frontend/routes/demo.tsx` | Projection entrypoint for the demo route |
| `frontend/components/HubOverviewCard.tsx` | Hub summary projection component |
| `frontend/components/EntityTableProjection.tsx` | Entity list projection component |
| `frontend/components/RecommendationPanel.tsx` | Recommendation status projection component |
| `frontend/components/ContextTokenBadgeList.tsx` | Token registry badge projection component |
| `frontend/package/demoPackage.ts` | Demo package definitions |
| `frontend/schema/demoSchema.ts` | Demo schema definitions |
| `frontend/registry/componentRegistry.ts` | Component registry (includes demo entries) |
| `backend/runtime/ContextRouteRecommendationResolver.cs` | Recommendation resolver (policy from function_parameters) |
| `backend/repository/NpgsqlContextRouteRepository.cs` | Windowed transition stats query |
