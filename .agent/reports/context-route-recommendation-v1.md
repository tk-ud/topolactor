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
  parameter_key  = default_policy
  parameter_value = JSONB policy blob
```

`ContextRoutePolicy` is a resolved policy record only. It does not own production defaults.

### No production fallback

If policy cannot be resolved from topology data, the resolver returns an explicit status:

```text
ExplicitError(CONTEXT_ROUTE_POLICY_NOT_FOUND)
```

Invalid policy JSON returns:

```text
ExplicitError(CONTEXT_ROUTE_POLICY_INVALID:...)
```

Silent fallback and production default config are prohibited.

`TopologyRepository.LoadFunctionParameterAsync` does not return hardcoded context route policy values. Until real DB access is implemented, missing function parameters return `null` and the resolver surfaces explicit policy-missing status.

Test policy values are isolated in `ContextRoutePolicyTestFixtures` / test stubs only.

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
| `db/seed_empty.sql` | Bootstrap topology seed, including context route `default_policy` row |

### Backend

| File | Role |
|---|---|
| `backend/schema/ContextRouteContracts.cs` | Data contracts for context route events, vectors, neighbors, and recommendation output |
| `backend/schema/ContextRoutePolicyContracts.cs` | Resolved `ContextRoutePolicy` record; no production defaults |
| `backend/repository/ContextRouteRepository.cs` | Context route repository skeleton |
| `backend/repository/TopologyRepository.cs` | Topology repository skeleton; function parameters return missing until real DB access is implemented |
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

### Tests

| File | Role |
|---|---|
| `backend/tests/Topolactor.Runtime.Tests/ContextRoutePolicyTestFixtures.cs` | Test-only valid policy JSON and policy stubs |
| `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs` | Resolver/vector/neighbor tests using explicit policy fixture |
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
| `context_transition_stats` | Transition probability aggregate |

### Optional tables

| Table | Role |
|---|---|
| `context_cluster` | Optional session cluster assignment |
| `context_cluster_label` | Optional human/LLM cluster labels |
| `context_drift_signal` | Optional drift/spike detection output |

## Runtime Behavior

1. Resolve policy from topology `function_parameters`.
2. Build current sparse event vector from context tokens.
3. Append context event.
4. Load recent prefix vectors.
5. Compute nearest prefixes by cosine similarity.
6. Produce `next_operations` and `next_tokens`.
7. Attach recommendation result and evidence to emission.

Status is explicit:

| Status | Meaning |
|---|---|
| `Ok` | Candidates are available |
| `InsufficientHistory` | Cold start or not enough context history |
| `ExplicitError` | Missing/invalid policy or runtime failure |

## Checks

CI passed after merge of PR #19:

- `Structure Check`
- `backend-tests`
- `default-entity-search`
- `frontend-types`
- `db-schema-check`

The DB schema issue was resolved by adding a unique constraint to `function_parameters`:

```sql
CONSTRAINT uq_function_parameters_function_key
    UNIQUE (function_name, parameter_key)
```

This matches the seed behavior:

```sql
ON CONFLICT (function_name, parameter_key) DO NOTHING
```

## Remaining TODO

See `.agent/tasks/todo.md`.

## 2026-05-17 Persistence Update
- TopologyRepository.LoadFunctionParameterAsync を function_parameters 実読みに移行。
- ContextRouteRepository の append/load/stats/upsert を context_route_tables 実DBクエリに置換。
- frontend API /api/admin/context-token-registry の 501 を廃止し、一覧取得・作成をDB接続で実装。
- /api/admin/context-token-registry/[id]/deprecate を追加し、status=deprecated 更新を実装。
- policy fallback / hardcoded seed は追加していない。
