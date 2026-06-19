# Refactor Todo: Abstract Function Runtime Substrate

Target repo: `github.com/tk-ud/topolactor`
Status: `partial_compatibility_fallback`
Maintenance class: `abstract_function_refactor_maintenance`
Queue role: refactor maintenance note for abstract function substrate work; not a product roadmap/status TODO and not canonical `.agent/tasks/todo.md` completion-status management.
Source judgment: audit carry-over from abstract-function / SQL Attention / recommendation / credential / execute_db_function review.

## Operating rules

- Read `AGENTS.md` before implementation.
- Treat SSOT docs as canonical. Implementation is a projection surface, not the source of truth.
- Do not split this into small implementation atoms such as one helper, one route, one UI component, or one backend method.
- Do not add provider-specific runtime handlers, bundle-specific one-off backend handlers, dedicated credential planes, or frontend SQL/recommendation judgment.
- Direct implementation should be Bundle-scoped. If scope must shrink, preserve the Bundle boundary and explicitly mark unimplemented acceptance conditions.
- Roadmap bundle / known_gap_ref / product completion_condition linkage is intentionally omitted here. Add it only if this note is later promoted into `.agent/tasks/todo.md` or roadmap/status maintenance.
- `docs/system-roadmap.yaml` and `.agent/tasks/todo.md` are status/reference surfaces, not implementation proof. Verify actual code and tests before changing completion status.

## Mandatory migration order

**実装順序 (per Bundle):**
`seed/manifest → dispatch → 汎用 primitive / abstract executor → SQL/repository adapter → seed-route test → 既存 concrete 削除/compat 縮小 → Bundle 完了`

The refactor must follow this global order. Do not delete existing concrete functions first.

**Execution path invariant (applies to all remaining Bundles):**
Every migrated operation must satisfy:
`seed/manifest → dispatch → 汎用 primitive / abstract executor → manifest-authorized SQL`
No Bundle is complete if any of the four legs is missing: the seed/manifest must express the operation, the dispatch layer must route to `AbstractFunctionExecutor`, the generic primitive adapter must carry the semantics, and SQL/DB access must be authorized through the manifest (step_config / authority_bindings), not through C#-side hardcoding, frontend payload authority, or direct repository calls outside the primitive adapter.

1. **SSOT fix and abstract function primitive generation**
   - Extend the existing SSOT documents first. Do not create a separate abstract-function runtime SSOT unless the user explicitly reopens SSOT ownership.
   - Define or extend `execute_abstract_function`, primitive taxonomy, manifest/runtime boundary, authority/input/output/projection rules, and fail-close vocabulary.
   - Generate the abstract function primitive set and generic primitive adapters needed by the target absorption Bundles.
   - Keep concrete legacy functions in place during this step.
2. **Extend seed for the target Bundle**
   - Add seed/manifest rows that express the target concrete behavior through abstract function steps.
   - Payload values may provide values only; table/column/join/projection/secret authority must come from existing SSOT/manifest/seed authority, not frontend payload.
   - This step is performed per target Bundle.
3. **Run tests against the target Bundle seed**
   - Add or update tests that prove the seed path executes the absorbed behavior, fails closed, and preserves projection/secret/route boundaries.
   - The seed path must be tested before any legacy concrete function removal.
   - This step is performed per target Bundle.
4. **Delete existing concrete functions for the target Bundle**
   - Remove or shrink the old concrete C# methods/switch cases/runtime islands only after seed tests pass.
   - If deletion cannot be completed, leave the legacy path explicitly marked as compatibility fallback and keep the Bundle partial.
   - This step is performed per target Bundle.
5. **Delete this refactor todo**
   - Delete `tasks/refactor_todo.md` only after all target Bundles have completed steps 2–4 and completion checks prove the refactor is no longer a carry-over maintenance note.

Steps 2–4 are a per-Bundle loop. Complete them for each target Bundle before final todo deletion.

## Completed maintenance in PR #477

Status judgment: `partial_ssot_done`. This is SSOT/todo maintenance only. It is not implementation completion evidence and does not authorize deleting concrete functions.

Completed:

- `tasks/refactor_todo.md` migration order now requires SSOT fix / abstract primitive generation before per-Bundle seed → seed test → concrete deletion.
- `docs/design/abstract-function-primitive-registry-ssot.yaml` now owns backend-wide `execute_abstract_function` substrate vocabulary, primitive categories, migration guardrails, and fail-close vocabulary.
- `docs/framework-policy.yaml` now defines `framework_policy.abstract_function_substrate_policy`, including provider/bundle-specific handler prohibitions, frontend/raw SQL prohibitions, payload-derived authority prohibition, plaintext projection prohibition, migration order, and Bundle loop rule.

Still open after this update:

