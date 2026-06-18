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

The refactor must follow this global order. Do not delete existing concrete functions first.

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
- File-storage concrete deletion is not complete; `execute_abstract_function` now has DB manifest loading and a `call_postgres_function` primitive path, but existing `topology.fs_*` PostgreSQL functions remain the call target until concrete deletion is proven safe.
- Attachment bind/list/unbind remain on `execute_db_function` compatibility path pending manifest authority for record-table binding.

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
| `backend-abstract-function-executor` | partial_compatibility_fallback | Implement runtime executor for abstract function manifests and primitive registry (step_config binding source added; OutputProp propagation added; file-storage attachment migration complete as representative absorption case; SQL Attention / recommendation / credential hardening remain not_started) |
| `sql-recommendation-primitive-migration` | not_started | Absorb SQL Attention and recommendation by migration order: abstract function fix → seed → seed test → concrete function deletion |
| `credential-primitive-hardening` | not_started | Absorb credential flow by migration order while preserving runtime-only secret materialization |
| `file-storage-db-function-to-abstract-function-migration` | partial_compatibility_fallback | Absorb file-storage DB functions by migration order and remove payload-derived table authority |
| `completion-gate-and-test-alignment` | not_started | Align tests/checks/status after Bundle migration |

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
- [x] Migration path preserves existing external port policy-step lane. (`execute_db_function` operation_key and `NpgsqlExternalPortDbFunctionRepository` compatibility path remain active; no legacy step removed.)

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
- `NpgsqlExternalPortDbFunctionRepository` is now a compatibility stub (concrete fs_* methods deleted); full interface/class removal can follow in `completion-gate-and-test-alignment`.
- SQL Attention / recommendation and credential hardening Bundles remain not_started.

---

## Bundle `sql-recommendation-primitive-migration`

Status: `not_started`

### Problem

SQL Attention and recommendation are conceptually candidate/evidence/ranking primitives, but runtime behavior is still represented through dedicated resolver/runtime classes and projection blocks. This risks preserving backend-specific function islands.

### Purpose

Move SQL Attention and recommendation execution into abstract function primitives while keeping route mutation explicit and user-driven.

### Migration steps

1. **Abstract function fix**
   - Define `sql_attention` and `recommendation_attention` as primitive boundaries.
   - Define `phase_attention_adapter` as the only allowed Phase Attention bridge.
2. **Add seed for absorption target**
   - Add seed/manifest rows for SQL Attention projection/evidence and context-route recommendation candidate generation.
   - Seed must express lane separation and route non-mutation.
3. **Seed test**
   - Add tests proving SQL Attention/recommendation seed paths produce candidates/evidence without mutating route/topology state.
   - Tests must cover no silent fallback, lane separation, append ordering, and projection/evidence boundaries.
4. **Delete existing concrete functions**
   - Remove/shrink dedicated resolver/runtime branches only after seed tests pass.
   - Keep Phase Attention internals as adapter code, not abstract function steps.

### Improvement plan

- [ ] Define `sql_attention` primitive as relational observation, evidence append, ranking, and projection.
- [ ] Define `recommendation_attention` primitive as candidate source, eligibility, feature resolve, score, rank, diversify/suppress, projection, feedback/event.
- [ ] Preserve lane separation: `ui_pressure`, `state_pressure`, `sql_attention_projection`.
- [ ] Preserve prohibition on auto-overwriting route state or canonical topology state.
- [ ] Migrate `ContextRouteRecommendationResolver` algorithm into primitive composition or isolate it behind a primitive adapter.
- [ ] Migrate SQL Attention scheduler/repository/projection runtime behavior into primitive composition or isolate it behind a primitive adapter.
- [ ] Keep Phase Attention internals behind `phase_attention_adapter` only.

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

- [ ] SQL Attention remains observation/evidence/candidate projection only.
- [ ] Recommendation produces ranked candidates but does not mutate route state automatically.
- [ ] `ui_pressure`, `state_pressure`, and `sql_attention_projection` cannot mix lanes.
- [ ] Missing policy and insufficient history remain explicit statuses.
- [ ] SQL Attention scheduler/repository/projection behavior is either expressed through primitives or explicitly isolated as primitive adapters.
- [ ] Tests cover candidate ranking, no silent fallback, lane separation, append ordering, and SQL Attention projection/evidence boundaries.
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

Status: `partial_compatibility_fallback`

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
- `NpgsqlExternalPortDbFunctionRepository` is now a stub — removal of the interface/class entirely can proceed in `completion-gate-and-test-alignment` once all Bundles are done.
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

Status: `not_started`

### Problem

A backend-wide abstract function substrate crosses runtime, DB, external port, SQL Attention, recommendation, credential, projection, and test lanes. Completion cannot be claimed by syntax or file presence alone.

### Purpose

Align required tests/checks and completion language after implementation bundles land.

### Improvement plan

- [ ] Add or update SSOT vocabulary checks for new primitive keys and manifest tables.
- [ ] Add backend runtime tests for primitive execution order, result context binding, fail-close, and no provider/bundle branching.
- [ ] Add seed tests for every absorption target before concrete function deletion.
- [ ] Add integration tests for representative DB mutation returning to projection/refetch/SSE where applicable.
- [ ] Add recommendation tests for lane separation and explicit status.
- [ ] Add credential tests for secret non-projection and refresh hardening.
- [ ] Update `.agent/tasks/todo.md` and `docs/system-roadmap.yaml` only if this refactor maintenance note is intentionally promoted into canonical TODO/roadmap maintenance after code/tests prove status changes.

### Materials

- `.agent/tests/check-worktype-routing.sh`
- `.agent/tests/check-completion-judgment.sh`
- `.agent/tests/check-structure.sh`
- `.agent/tests/check-system-roadmap.sh` only if roadmap changes
- `backend/tests/Topolactor.Runtime.Tests/*`
- `backend/tests/Topolactor.Integration.Tests/*`
- `frontend/tests/*`
- `docs/design/pipeline-continuity-ssot.yaml`

### Acceptance conditions

- [ ] Required checks are documented per changed lane.
- [ ] Global order is enforced: SSOT fix and abstract function primitive generation first, then steps 2–4 loop per target Bundle, then final refactor todo deletion.
- [ ] Runtime with DB update asserts DB state or projection source changed.
- [ ] Runtime returning to frontend asserts SSE/refetch/final state where applicable.
- [ ] No TODO/roadmap status is advanced without implementation and test evidence.
- [ ] No partial status is hidden behind implemented wording.

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
