# Implementation Report: Context Route Recommendation Runtime

## Summary

Context Route Recommendation Runtime was implemented as an abstract topolactor runtime capability.

It provides generic recommendation output from append-only context events, sparse token vectors, nearest-prefix cosine search, and transition statistics.

This feature is not a business-specific recommendation screen. It is an optional capability attached to the existing topology runtime route.

## Canonical Route Position

```text
stored_topology_data
→ user_operation
→ operation_vector
→ attractor_resolve
→ structure_map_resolve
→ package_resolve
→ schema_resolve
→ component_expand
→ context_route_recommendation_resolve
→ emission_or_projection
```

The runtime layer remains business-agnostic. Terms such as maintenance, work_content, parts, and work_code are intentionally excluded from runtime naming.

## Architecture Decisions

### Context route is topology-owned

Context Route Recommendation is not implemented as an independent config subsystem.

Policy is stored in the existing topology definition surface:

```text
function_parameters
  function_name  = context_route_recommendation_resolve
  parameter_key  = default_policy  (or a scoped key from structure_maps.state_policy)
  parameter_value = JSONB policy blob
```

`ContextRoutePolicy` is a resolved policy record only. It does not own production defaults.

Per-structure-map policy scoping is supported via `structure_maps.state_policy.context_route_policy_ref`.
When present, that key overrides `default_policy` for the given structure map.

### No production fallback

If policy cannot be resolved from topology data, the resolver returns an explicit status:

```text
ExplicitError(CONTEXT_ROUTE_POLICY_NOT_FOUND)
```

Invalid policy JSON in `function_parameters` returns:

```text
ExplicitError(CONTEXT_ROUTE_POLICY_INVALID:...)
```

Malformed `structure_maps.state_policy` JSON returns:

```text
ExplicitError(CONTEXT_ROUTE_STATE_POLICY_INVALID)
```

Empty or whitespace `context_route_policy_ref` returns:

```text
ExplicitError(CONTEXT_ROUTE_POLICY_REF_INVALID)
```

Silent fallback and production default config are prohibited.

Test policy values are isolated in `ContextRoutePolicyTestFixtures` / test stubs only.

### Persistence

Production DB access is provided by:

- `NpgsqlTopologyRepository` — overrides all `Load*` methods in `TopologyRepository` with real Npgsql queries.
- `NpgsqlContextRouteRepository` — overrides all methods in `ContextRouteRepository` with real Npgsql queries.

Tests continue to use in-memory stubs via virtual method overrides on the base classes.

### Transition statistics are a true conditional proportion

`context_transition_stats.prob01` is maintained as `count_hits / count_events`, where `count_events`
is the shared denominator across all `next_operations` in the same `(prev_operation, role, user_id)` scope.

Near-realtime update runs as a 3-step transaction inline on each `AppendContextEventAsync` call:

1. Upsert `(prev, next)` edge — `count_hits += 1`.
2. `UPDATE` all rows with same `(prev, role, user_id)` scope — `count_events += 1`.
3. `UPDATE` all rows in scope — `prob01 = count_hits::float / count_events`.

Smoothing parameters are not hardcoded. If smoothing is needed, parameters must come from `function_parameters`.

### Frontend is projection only

Frontend receives `ContextRouteRecommendation` as emitted data and displays it.

Frontend does not compute:

- cosine similarity
- nearest-prefix search
- transition probability
- candidate ranking
- fallback behavior

## Main Files

### DB

| File | Role |
|---|---|
| `db/context_route_tables.sql` | Context route tables: token registry, events, vector caches, transition stats, optional analytics tables |
| `db/schema.sql` | `function_parameters` topology policy table, including unique `(function_name, parameter_key)` constraint |
| `db/seed_empty.sql` | Bootstrap topology seed, including context route `default_policy` row and `context_event_retention / retention_policy` row |

### Backend