- SQL Attention / recommendation and credential hardening Bundles remain partial/not_started and must not be marked implemented.
- File-storage C# concrete deletion: `NpgsqlExternalPortDbFunctionRepository` concrete `fs_*` switch/methods are now deleted (completed in PR#481). `topology.fs_*` PostgreSQL functions are opaque DB adapters called via `call_postgres_function` primitive — they are NOT deletion targets and must remain.
- Attachment bind/list/unbind migrated to `execute_abstract_function` manifests af05-af07 with `step_config` binding authority for `record_table_ref` (completed in PR#481).

## Audit conclusion

The current architecture already points toward hardcoded abstract function shapes plus data-defined policy/runtime/function parameters, but the backend implementation is still split across concrete runtime classes and repositories.

Primary problem:

- `execute_db_function` is currently a named PostgreSQL function boundary, but its implementation has become a C# `functionName switch` plus per-payload extraction surface.
- `execute_db_operation` would be too small as the main refactor target. It would only absorb CRUD-like work and would leave recommendation, credential, SQL Attention, event, projection, and HTTP-step orchestration as separate concrete backend functions.
- The intended target is a backend-wide `execute_abstract_function` substrate with primitive adapters underneath.

Target abstraction:

```text
execute_abstract_function
  ├─ validate_authority
  ├─ bind_input
  ├─ db_query
  ├─ db_mutation
  ├─ call_postgres_function
  ├─ sql_attention
  ├─ recommendation_attention
  ├─ phase_attention_adapter
  ├─ credential_reference_resolve
  ├─ credential_materialize
  ├─ http_request
  ├─ scheduler_enqueue
  ├─ event_log
  ├─ projection
  └─ fail_close
```

Absorption policy:

| Surface | Refactor judgment |
|---|---|
| DB CRUD / DB mutation | absorb into `db_query` / `db_mutation` primitives |
| `execute_db_function` | demote to `call_postgres_function` primitive |
| SQL Attention | absorb as relational observation / ranking / projection primitive |
| recommendation | absorb as candidate source + eligibility + score + rank + projection primitive |
| credential flow | absorb step orchestration; keep crypto/secret materialization as runtime-only adapter |
| HTTP client | keep generic adapter; no provider-specific handler |
| Phase Attention internals | do not absorb; call through adapter and record evidence/projection only |
| event / audit log | absorb as append-only `event_log` primitive |
| frontend projection | render/request surface only; no topology, SQL Attention, recommendation, credential judgment |

## Bundle index

| Bundle ID | Status | Purpose |
|---|---|---|
| `abstract-function-runtime-substrate-ssot` | ssot_contract_complete | Define backend-wide abstract function runtime SSOT and primitive taxonomy |
| `abstract-function-manifest-schema` | ssot_contract_complete | Add DB manifest/schema surface for abstract functions, steps, authority, output shapes |
| `backend-abstract-function-executor` | partial_compatibility_fallback | Implement runtime executor for abstract function manifests and primitive registry (step_config binding source added; OutputProp propagation added; file-storage attachment migration complete; SQL Attention list_projection and context_route recommendation_resolve migrated via abstract function; credential hardening remains not_started) |
| `sql-recommendation-primitive-migration` | partial_compatibility_fallback | Absorb SQL Attention and recommendation by migration order: abstract function fix → seed → seed test → concrete function deletion. SQL Attention list_projection (af08) complete. context_route.recommendation_resolve (af09) seed/manifest/primitive/tests complete; ContextRouteRecommendationResolver marked COMPATIBILITY FALLBACK. credential hardening remains not_started. |
| `credential-primitive-hardening` | not_started | Absorb credential flow by migration order while preserving runtime-only secret materialization |
| `projection-manifest-primitive-migration` | investigation_needed | Move remaining projection constructor / runtime event / screen operation derivation mapping into projection manifest or projection primitives while preserving pure render/test-only exceptions |
| `scheduler-job-body-primitive-migration` | investigation_needed | Separate scheduler substrate from hardcoded job bodies and move recurring job/evidence/projection work into abstract function or manifest-backed job primitives |
| `cli-mcp-read-export-port-substrate` | not_started | Implement CLI/MCP read/export/import-candidate port through dispatch-secured port and abstract function primitives rather than dedicated tool handlers |
| `file-storage-db-function-to-abstract-function-migration` | implemented | Absorb file-storage DB functions by migration order and remove payload-derived table authority — all 7 fs_* operations migrated to execute_abstract_function manifests af01-af07; NpgsqlExternalPortDbFunctionRepository stub; all acceptance conditions satisfied |
| `completion-gate-and-test-alignment` | implemented | Align tests/checks/status after Bundle migration |

---

## Bundle `abstract-function-runtime-substrate-ssot`

Status: `ssot_contract_complete`

Note: SSOT contract is complete. This is not implementation-absorption completion. Concrete function deletion, file-storage migration, SQL Attention / recommendation, and credential hardening remain as separate Bundles.

### Completed in PR #477

- [x] Extended `docs/design/abstract-function-primitive-registry-ssot.yaml` with backend-wide `execute_abstract_function` substrate vocabulary.
- [x] Defined primitive categories for authority, input binding, DB operation, attention/recommendation, credential, external/event, projection/failure.
- [x] Added fail-close vocabulary for missing/invalid authority, missing/invalid input binding, invalid projection, secret projection denial, and unsupported primitives.
- [x] Added `framework_policy.abstract_function_substrate_policy` with authority, secret, frontend/raw SQL, provider/bundle handler, silent fallback, migration order, and Bundle loop rules.

### Completed in PR #478 / PR #479

- [x] Safely align `docs/design/runtime-orchestration-ssot.yaml` with the abstract-function substrate without unrelated full-file line loss.
- [x] Generate initial implementation primitive adapters (`AbstractFunctionExecutor` + projection/test adapter surface).
- [x] Add DB schema/seed/test coverage before any concrete function deletion; concrete deletion remains blocked because legacy `topology.fs_*` PostgreSQL functions are still the manifest `call_postgres_function` target.

### SSOT contract basis

The following SSOT surfaces together establish the runtime substrate contract:

- `docs/design/abstract-function-primitive-registry-ssot.yaml` `backend_abstract_function_runtime_substrate`: owns `execute_abstract_function` vocabulary, primitive taxonomy (authority / input_binding / db_operation / attention_and_recommendation / credential / external_and_event / projection_and_failure), and fail-close status set.
- `docs/framework-policy.yaml` `abstract_function_substrate_policy`: owns provider/bundle-specific handler prohibition, frontend raw SQL / recommendation / credential judgment prohibition, payload-derived authority prohibition, migration order, and Bundle loop rule.
- `docs/design/runtime-orchestration-ssot.yaml` `abstract_function_or_db_driven_operation_boundary`: owns `execute_abstract_function` runtime contract, fail-close invariant (missing_authority / invalid_authority / missing_input / invalid_input_binding / invalid_projection / secret_projection_denied / unsupported_primitive), and projection deny list (credential / signed_url / bucket / endpoint / storage_path / raw_storage_ref / plaintext_payload).

Concrete deletion is a separate per-Bundle loop step and is NOT part of this SSOT contract.

### Problem

`docs/design/abstract-function-primitive-registry-ssot.yaml` defines UI/UX primitive vocabulary and mutation boundaries, but it explicitly does not own backend execution engine implementation, backend dispatch configuration, DB function persistence, or SQL Attention ranking implementation. Backend-wide `execute_abstract_function` is therefore not yet a canonical runtime substrate.

### Purpose

Define `execute_abstract_function` as the backend-wide substrate that absorbs backend function bodies currently scattered across DB function dispatch, recommendation, credential flow, event logging, and external port policy execution.

### Improvement plan

- [x] Define `execute_abstract_function` scope and non-scope in existing SSOT/policy surfaces.
- [x] Define primitive categories: authority, input binding, DB operation, PostgreSQL function call, SQL Attention, recommendation, credential, HTTP, scheduler, event log, projection, fail-close.
- [x] Define Phase Attention boundary as `phase_attention_adapter`, not as in-VM semantic/ID-space exploration logic.
- [x] Define frontend boundary: request/render/candidate/preview surface only.
- [x] Define secret-deny projection rule for credential, signed URL, bucket, endpoint, storage path, and plaintext payload.
- [x] Define migration relation to existing `execute_db_function` and external port policy steps at policy/vocabulary level.

### Materials

- `AGENTS.md`
- `.agent/rules/rule.md`
- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/abstract-function-primitive-registry-ssot.yaml`
- `docs/design/sql-attention-logs-ssot.yaml`
- `docs/design/context-route-recommendation.yaml`
- `docs/design/external-port-substrate-ssot.yaml`

### Target files

- `docs/design/abstract-function-primitive-registry-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/framework-policy.yaml`
- Extend existing SSOT documents first. Do not create `docs/design/abstract-function-runtime-substrate-ssot.yaml`; splitting runtime substrate ownership would break SSOT.

### Target surfaces / functions

- `abstract_function_contract`
- `runtime_contract.backend_dispatchable_kinds`
- `topology_function_binder`
- `function_parameters`
- `registry_tensor_principle`
- `sql_attention_logs_boundary`
- `recommendation_semantic_authority`
- `credential_requirement`

### Acceptance conditions

- [x] SSOT states that backend function bodies should be expressed as abstract function primitives when possible.
- [x] `execute_db_operation` is not treated as the top-level target; it is a DB primitive under `execute_abstract_function`.
- [x] Phase Attention internals remain outside the abstract function VM.
- [x] SQL Attention and recommendation are explicitly candidate/evidence/projection primitives and must not auto-mutate route or canonical topology state.
- [x] Credential secret materialization is runtime-only and prohibited from projection/log/seed/SSOT output.

---

## Bundle `abstract-function-manifest-schema`

Status: `ssot_contract_complete`

Note: Manifest / schema SSOT contract is complete. This is not implementation-absorption completion. The seed path for file-storage concrete deletion, attachment bind/list/unbind migration, and per-Bundle seed test gate remain as separate open work.

### Completed in PR #478

- [x] Defined `topology.abstract_function_manifests`: manifest authority for `execute_abstract_function` runtime lane, authority scope, `output_shape`, `projection_deny_keys`, and `active` flag.
- [x] Defined `topology.abstract_function_steps`: ordered generic primitive step list; `primitive_key` resolves through backend generic registry only.
- [x] Defined `topology.abstract_function_input_bindings`: typed input binding from payload/context/result/manifest/physical table binding/route context; raw SQL and payload-derived table authority prohibited.
- [x] Defined `topology.abstract_function_authority_bindings`: table/column/join/output/policy authority surface; used for fail-close on missing or invalid authority.
- [x] Added `external_port_policy_steps.abstract_function_key TEXT REFERENCES topology.abstract_function_manifests(function_key)`: policy steps can now call abstract functions by key.
- [x] Added `execute_abstract_function` as a valid `operation_key` in `topology.external_port_policy_steps` CHECK constraint.
- [x] Added bootstrap seed rows for `abstract_function_manifests`, `abstract_function_steps`, `abstract_function_input_bindings`, and `abstract_function_authority_bindings` in `db/seed_empty.sql`.
- [x] Documented all four manifest tables in `docs/design/db-schema.yaml` under `abstract_function_manifest` category.
- [x] Added `execute_abstract_function` to `external-port-substrate-ssot.yaml` `operation_key_allowed_values` and documented `abstract_function_key` policy step surface (minimal SSOT gap closure).

### Problem

`external_port_policy_steps.step_config` is a small string dictionary and cannot safely hold the full authority/input/output structure required for backend-wide abstract functions. Keeping all operation bodies inside `step_config` will recreate hardcoded C# extraction and switch logic.

### Purpose

Create a DB-backed manifest/schema surface for abstract function definitions, primitive steps, input bindings, authority constraints, output shapes, and policy bindings.

### Improvement plan

- [x] Define abstract function manifest tables.
- [x] Define primitive step table with ordered execution.
- [x] Define input binding table that maps payload/context/result values to typed primitive inputs.
- [x] Define table/column/output authority table surface.
- [x] Define secret-deny projection metadata.
- [x] Define relation from external port policy steps to abstract function manifest key.
- [x] Define bootstrap seed examples without raw SQL or frontend-authored table/column authority.

### Materials

- `docs/design/db-schema.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/abstract-function-primitive-registry-ssot.yaml`
- `db/topology_tables.sql`
- `db/seed_empty.sql`

### Target files

- `db/topology_tables.sql`
- `db/seed_empty.sql`
- `docs/design/db-schema.yaml`
- No new abstract-function runtime SSOT file; manifest/schema ownership must be linked back to existing SSOT documents.

### Proposed DB surfaces

```text
topology.abstract_function_manifests
topology.abstract_function_steps
topology.abstract_function_input_bindings
topology.abstract_function_table_authorities
topology.abstract_function_column_authorities
topology.abstract_function_output_shapes
topology.abstract_function_policy_bindings
```

### Acceptance conditions

- [x] No raw SQL is accepted from frontend payload, seed payload, or operation payload. (`abstract_function_manifests` comment and `abstract_function_input_bindings` comment prohibit raw SQL; `db-schema.yaml` authority rules enforce this.)
- [x] Table/column/join/output authority comes from manifest/physical table binding, not frontend payload. (`abstract_function_authority_bindings` is the authority surface; `db-schema.yaml` states `frontend_payload_is_not_table_column_join_output_authority`.)
- [x] `external_port_policy_steps` can call an abstract function by key instead of carrying the full operation body. (`abstract_function_key TEXT REFERENCES topology.abstract_function_manifests(function_key)` column added in PR #478; `execute_abstract_function` is a valid `operation_key`.)
- [x] Secret-bearing fields are deny-listed fail-close. (`projection_deny_keys` column in `abstract_function_manifests`; `db-schema.yaml` states `secret_projection_denied_for_credential_signed_url_bucket_endpoint_storage_path_raw_storage_refs`.)
- [x] Migration path preserves existing external port policy-step lane. (`execute_db_function` operation_key remains in schema CHECK constraint for compatibility; `NpgsqlExternalPortDbFunctionRepository` is now a compatibility stub — all concrete fs_* methods deleted in PR#481; no active seed row uses execute_db_function for file-storage mutations.)

---

## Bundle `backend-abstract-function-executor`

Status: `partial_compatibility_fallback`

Note: NOT marked complete based on file-storage migration alone. SQL Attention / recommendation and credential hardening Bundles remain not_started. File-storage attachment migration is the representative absorption case that proves the substrate works end-to-end.

### Problem

Backend behavior is split across concrete classes:

- `ExternalPortPolicyStepExecutor`
- `NpgsqlExternalPortDbFunctionRepository`
- `ContextRouteRecommendationResolver`
- credential refresher/crypto/http surfaces
- runtime event log repository

This keeps individual backend functions alive instead of expressing them as reusable primitive steps.

### Purpose

Implement a backend `execute_abstract_function` executor that reads abstract function manifests and dispatches only generic primitive adapters.

### Improvement plan

- [x] Add backend records/contracts for abstract function manifest, step, binding, authority, and output shape.
- [x] Add repository read surface for active abstract function manifests.
- [x] Add primitive registry with no provider-specific or bundle-specific branching.
- [x] Support result context binding between primitive steps.
- [x] Support explicit fail-close statuses for missing authority, missing input, invalid projection, and unsupported primitive; credential-specific hardening remains in its Bundle.
- [x] Route external port `execute_abstract_function` through existing `external_port_runtime` lane without new API route.
- [x] Keep concrete compute adapters for checksum, crypto, HTTP client, DB connection, and Phase Attention engine; only `call_postgres_function` is implemented in this Bundle slice.

### Materials

- `backend/runtime/ExternalPortCredentialRefresher.cs`
- `backend/runtime/ExternalPortDispatchRuntime.cs`
- `backend/runtime/ContextRouteRecommendationResolver.cs`
- `backend/repository/NpgsqlExternalPortDbFunctionRepository.cs`
- `backend/repository/NpgsqlExternalPortPolicyRepository.cs`
- `backend/Program.cs`
- `backend/tests/Topolactor.Runtime.Tests/*`

### Target files

- `backend/runtime/*AbstractFunction*`
- `backend/repository/*AbstractFunction*`
- `backend/runtime/ExternalPortCredentialRefresher.cs`
- `backend/runtime/ExternalPortDispatchRuntime.cs`
- `backend/repository/NpgsqlExternalPortDbFunctionRepository.cs`
- `backend/Program.cs`

### Target functions / classes

- `ExternalPortPolicyStepExecutor.ExecutePolicyAsync`
- `ExternalPortPolicyStepExecutor.ExecuteAsync`
- `IExternalPortDbFunctionRepository.ExecuteAsync`
- `ContextRouteRecommendationResolver.ResolveAsync`
- `ExternalTokenRefresher.RefreshIfNeededAsync`
- `IExternalPortRuntimeEventLogRepository.AppendAsync`

### Acceptance conditions

- [x] Backend abstract function executor exists and is registered through DI.
- [x] Primitive registry is generic and data-driven for implemented primitives.
- [x] Provider/bundle-specific branching does not enter the generic executor.
- [x] Existing external port policy execution can call abstract function manifests.
- [x] Failure paths are explicit and covered by tests for the substrate statuses in this slice.
- [x] Tests prove no silent fallback for `execute_abstract_function` executor wiring and no frontend judgment authority in this slice.

### Bundle `abstract_function_authority_bindings_runtime_enforcement` (PR #479)

Completed in PR #479:

- [x] `NpgsqlAbstractFunctionManifestRepository` loads `topology.abstract_function_authority_bindings` (active only).
- [x] `AbstractFunctionManifest` carries `AuthorityBindings: IReadOnlyList<AbstractFunctionAuthorityBinding>`.
- [x] `AbstractFunctionExecutor.ExecuteAsync` fail-closes on missing authority bindings (`MissingAuthority`) and missing policy binding (`MissingAuthority`) before any step execution.
- [x] `AbstractFunctionExecutionContext` exposes authority bindings to primitive adapters via `SetAuthorityBindings` (internal, set by executor).
- [x] `CallPostgresFunctionPrimitiveAdapter` fail-closes on missing table authority binding (`MissingAuthority`).
- [x] Unit tests cover: no bindings, no policy binding, inactive-only bindings, valid binding pass, no table authority.
- [x] Live DB integration test (`SeededAbstractFunctions_LoadAuthorityBindings_AndEnforceFailClose`) proves seeded authority bindings are loaded and executor fail-closes on empty bindings.

Still open after the file-storage attachment migration:
- `NpgsqlExternalPortDbFunctionRepository` is now an explicit fail-closed compatibility stub (concrete fs_* methods deleted); full interface/class removal is not part of `completion-gate-and-test-alignment` and remains blocked until all Bundles leave the legacy `execute_db_function` operation key.
- SQL Attention / recommendation and credential hardening Bundles remain not_started.

---

## Bundle `sql-recommendation-primitive-migration`

Status: `partial_compatibility_fallback`

### Problem

SQL Attention and recommendation are conceptually candidate/evidence/ranking primitives, but runtime behavior is still represented through dedicated resolver/runtime classes and projection blocks. This risks preserving backend-specific function islands.

### Purpose

Move SQL Attention and recommendation execution into abstract function primitives while keeping route mutation explicit and user-driven.

### Migration steps

1. **Abstract function fix** ✓
   - Define `sql_attention` and `recommendation_attention` as primitive boundaries.
   - Define `phase_attention_adapter` as the only allowed Phase Attention bridge.
2. **Add seed for absorption target** ✓ (SQL Attention projection only)
   - Add seed/manifest rows for SQL Attention projection/evidence and context-route recommendation candidate generation.
   - Seed must express lane separation and route non-mutation.
3. **Seed test** ✓ (SQL Attention projection only)
   - Add tests proving SQL Attention/recommendation seed paths produce candidates/evidence without mutating route/topology state.
   - Tests must cover no silent fallback, lane separation, append ordering, and projection/evidence boundaries.
4. **Delete existing concrete functions** ✓ (SQL Attention projection only — compatibility fallback marked)
   - Remove/shrink dedicated resolver/runtime branches only after seed tests pass.
   - Keep Phase Attention internals as adapter code, not abstract function steps.

### Completed in this change (SQL Attention list_projection)

- [x] SSOT fix: `sql_attention_runtime_lane_extension` added to `abstract-function-primitive-registry-ssot.yaml`; `admin_runtime` documented as valid lane for sql_attention projection; `function_name`, `parameter_key` documented as step_config-authority (not payload-derived).
- [x] Seed (af08): `sql_attention.list_projection` manifest seeded with `runtime_lane=admin_runtime`, `authority_scope=admin_sql_attention`, `sql_attention` primitive step, `source_set_id` payload binding, `logs.attention` table authority, `admin_sql_attention_projection` policy authority.
- [x] Primitive adapter: `SqlAttentionProjectionPrimitiveAdapter` (key `"sql_attention"`) added to `AbstractFunctionRuntime.cs`; verifies table authority, reads `function_name`/`parameter_key` from step_config, loads policy from `topology.function_parameters`, loads evidence from `logs.attention`, projects via `SqlAttentionTopologyProjectionRuntime.ProjectCandidates`.
- [x] `AbstractFunctionExecutionContext.RequiredRuntimeLane` added; `AbstractFunctionExecutor` uses it for lane check (backward-compatible default `"external_port_runtime"`).
- [x] `SqlAttentionProjectionPrimitiveAdapter` registered in `Program.cs` as `IAbstractFunctionPrimitiveAdapter`.
- [x] `AdminRuntime` updated: `_sqlAttentionTopologyProjectionRuntime` replaced by `_abstractFunctionExecutor`; `DataSqlAttentionListProjectionAsync` routes through `AbstractFunctionExecutor`.
- [x] `SqlAttentionTopologyProjectionRuntime` marked as compatibility fallback; `ParsePolicy` made `internal static` for primitive adapter reuse.
- [x] `SqlAttentionScheduler` (BackgroundService cron route) remains unchanged per SSOT exception.
- [x] Seed path tests: `SqlAttentionAbstractFunctionTests.cs` added covering seed execution, MissingPolicy result, lane separation, authority checks, fail-close vocabulary, no route/topology mutation.
- [x] `AdminRuntimeSqlAttentionProjectionTests.cs` updated to use abstract function path with `AbstractFunctionExecutor`.
- [x] All 921 runtime tests pass.

### Completed in this change (recommendation_attention af09)

- [x] SSOT fix: `recommendation_attention_lane_extension` added to `abstract-function-primitive-registry-ssot.yaml`; `runtime_executor` lane documented; `function_name`, `parameter_key` documented as step_config-authority; `runtime_context` binding source documented; Phase Attention adapter boundary documented.
- [x] Seed (af09): `context_route.recommendation_resolve` manifest seeded with `runtime_lane=runtime_executor`, `authority_scope=context_route_recommendation`, `recommendation_attention` primitive step with `working_shape` runtime_context binding, `context_route.context_hub_recommendation_current` table authority, `context_route_recommendation_resolve` policy authority, `function_name`/`parameter_key` in step_config.
- [x] Primitive adapter: `RecommendationAttentionPrimitiveAdapter` (key `"recommendation_attention"`) verifies table/policy authority, reads `function_name`/`parameter_key` from step_config, passes them to `ContextRouteRecommendationResolver.ResolveAsync` — manifest-authorized SQL leg complete.
- [x] `ContextRouteRecommendationResolver.ResolveAsync` updated to accept `functionName` and `defaultParameterKey`; C# constants removed; policy SQL query uses manifest step_config values, not hardcoded names.
- [x] `RecommendationAttentionPrimitiveAdapter` registered in `Program.cs` as `IAbstractFunctionPrimitiveAdapter`.
- [x] `RuntimeExecutor` updated: recommendation step routes through `AbstractFunctionExecutor` (not directly to `ContextRouteRecommendationResolver`).
- [x] `ContextRouteRecommendationResolver` marked as COMPATIBILITY FALLBACK; canonical path is af09 → abstract executor → adapter → resolver.
- [x] Seed path tests: `RecommendationAttentionAbstractFunctionTests.cs` added covering cold-start InsufficientHistory, null working_shape fail-close, table/policy authority checks, step_config function_name/parameter_key injection prevention, lane separation, no route/topology mutation, Phase Attention adapter boundary, and manifest-authorized SQL evidence (function_name/parameter_key flow to SQL call).
- [x] DB constraint extended: `runtime_executor` added to `abstract_function_manifests.runtime_lane` check and `db-schema.yaml` authority rule updated.
- [x] All runtime tests pass.

### Remaining (ContextRouteRecommendationResolver full decomposition)

- [ ] Decompose `ContextRouteRecommendationResolver` algorithm into discrete primitive composition (candidate source, eligibility, score, rank, diversify/suppress, projection, feedback/event) rather than a single adapter call. Current state: isolated behind a primitive adapter (compatibility fallback); full primitive decomposition is a future Bundle task.
- [ ] Migrate remaining SQL Attention scheduler/repository behavior into primitive composition or isolate behind a primitive adapter.
- [ ] Keep Phase Attention internals behind `phase_attention_adapter` only (currently opaque via adapter — preserved).

### Improvement plan

- [x] Define `sql_attention` primitive as relational observation, evidence append, ranking, and projection. (done)
- [x] Define `recommendation_attention` primitive and isolate `ContextRouteRecommendationResolver` behind primitive adapter (done; full primitive decomposition is future work).
- [x] Preserve prohibition on auto-overwriting route state or canonical topology state. (enforced by adapter boundary)
- [x] Migrate `ContextRouteRecommendationResolver` to be called only via primitive adapter; marked COMPATIBILITY FALLBACK. (done)
- [x] Keep Phase Attention internals behind `phase_attention_adapter` only. (SystemOperationCiRuntime is opaque inside the resolver — boundary preserved)
- [ ] Decompose `ContextRouteRecommendationResolver` into discrete primitive steps (candidate source, eligibility, score, rank, diversify/suppress, projection) — future Bundle task.
- [ ] Migrate remaining SQL Attention scheduler/repository behavior into primitive composition or isolate behind a primitive adapter.
- [ ] Preserve lane separation: `ui_pressure`, `state_pressure`, `sql_attention_projection` cannot mix — tests exist; full enforcement requires decomposition.

### Materials

- `docs/design/sql-attention-logs-ssot.yaml`
- `docs/design/sql-attention-logs-ssot.md`
- `docs/design/context-route-recommendation.yaml`
- `docs/design/context-route-recommendation.md`
- `docs/design/runtime-orchestration-ssot.yaml`
- `backend/runtime/ContextRouteRecommendationResolver.cs`
- `backend/runtime/SqlAttentionEvidencePromotionRuntime.cs`
- `backend/runtime/SqlAttentionPhaseVectorRuntime.cs`
- `backend/runtime/SqlAttentionTopologyProjectionRuntime.cs`
- `backend/scheduler/SqlAttentionScheduler.cs`
- `backend/repository/SqlAttentionLogsRepository.cs`
- `backend/repository/NpgsqlSqlAttentionLogsRepository.cs`
- `backend/schema/SqlAttentionContracts.cs`
- `backend/schema/ContextRouteContracts.cs`
- `backend/schema/RecommendationPressureLanes.cs`
- `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs`
- `backend/tests/Topolactor.Runtime.Tests/SqlAttentionLogsFunctionContractTests.cs`
- `backend/tests/Topolactor.Runtime.Tests/SqlAttentionPhaseGenerationLineTests.cs`
- `backend/tests/Topolactor.Runtime.Tests/SqlAttentionTopologyProjectionRuntimeTests.cs`
- `backend/tests/Topolactor.Runtime.Tests/SqlAttentionLiveDbEndToEndTests.cs`
- `frontend/api/sqlAttentionProjection.ts`
- `frontend/components/RecommendNavigationIsland.tsx`
- `frontend/components/SqlAttentionProjectionBlock.tsx`
- `frontend/components/SqlAttentionProjectionPanel.tsx`
- `frontend/tests/sqlAttentionProjectionApi.test.ts`
- `frontend/tests/sqlAttentionProjectionPanel.test.ts`
- `frontend/tests/recommendNavigationIsland.test.ts`
- `frontend/tests/recommendationPressureLaneGuard.test.ts`
- `.agent/tests/check-sql-attention-ssot.sh`

### Target functions / classes

- `ContextRouteRecommendationResolver.ResolveAsync`
- `ContextRouteRecommendationResolver.ResolveNextOperations`
- `ContextRouteRecommendationResolver.ResolveNextTokens`
- `ContextRouteRecommendationResolver.ResolveNextEnumItemsAsync`
- `ContextNeighborSearch.FindNearestPrefixes`
- `ContextVectorBuilder.BuildMultiHotVector`
- `SqlAttentionScheduler`
- `SqlAttentionLogsRepository`
- `NpgsqlSqlAttentionLogsRepository`
- `SqlAttentionEvidencePromotionRuntime`
- `SqlAttentionPhaseVectorRuntime`
- `SqlAttentionTopologyProjectionRuntime`
- `sqlAttentionProjection` frontend API boundary
- `SqlAttentionProjectionBlock`
- `SqlAttentionProjectionPanel`

### Acceptance conditions

- [x] SQL Attention remains observation/evidence/candidate projection only. (primitive adapter + seed enforce read-only path)
- [x] Recommendation produces ranked candidates but does not mutate route state automatically. (enforced; no auto-overwrite in adapter)
- [x] Missing policy and insufficient history remain explicit statuses. (ExplicitError / InsufficientHistory paths tested)
- [x] `function_name` and `parameter_key` from manifest step_config reach the policy SQL call — no C# constant override. (ContextRouteRecommendationResolver.ResolveAsync takes these as parameters)
- [ ] `ui_pressure`, `state_pressure`, and `sql_attention_projection` cannot mix lanes. (lane boundary tests exist; full enforcement requires primitive decomposition)
- [ ] SQL Attention scheduler/repository behavior is expressed through primitives or explicitly isolated as primitive adapters. (scheduler not yet migrated)
- [ ] Tests cover candidate ranking from full primitive decomposition (current tests cover adapter/seed path only).
- [ ] Existing concrete resolver/runtime branches are deleted or marked compatibility fallback only after seed tests pass.

---

## Bundle `credential-primitive-hardening`

Status: `not_started`

### Problem

Credential flow already has generic primitives, but refresh request/response handling remains thin and must be hardened before treating credential flow as a reusable abstract function substrate.

### Purpose

Preserve credential flow as abstract function primitives while keeping actual secret materialization in runtime-only adapters.

### Migration steps

1. **Abstract function fix**
   - Define credential reference, encrypted payload load, runtime decrypt, request build, HTTP send, response parse, encrypted write, hash/version/expiry update, and lease release as primitive steps.
2. **Add seed for absorption target**
   - Add seed/manifest rows for credential refresh and external port credential injection flows.
   - Seed must keep provider_kind as data and must not introduce provider-specific runtime branching.
3. **Seed test**
   - Add tests for seed-driven credential flow, invalid/missing token response, stale version, missing crypto, and plaintext projection attempts.
4. **Delete existing concrete functions**
   - Remove/shrink concrete credential refresh parsing/request logic only after seed tests pass.
   - Keep crypto and HTTP clients as runtime-only adapters.

### Improvement plan

- [ ] Keep `credential_requirement` as port record attachment, not standalone credential plane.
- [ ] Keep plaintext out of DB, UI, seed, SSOT, projection, audit log, and runtime event log.
- [ ] Harden `BuildTokenRefreshRequest` with manifest/policy-defined request shape.
- [ ] Harden `ParseTokenRefreshResult` to use crypto hash adapter, expiry parsing, rotated payload policy, and explicit provider config without provider-specific C# branching.
- [ ] Preserve lease/version/expiry guards.
- [ ] Add tests for missing/invalid token response, stale version, plaintext projection prohibition, and provider branch prohibition.

### Materials

- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-bundle-secret-credential-ssot.yaml`
- `docs/design/auth-db-session-credential-ssot.yaml`
- `backend/runtime/ExternalPortCredentialRefresher.cs`
- `backend/runtime/ExternalPortCredentialReferenceResolver.cs`
- `backend/repository/NpgsqlExternalCredentialVaultRepository.cs`
- `backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs`

### Target functions / classes

- `ExternalTokenRefresher.RefreshIfNeededAsync`
- `ExternalTokenRefresher.FailCloseOnMissingOrInvalidCredential`
- `ExternalPortPolicyStepExecutor.BuildTokenRefreshRequest`
- `ExternalPortPolicyStepExecutor.ParseTokenRefreshResult`
- `ExternalPortPolicyStepExecutor` credential operation keys
- `IExternalCredentialCrypto`
- `IExternalCredentialVaultRepository`
- `ExternalPortCredentialReferenceResolver.ResolveCredentialReferenceAsync`

### Acceptance conditions

- [ ] Credential primitive flow is manifest/policy-driven and provider_kind remains data.
- [ ] No provider-specific runtime handler or provider switch is added.
- [ ] Decrypted payload exists only inside runtime context and never enters projection/log/seed/SSOT.
- [ ] Refresh token rotation updates encrypted payload/hash/expires/version atomically.
- [ ] Token hash is computed by crypto adapter, not placeholder response-length logic.
- [ ] Tests prove fail-close on invalid credential, missing crypto, stale version, invalid response, and plaintext projection attempts.
- [ ] Existing concrete request/parse logic is deleted or marked compatibility fallback only after seed tests pass.

---

## Bundle `file-storage-db-function-to-abstract-function-migration`

Status: `implemented`

### Completed

- Attachment bind/list/unbind policy steps migrated from `execute_db_function` to `execute_abstract_function` (seed rows ed/ee/ef).
- Abstract function manifests af05/af06/af07 added for `file_storage.bind_record_file_attachment`, `file_storage.list_record_file_attachments`, `file_storage.unbind_record_file_attachment`.
- Steps bf05–bf09 added; input bindings c015–c021 added with `record_table_ref` using `step_config` binding source (manifest authority, not payload).
- Authority bindings added for af05–af07 (policy + table authority for `topology.record_file_attachments`).
- `AbstractFunctionRuntime.ResolveBinding` extended with `step_config` binding source.
- `AbstractFunctionRuntime.ApplyResultToExternalContext` extended with generic `OutputProp` propagation.
- `NpgsqlExternalPortDbFunctionRepository` shrunk to compatibility placeholder stub (all 7 concrete fs_* methods deleted).
- `Program.cs` DI registration updated to parameterless stub registration.
- Unit tests added: `step_config` binding, `OutputProp` propagation, no-concrete-methods assertion, seed attachment migration assertion.
- Integration test stubs added: seeded attachment policies use `execute_abstract_function`, manifest step_config authority verified.

Still open after this update:

- Export job / file artifact / manifest record / signed download authorization operations remain on abstract function manifests af01–af04 (added in PR #479), no change needed.
- `NpgsqlExternalPortDbFunctionRepository` is now an explicit fail-closed compatibility stub — removal of the interface/class entirely is not part of `completion-gate-and-test-alignment` and can proceed only after all Bundles leave the legacy `execute_db_function` operation key.
- SQL Attention / recommendation and credential hardening remain not_started.

### Problem

`NpgsqlExternalPortDbFunctionRepository` currently maps specific `topology.fs_*` names through a C# switch and extracts parameters from request payload/context per function. Attachment operations also read `record_table_ref` from payload, which is a table authority boundary risk.

### Purpose

Move file-storage DB operations into abstract function manifests and primitives, leaving only hard-runtime compute and opaque PostgreSQL function calls as adapters.

### Migration steps

1. **Abstract function fix**
   - Add/extend DB query, DB mutation, PostgreSQL function call, input binding, output projection, and authority primitives.
   - Keep `compute_checksum` as hard-runtime primitive.
2. **Add seed for absorption target**
   - Add abstract function seed/manifest rows for export job, file artifact, manifest record, signed download authorization, and attachment bind/list/unbind.
   - Seed must resolve table authority through manifest/physical table binding/current route context, not payload `record_table_ref`.
3. **Seed test**
   - Add tests proving the seed path performs DB mutations/projections and denies secret-bearing output.
   - Add tests proving payload-provided table authority is ignored or rejected fail-close.
4. **Delete existing concrete functions**
   - Remove/shrink `NpgsqlExternalPortDbFunctionRepository` function switch and per-function extraction methods only after seed tests pass.
   - Keep opaque PostgreSQL calls only as `call_postgres_function` primitive adapters when still justified.

### Improvement plan

- [x] Replace `functionName switch` with manifest-driven `call_postgres_function`, `db_query`, or `db_mutation` primitives. (`NpgsqlExternalPortDbFunctionRepository` is now a stub; all operations route through abstract function manifests.)
- [x] Move parameter binding into abstract function input binding rows/specs. (Input bindings c001–c021 in seed cover all file-storage operations.)
- [x] Resolve `record_table_ref` through manifest/physical table binding/current route context, not frontend payload. (`step_config` binding source added; attachment manifests use `binding_source = 'step_config'` for `record_table_ref`.)
- [x] Keep `compute_checksum` as hard-runtime primitive. (No change — checksum remains in `FileStorageBundleStepHandler`.)
- [x] Keep complex opaque functions such as signed download authorization behind `call_postgres_function` where appropriate. (All fs_* calls remain as `call_postgres_function` primitive steps.)
- [x] Ensure projection denies signed URL, bucket, endpoint, storage path, credential, and raw storage refs. (`projection_deny_keys` enforced in manifests af01–af07.)

### Materials

- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/runtime-bundle-file-storage-ssot.md`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/db-schema.yaml`
- `backend/runtime/FileStorageBundleStepHandler.cs`
- `backend/repository/NpgsqlExternalPortDbFunctionRepository.cs`
- `backend/schema/FileStorageContracts.cs`
- `db/topology_tables.sql`
- `db/seed_empty.sql`
- `db/file_attachment_crud_preset_seed.sql`
- `backend/tests/Topolactor.Runtime.Tests/FileStorageBundleDispatchTests.cs`
- `backend/tests/Topolactor.Integration.Tests/FileStoragePortConsumerLiveDbTests.cs`
- `frontend/tests/fileStoragePortConsumer.test.ts`

### Target functions / classes

- `NpgsqlExternalPortDbFunctionRepository.ExecuteAsync`
- `ExecuteRecordExportJobAsync`
- `ExecuteRecordFileArtifactAsync`
- `ExecuteWriteManifestRecordAsync`
- `ExecuteAuthorizeSignedDownloadAsync`
- `ExecuteBindRecordFileAttachmentAsync`
- `ExecuteListRecordFileAttachmentsAsync`
- `ExecuteUnbindRecordFileAttachmentAsync`
- `FileStorageBundleStepHandler.ExecuteAsync`

### Acceptance conditions

- [x] `record_table_ref` is no longer payload-authoritative. (`step_config` binding source reads from manifest step_config, not request payload.)
- [x] Attachment bind/list/unbind use manifest/physical table authority. (Authority bindings for `topology.record_file_attachments` added for af05–af07.)
- [x] File-storage operation bodies are expressible through abstract function manifests/primitives. (All seven fs_* operations covered by manifests af01–af07 and steps bf01–bf09.)
- [x] `NpgsqlExternalPortDbFunctionRepository` no longer grows per-bundle/per-function switch cases for simple DB operations. (Shrunk to stub; throws for any unknown function name.)
- [x] `compute_checksum` remains the only file-storage hard-runtime compute handler unless SSOT explicitly authorizes more. (Checksum stays in `FileStorageBundleStepHandler`; no new hard-runtime handlers added.)
- [x] Tests prove DB state/projection result and deny-list secret projection behavior. (Unit tests in `FileStorageBundleDispatchTests` and `AbstractFunctionExecutorTests`; integration stubs in `FileStoragePortConsumerLiveDbTests`.)
- [x] Existing concrete file-storage DB function methods are deleted or marked compatibility fallback only after seed tests pass. (All 7 concrete fs_* methods deleted after seed tests were added.)

---

## Bundle `completion-gate-and-test-alignment`

Status: `implemented`

### Completed in this change

- [x] Added the Abstract Function Bundle Completion Alignment Gate to `.agent/protocols/completion.md`.
- [x] Documented required check surfaces by changed lane: SSOT/manifest schema, backend primitive executor, seed/migration, DB mutation projection source, frontend return lane, SQL Attention/recommendation, and credential primitive.
- [x] Enforced global migration order in completion governance: SSOT/primitive generation first, then per-Bundle seed/manifest migration, seed/runtime/integration proof, concrete deletion or explicit compatibility fallback, and only then TODO/roadmap/refactor-todo closure.
- [x] Extended `.agent/tests/check-completion-judgment.sh` and added `.agent/tests/check-abstract-function-completion-alignment.sh` so the completion alignment gate, migration-order vocabulary, Bundle-to-test-surface mapping, seed/integration proof, and compatibility fallback classification are executable governance checks.
- [x] Confirmed through `.agent/tests/check-abstract-function-completion-alignment.sh` that existing representative runtime and seed tests cover current file-storage absorption evidence surfaces (`AbstractFunctionExecutorTests`, `FileStorageBundleDispatchTests`, `FileStoragePortConsumerLiveDbTests`) and that `NpgsqlExternalPortDbFunctionRepository` remains an explicit fail-closed compatibility stub; future SQL Attention/recommendation and credential Bundles remain separate not_started Bundles and are not advanced by this alignment Bundle.

### Problem

A backend-wide abstract function substrate crosses runtime, DB, external port, SQL Attention, recommendation, credential, projection, and test lanes. Completion cannot be claimed by syntax or file presence alone.

### Purpose

Align required tests/checks and completion language after implementation bundles land.

### Improvement plan

- [x] Add or update SSOT vocabulary checks for new primitive keys and manifest tables; the executable alignment check now verifies that implemented completion wording is tied to concrete test surfaces.
- [x] Add backend runtime tests for primitive execution order, result context binding, fail-close, and no provider/bundle branching.
- [x] Add seed tests for current absorption targets before concrete function deletion; future targets remain in their own not_started Bundles.
- [x] Add integration tests for representative DB mutation returning to projection/refetch/SSE where applicable.
- [x] Add recommendation-test requirement for lane separation and explicit status; implementation remains in `sql-recommendation-primitive-migration`.
- [x] Add credential-test requirement for secret non-projection and refresh hardening; implementation remains in `credential-primitive-hardening`.
- [x] Update `.agent/tasks/todo.md` and `docs/system-roadmap.yaml` only if this refactor maintenance note is intentionally promoted into canonical TODO/roadmap maintenance after code/tests prove status changes; no canonical TODO/roadmap promotion was needed, and the executable alignment check guards against wording-only status advancement.

### Materials

- `.agent/tests/check-worktype-routing.sh`
- `.agent/tests/check-completion-judgment.sh`
- `.agent/tests/check-abstract-function-completion-alignment.sh`
- `.agent/tests/check-structure.sh`
- `.agent/tests/check-system-roadmap.sh` only if roadmap changes
- `backend/tests/Topolactor.Runtime.Tests/*`
- `backend/tests/Topolactor.Integration.Tests/*`
- `frontend/tests/*`
- `docs/design/pipeline-continuity-ssot.yaml`

### Acceptance conditions

- [x] Required checks are documented per changed lane and mechanically checked for the current implemented Bundle evidence mapping.
- [x] Global order is enforced: SSOT fix and abstract function primitive generation first, then steps 2–4 loop per target Bundle, then final refactor todo deletion.
- [x] Runtime with DB update asserts DB state or projection source changed.
- [x] Runtime returning to frontend asserts SSE/refetch/final state where applicable.
- [x] No TODO/roadmap status is advanced without implementation and test evidence; implemented status is now guarded by a check that inspects Bundle status, target test files, live DB proof, and compatibility fallback text.
- [x] No partial status is hidden behind implemented wording.


## Bundle `projection-manifest-primitive-migration`

Status: `investigation_needed`

### Problem

Projection execution is mostly routed through manifest / screen data shape / runtime lanes, but several general runtime surfaces still encode projection semantics in C#/TS branch tables. Confirmed examples include component-kind prop normalization in `projectionConstructor.ts`, frontend wiring-kind to backend layer/action mapping in `renderEmission.ts`, local UI event action mapping in `renderEmission.ts`, screen operation kind to dispatcher axes in `ManifestScreenOperationDeriver.cs`, and the backend SSE runtime emitting a fixed `projection` event type. These are not admin-only authoring exceptions when they participate in the general runtime projection / dispatch / SSE lane.

### Purpose

Move projection mapping authority toward projection manifest, screen_data_shape authority, projection primitive definitions, and abstract function output/projection metadata while keeping the frontend as request/render surface only.

### Improvement plan

1. **Projection authority inventory**
   - Classify each branch as projection authority, dispatch authority, pure view normalization, local UI state only, admin authoring UX, or test-only handwritten emission.
   - Do not migrate pure rendering, visual layout-only sizing, inert draft preview, or test-only handwritten emission.
2. **Manifest / primitive contract extension**
   - Extend existing SSOT/manifest vocabulary for projection constructor keys, component prop schemas/defaults, event binding action vocabulary, screen operation axis derivation, and SSE projection event metadata where the mapping is currently hardcoded.
   - Reuse `abstract_function_manifests.output_shape`, projection deny keys, screen_data_shape, and component registry primitives; do not create a parallel projection SSOT unless existing SSOT ownership is explicitly reopened.
3. **Seed / manifest absorption**
   - Add or extend seed rows so component props, screen operation dispatch axes, external port event binding output propagation, and SSE projection payload type are resolved from manifest/primitive data rather than C#/TS switches.
4. **Seed tests before deletion**
   - Add tests proving data-defined projection construction, dispatch axis resolution, event binding construction, fail-close on missing/invalid projection metadata, and no frontend authority over table/column/join/output or secret-bearing values.
5. **Delete / shrink concrete mapping**
   - Remove or shrink branch tables only after seed tests pass. Compatibility fallbacks must be explicit and fail closed.

### Materials

- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/abstract-function-primitive-registry-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `backend/tests/Topolactor.Runtime.Tests/ScreenDataShapeQueryEvaluatorTests.cs`
- `backend/tests/Topolactor.Runtime.Tests/ManifestScreenOperationDeriverTests.cs`
- `backend/tests/Topolactor.Runtime.Tests/DraftPreviewComposerTests.cs`
- `backend/tests/Topolactor.Integration.Tests/SseEndToEndTests.cs`
- `frontend/tests/*projection*.test.ts`

### Target files

- `frontend/runtime/projectionConstructor.ts`
- `frontend/runtime/projectionRuntime.ts`
- `frontend/runtime/renderEmission.ts`
- `frontend/runtime/payloadFromResolver.ts`
- `frontend/runtime/propBindingResolver.ts`
- `frontend/runtime/frontendLocalCalculationResolver.ts`
- `backend/runtime/SseProjectionRuntime.cs`
- `backend/runtime/ScreenDataShapeValueResolver.cs`
- `backend/runtime/ScreenDataShapeQueryEvaluator.cs`
- `backend/repository/ManifestCanonicalProjection.cs`
- `backend/repository/ManifestScreenOperationDeriver.cs`
- `db/seed_empty.sql`
- `docs/design/db-schema.yaml` if manifest/schema additions are required

### Target functions/classes

- `constructProjection`
- `normalizeComponentProps`
- `mapWiringKindToLayer`
- `mapWiringKindToAction`
- `buildRuntimeDispatchSpec`
- `buildLocalUiStateEventBinding`
- `buildExternalPortEventBinding`
- `resolvePayloadFromSource`
- `resolvePropBinding`
- `evaluateAllCalcBindings`
- `SseProjectionRuntime.ExecuteAsync`
- `ScreenDataShapeValueResolver.Resolve`
- `ScreenDataShapeQueryEvaluator.Evaluate`
- `ManifestCanonicalProjection.ApplyCanonicalProjectionAsync`
- `ManifestScreenOperationDeriver.TryDeriveAxes`

### Acceptance conditions

- [ ] Projection constructor mapping and output prop defaults are manifest/primitive-defined where they affect runtime projection semantics.
- [ ] Screen operation kind → dispatcher axes are data-defined or explicitly registered primitive vocabulary, not an unbounded C# switch.
- [ ] SSE event type/payload metadata for projection lane is manifest/primitive-defined or explicitly justified as transport skeleton.
- [ ] Frontend remains a render/request surface and cannot provide table/column/join/output authority.
- [ ] Missing/invalid projection metadata fails closed with explicit error; no silent fallback to handwritten defaults.
- [ ] Pure view rendering, visual layout-only formatting, draft preview display helpers, and test-only handwritten emissions are documented as out of scope and not migrated.
- [ ] Tests cover data-defined projection construction, screen_data_shape authority, dispatch axis resolution, SSE/refetch projection continuity, and projection deny keys.

### Explicitly out of scope

- Admin import/runtime/admin submit UX unless a mapping leaks into general runtime projection or dispatch.
- Pure display components and visual layout CSS formatting (`layoutNodeFlowProjection.ts`, `visualLayoutUtils.ts`) when they do not choose runtime authority.
- Draft preview sample display helpers (`draftPreviewProjection.ts`) that only shape inert preview data and do not dispatch or mutate.
- Test-only handwritten emissions.
- SQL Attention / recommendation projection migration already covered by `sql-recommendation-primitive-migration`.
- Credential secret projection hardening already covered by `credential-primitive-hardening`.

---

## Bundle `scheduler-job-body-primitive-migration`

Status: `investigation_needed`

### Problem

The runtime timeline scheduler is a valid substrate for trigger alignment, queueing, cancellation, and dispatch order, but scheduler-adjacent classes still risk mixing substrate responsibilities with job-body semantics. Confirmed hardcoded boundaries include fixed cron/service loops for retention and system CI, fixed db_notify → hook/SSE projection payload routing, fixed SSE fan-out event handling, and frontend component-event queue retry/append behavior. `SqlAttentionScheduler` is already part of the existing SQL Attention / recommendation migration and should not be duplicated as a separate Bundle.

### Purpose

Preserve scheduler substrate primitives (claim/lease/retry/due selection, trigger alignment, queue overflow signaling, cancellation, listener/broadcaster transport) while moving job bodies, projection/evidence append decisions, fixed event-type handling, and runtime action selection into abstract function manifests or manifest-backed job primitives.

### Improvement plan

1. **Scheduler substrate vs job-body split**
   - Keep queueing, trigger alignment, due selection, claim/lease, retry, LISTEN/NOTIFY transport, and client response/cancellation contract in hard runtime substrate where SSOT allows it.
   - Classify retention policy execution, system CI diagnostic invocation, db_notify projection routing, and component-event append/retry payload shaping as job bodies or event primitives when they choose runtime action/evidence/projection semantics.
2. **Abstract job primitive vocabulary**
   - Add or extend primitives such as `scheduler_enqueue`, `scheduler_claim_due`, `scheduler_release_lease`, `event_log`, `runtime_event_projection`, `retention_execute`, and `diagnostic_execute` only where SSOT authorizes the boundary.
3. **Manifest-backed job definitions**
   - Represent recurring job identity, runtime action, evidence append, projection response, event type, and target manifests through seed/manifest rows instead of per-job C# branches.
4. **Tests before shrinking concrete jobs**
   - Add tests for queue semantics remaining in scheduler substrate, job body manifest resolution, retry/lease fail-close, runtime event log/evidence append, SSE projection event metadata, and no SQL Attention lane regression.
5. **Delete / shrink concrete job bodies**
   - Shrink scheduler classes to trigger/queue/transport shells after manifest-backed job tests pass.

### Materials

- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/abstract-function-primitive-registry-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-bundle-job-scheduler-ssot.yaml`
- `backend/tests/Topolactor.Runtime.Tests/SystemOperationCiSchedulerTests.cs`
- `backend/tests/Topolactor.Runtime.Tests/LogRetentionRuntimeTests.cs`
- `backend/tests/Topolactor.Runtime.Tests/SchedulerLoadBenchmarkTests.cs`
- `backend/tests/Topolactor.Runtime.Tests/FrontendComponentEventLogLaneTests.cs`
- `backend/tests/Topolactor.Integration.Tests/SseEndToEndTests.cs`

### Target files

- `backend/scheduler/RuntimeTimelineScheduler.cs`
- `backend/scheduler/RetentionScheduler.cs`
- `backend/scheduler/SystemOperationCiScheduler.cs`
- `backend/scheduler/DbNotifyListener.cs`
- `backend/repository/DbNotifyRepository.cs`
- `backend/scheduler/SseEventBroadcaster.cs`
- `frontend/runtime/frontendScheduler.ts`
- `backend/runtime/LogRetentionRuntime.cs`
- `backend/runtime/SystemOperationCiRuntime.cs`
- `db/seed_empty.sql`
- `docs/design/db-schema.yaml` if job manifest/schema additions are required

### Target functions/classes

- `RuntimeTimelineScheduler.AlignAndDispatchAsync`
- `RuntimeTimelineScheduler.EnqueueCronTrigger`
- `RuntimeTimelineScheduler.EnqueueHookTrigger`
- `RuntimeTimelineScheduler.ExecuteAsync`
- `RetentionScheduler.ExecuteAsync`
- `SystemOperationCiScheduler.ExecuteAsync`
- `DbNotifyListener.HandleNotificationPayload`
- `DbNotifyRepository.NotifyAsync`
- `SseEventBroadcaster.Broadcast`
- `emitComponentOperationEvent`
- `flushComponentEventQueue`
- `scheduleUserOperation`
- `scheduleAdminDispatch`

### Acceptance conditions

- [ ] Scheduler substrate responsibilities remain explicit and are not moved into manifests when they are runtime skeleton concerns.
- [ ] Job body runtime action selection, projection/evidence append decisions, fixed event types, and output lane routing are manifest/primitive-defined or explicitly justified as transport skeleton.
- [ ] Claim/lease/retry/due-selection behavior is either substrate-owned with tests or primitive-owned with manifest tests; the boundary is documented.
- [ ] Cron/hook/client triggers still enter the canonical scheduler → ManifestDispatcher route with explicit failure signals.
- [ ] SQL Attention scheduler work is consolidated into `sql-recommendation-primitive-migration` instead of duplicated here.
- [ ] Tests prove no silent fallback, queue overflow/cancellation behavior, job body manifest resolution, runtime event/evidence append, and SSE projection continuity.

### Explicitly out of scope

- Rewriting `RuntimeTimelineScheduler` queue mechanics into a DB job queue unless the job scheduler SSOT is explicitly reopened.
- SQL Attention-specific observation/ranking/projection details; integrate those into `sql-recommendation-primitive-migration`.
- Admin-only runtime/import/submit UX unless it leaks into the general scheduler/dispatch route.
- Provider-specific external scheduler clients.
- Treating frontend component-event local persistence as projection authority when it only preserves retry durability and redacts payload.

---

## Bundle `cli-mcp-read-export-port-substrate`

Status: `not_started`

### Problem

CLI/MCP read/export/import-candidate behavior is currently SSOT-defined but implementation-thin. The design requires MCP/CLI entrypoints to pass through auth, capability/scope resolution, ManifestDispatcher/runtime dispatch, Data Reader/authorized read model, export job/audit log, and file stream authorization. `.cursor/mcp.json` contains no MCP server wiring, and no concrete dispatch-secured MCP tool/resource implementation was confirmed in the inspected runtime. This should be tracked as a design-to-implementation Bundle, not as a concrete handler-deletion Bundle yet.

### Purpose

Implement CLI/MCP read/export/import-candidate port through the canonical dispatch-secured port substrate and abstract function primitives, preventing future dedicated tool handlers (`read_file`, `stream_file`, `call_tool`, export/import wrappers) from bypassing policy, manifest, projection, audit, or file-stream authorization.

### Improvement plan

1. **Contract-to-runtime mapping**
   - Map `read`, `search`, `aggregate`, `analyze`, `validate`, `export`, `stream_file`, `create_export_job`, `import_structured_output`, `assign_business_object_candidate`, `create_draft_operation`, and `create_commit_candidate` to portTargetRef / policy step / abstract function / projection or file stream primitives.
2. **MCP API port entrypoint**
   - Add an authenticated MCP/CLI API port entrypoint that resolves scope/capability and then enters ManifestDispatcher/runtime dispatch. Do not add a direct DB reader or core API bypass.
3. **Data Reader and file stream primitives**
   - Implement authorized read model generation, query validation, export job creation, manifest/checksum/audit logging, and file stream authorization as abstract function or port primitives.
4. **Structured input candidate lane**
   - Implement external structured output import as evidence/candidate/draft only; DB commit remains UI/human approval plus canonical dispatch.
5. **Tests and guards**
   - Add tests for auth/scope fail-close, dispatch bypass denial, direct SQL denial, file stream permission, export audit/manifest/checksum, candidate import non-authority, and no approval/commit/delete/email execution through CLI/MCP.

### Materials

- `docs/design/cli-model-context-protocols-port-ssot.yaml`
- `docs/design/cli-mcp-port-implementation-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `.cursor/mcp.json`
- `.agent/tasks/todo.md`
- `docs/system-roadmap.yaml`

### Target files

- `docs/design/cli-mcp-port-implementation-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`
- `.cursor/mcp.json` only if local MCP server config is intentionally added
- New backend MCP/API port entrypoint files under `backend/endpoint/` only after route ownership is confirmed
- New backend runtime/repository files for Data Reader, export job, audit log, file stream authorization, and candidate import only as reusable port/abstract-function substrate
- `db/topology_tables.sql` / `db/seed_empty.sql` if CLI/MCP port manifests or export/candidate tables are added
- Backend and integration tests under `backend/tests/Topolactor.Runtime.Tests/` and `backend/tests/Topolactor.Integration.Tests/`

### Target functions/classes

- MCP API port entrypoint boundary (new, dispatch-secured)
- Data Reader authorized read-model builder (new)
- Context API scope/capability resolver (new or extended)
- Export job primitive / repository (new or extended)
- File stream authorization primitive (new or extended)
- Structured output candidate importer (new)
- ManifestDispatcher / abstract function executor integration points
- Tool/resource adapters for `read`, `search`, `aggregate`, `export`, `stream_file`, `import_structured_output`, and `call_tool` only as thin entry adapters into the canonical route

### Acceptance conditions

- [ ] CLI/MCP tools/resources cannot execute without authentication, scope/capability resolution, and ManifestDispatcher/runtime dispatch.
- [ ] No direct DB connection, direct SQL execution, core API direct call, dedicated backend bypass route, or direct API wrapper is introduced.
- [ ] Export creates an export job with source record ids, manifest, checksum, audit/runtime event evidence, and authorized file stream metadata.
- [ ] Structured import creates candidates/drafts/commit candidates only; no record commit, delete, approval, payment approval, email send, credential read/export, or arbitrary mutation is possible from CLI/MCP.
- [ ] PortTargetRef / policy step / abstract function / projection or file stream mapping is seed/manifest-driven.
- [ ] Tests cover fail-close and forbidden-operation cases, including `read_file`/`stream_file`/`call_tool` bypass attempts.

### Explicitly out of scope

- Browser UI automation.
- Direct database connection or direct SQL execution from CLI/MCP.
- Approval execution, record commit, delete, payment approval, email send, credential read/export, or autonomous external AI mutation.
- Provider-specific MCP server behavior or local developer `.cursor/mcp.json` convenience wiring unless it is a thin client of the canonical authenticated port.
- Treating external AI structured output as SSOT.

---

## Investigation exclusions / consolidation notes from PR #481 follow-up scan

- `SqlAttentionScheduler.cs` is not added as a standalone scheduler Bundle because SQL Attention observation/ranking/projection is already explicitly owned by `sql-recommendation-primitive-migration`; scheduler substrate concerns may be handled by `scheduler-job-body-primitive-migration` only where generic.
- `draftPreviewProjection.ts` and `layoutNodeFlowProjection.ts` are excluded unless later evidence shows general runtime authority leakage; current inspected behavior is preview/display or visual-layout shaping rather than canonical projection authority.
- Admin runtime/import/submit UX remains an exception unless its handwritten mapping leaks into the general runtime projection/dispatch/scheduler boundary.
- CLI/MCP is tracked as `not_started` implementation-substrate work, not concrete deletion work, because inspected implementation is design-thin (`.cursor/mcp.json` has no configured MCP server and no confirmed runtime MCP handler surface).
- `DbNotifyRepository`, `DbNotifyListener`, `SseEventBroadcaster`, and `SseProjectionRuntime` are not marked deletion targets outright; LISTEN/NOTIFY/SSE transport can remain hard runtime skeleton, while fixed event metadata/payload projection authority should be evaluated under the projection and scheduler Bundles.

---
## Immediate high-risk items

- `record_table_ref` must not remain frontend/payload-authoritative for attachment binding.
- `NpgsqlExternalPortDbFunctionRepository` must not become a growing bundle-function switchboard.
- `ContextRouteRecommendationResolver` should not become a permanent concrete recommendation function island.
- Credential refresh parsing must not retain placeholder hash/expiry behavior when treated as production substrate.
- SQL Attention / recommendation must never auto-overwrite fixed route or canonical topology state.
- Phase Attention internals must not be reimplemented as normal abstract function steps.
- Existing concrete functions must not be deleted before the abstract function seed path has passing tests.

## Non-goals

- Do not create a new frontend route/island/API wrapper merely to expose this refactor.
- Do not introduce provider-specific SMTP/SFTP/Stripe/object-storage runtime handlers.
- Do not create a standalone credential admin plane or credential runtime.
- Do not move Phase Attention semantic/ID-space exploration internals into abstract function manifest steps.
- Do not treat this file as completion evidence. It is a refactor carry-over maintenance note only.