| File | Role |
|---|---|
| `backend/schema/ContextRouteContracts.cs` | Data contracts for context route events, vectors, neighbors, and recommendation output |
| `backend/schema/ContextRoutePolicyContracts.cs` | Resolved `ContextRoutePolicy` record; no production defaults |
| `backend/repository/ContextRouteRepository.cs` | Context route repository base class (in-memory skeleton; used by tests) |
| `backend/repository/NpgsqlContextRouteRepository.cs` | Production Npgsql implementation of ContextRouteRepository |
| `backend/repository/TopologyRepository.cs` | Topology repository base class (in-memory skeleton; used by tests) |
| `backend/repository/NpgsqlTopologyRepository.cs` | Production Npgsql implementation of TopologyRepository |
| `backend/runtime/ContextVectorBuilder.cs` | Sparse event/prefix vector builder and L2 norm computation |
| `backend/runtime/ContextNeighborSearch.cs` | Cosine similarity and nearest-prefix search |
| `backend/runtime/ContextRouteRecommendationResolver.cs` | Recommendation resolver inserted after component expansion |
| `backend/runtime/RuntimeExecutor.cs` | Connects recommendation resolver into canonical route |
| `backend/runtime/EmissionBuilder.cs` | Adds recommendation result to emission |

### Frontend

| File | Role |
|---|---|
| `frontend/api/dispatch.ts` | Recommendation output types |
| `frontend/components/EmissionView.tsx` | Projection-only recommendation display |
| `frontend/routes/admin/context-token-registry.tsx` | Token registry admin page shell |
| `frontend/islands/ContextTokenRegistryEditor.tsx` | Token registry admin island; no hardcoded token seed |
| `frontend/routes/api/admin/context-token-registry.ts` | Explicit 501 until real DB endpoint is implemented |
| `frontend/routes/api/admin/context-token-registry/[tokenId]/deprecate.ts` | Deprecate endpoint with UUID validation; 501 until real DB update is implemented |

### Tests

| File | Role |
|---|---|
| `backend/tests/Topolactor.Runtime.Tests/ContextRoutePolicyTestFixtures.cs` | Test-only valid policy JSON and policy stubs (StubValidPolicyTopologyRepository, StubMissingPolicyTopologyRepository, StubScopedPolicyTopologyRepository) |
| `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs` | Resolver/vector/neighbor tests including policy scope and explicit error tests |
| `backend/tests/Topolactor.Runtime.Tests/RuntimeExecutorTests.cs` | Runtime executor tests with policy fixture injected only for recommendation branch |

### Design / Agent Docs

| File | Role |
|---|---|
| `docs/design/context-route-recommendation.md` | Design policy and handling guide |
| `docs/design/context-route-recommendation.yaml` | Structure-level design SSOT |
| `.agent/docs/design-ssot-index.md` | Agent-facing design index |
| `.agent/tasks/todo.md` | Remaining TODO only |

## DB Tables

### Core tables

| Table | Role |
|---|---|
| `context_token_registry` | Discrete token dictionary; `value` is sparse vector component |
| `context_token_binding` | Optional table/domain token group scope |
| `context_session` | Implicit session grouping |
| `context_event` | Append-only context operation event log |
| `context_record_snapshot_cache` | Current token snapshot cache by record |
| `context_event_vector_cache` | Event sparse vector + L2 norm cache |
| `context_prefix_vector_cache` | Session prefix vector cache for nearest-prefix search |
| `context_transition_stats` | Transition probability aggregate (prob01 = count_hits / count_events) |

### Optional tables

| Table | Role |
|---|---|
| `context_cluster` | Optional session cluster assignment |
| `context_cluster_label` | Optional human/LLM cluster labels |
| `context_drift_signal` | Optional drift/spike detection output |

## Runtime Behavior

1. Resolve policy key from `structure_maps.state_policy.context_route_policy_ref` (or fall back to `default_policy`).
2. Load policy from topology `function_parameters` using the resolved key.
3. Build current sparse event vector from context tokens.
4. Append context event and update transition stats (3-step transaction).
5. Load recent prefix vectors.
6. Compute nearest prefixes by cosine similarity.
7. Produce `next_operations` and `next_tokens`.
8. Attach recommendation result and evidence to emission.

Status is explicit:

| Status | Meaning |
|---|---|
| `Ok` | Candidates are available |
| `InsufficientHistory` | Cold start or not enough context history |
| `ExplicitError` | Missing/invalid policy, malformed state_policy, empty policy_ref, or runtime failure |

## CI Status

`check-structure.sh`: PASS

`check-backend-tests.sh`, `check-frontend-types.sh`, `check-default-entity-search.sh`:
not executed — `dotnet` / `deno` not available in agent environment.

## Remaining TODO

See `.agent/tasks/todo.md`.
