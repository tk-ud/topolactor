# Refactor Todo: Abstract Function Runtime Substrate

Target repo: `github.com/tk-ud/topolactor`
Status: `not_started`
Worktype: `todo_maintenance`
Source judgment: audit carry-over from abstract-function / SQL Attention / recommendation / credential / execute_db_function review.

## Operating rules

- Read `AGENTS.md` before implementation.
- Treat SSOT docs as canonical. Implementation is a projection surface, not the source of truth.
- Do not split this into small implementation atoms such as one helper, one route, one UI component, or one backend method.
- Do not add provider-specific runtime handlers, bundle-specific one-off backend handlers, dedicated credential planes, or frontend SQL/recommendation judgment.
- Direct implementation should be Bundle-scoped. If scope must shrink, preserve the Bundle boundary and explicitly mark unimplemented acceptance conditions.
- `docs/system-roadmap.yaml` and `.agent/tasks/todo.md` are status/reference surfaces, not implementation proof. Verify actual code and tests before changing completion status.

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
| `abstract-function-runtime-substrate-ssot` | not_started | Define backend-wide abstract function runtime SSOT and primitive taxonomy |
| `abstract-function-manifest-schema` | not_started | Add DB manifest/schema surface for abstract functions, steps, authority, output shapes |
| `backend-abstract-function-executor` | not_started | Implement runtime executor for abstract function manifests and primitive registry |
| `sql-recommendation-primitive-migration` | not_started | Move SQL Attention and recommendation execution into abstract primitives |
| `credential-primitive-hardening` | not_started | Harden credential primitive flow while preserving runtime-only secret materialization |
| `file-storage-db-function-to-abstract-function-migration` | not_started | Replace file-storage `functionName switch` and payload-derived table authority |
| `completion-gate-and-test-alignment` | not_started | Align tests/checks/status after Bundle migration |

---

## Bundle `abstract-function-runtime-substrate-ssot`

Status: `not_started`

### Problem

`docs/design/abstract-function-primitive-registry-ssot.yaml` defines UI/UX primitive vocabulary and mutation boundaries, but it explicitly does not own backend execution engine implementation, backend dispatch configuration, DB function persistence, or SQL Attention ranking implementation. Backend-wide `execute_abstract_function` is therefore not yet a canonical runtime substrate.

### Purpose

Define `execute_abstract_function` as the backend-wide substrate that absorbs backend function bodies currently scattered across DB function dispatch, recommendation, credential flow, event logging, and external port policy execution.

### Improvement plan

- [ ] Define `execute_abstract_function` scope and non-scope.
- [ ] Define primitive categories: authority, input binding, DB operation, PostgreSQL function call, SQL Attention, recommendation, credential, HTTP, scheduler, event log, projection, fail-close.
- [ ] Define Phase Attention boundary as `phase_attention_adapter`, not as in-VM semantic/ID-space exploration logic.
- [ ] Define frontend boundary: request/render/candidate/preview surface only.
- [ ] Define secret-deny projection rule for credential, signed URL, bucket, endpoint, storage path, and plaintext payload.
- [ ] Define migration relation to existing `execute_db_function` and external port policy steps.

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
- Optional new SSOT if needed: `docs/design/abstract-function-runtime-substrate-ssot.yaml`

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

- [ ] SSOT states that backend function bodies should be expressed as abstract function primitives when possible.
- [ ] `execute_db_operation` is not treated as the top-level target; it is a DB primitive under `execute_abstract_function`.
- [ ] Phase Attention internals remain outside the abstract function VM.
- [ ] SQL Attention and recommendation are explicitly candidate/evidence/projection primitives and must not auto-mutate route or canonical topology state.
- [ ] Credential secret materialization is runtime-only and prohibited from projection/log/seed/SSOT output.

---

## Bundle `abstract-function-manifest-schema`

Status: `not_started`

### Problem

`external_port_policy_steps.step_config` is a small string dictionary and cannot safely hold the full authority/input/output structure required for backend-wide abstract functions. Keeping all operation bodies inside `step_config` will recreate hardcoded C# extraction and switch logic.

### Purpose

Create a DB-backed manifest/schema surface for abstract function definitions, primitive steps, input bindings, authority constraints, output shapes, and policy bindings.

### Improvement plan

- [ ] Define abstract function manifest tables.
- [ ] Define primitive step table with ordered execution.
- [ ] Define input binding table that maps payload/context/result values to typed primitive inputs.
- [ ] Define table/column/output authority tables.
- [ ] Define secret-deny projection metadata.
- [ ] Define relation from external port policy steps to abstract function manifest key.
- [ ] Define bootstrap seed examples without raw SQL or frontend-authored table/column authority.

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
- `docs/design/abstract-function-runtime-substrate-ssot.yaml` if created

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

- [ ] No raw SQL is accepted from frontend payload, seed payload, or operation payload.
- [ ] Table/column/join/output authority comes from manifest/physical table binding, not frontend payload.
- [ ] `external_port_policy_steps` can call an abstract function by key instead of carrying the full operation body.
- [ ] Secret-bearing fields are deny-listed fail-close.
- [ ] Migration path preserves existing external port policy-step lane.

---

## Bundle `backend-abstract-function-executor`

Status: `not_started`

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

- [ ] Add backend records/contracts for abstract function manifest, step, binding, authority, and output shape.
- [ ] Add repository read surface for active abstract function manifests.
- [ ] Add primitive registry with no provider-specific or bundle-specific branching.
- [ ] Support result context binding between primitive steps.
- [ ] Support explicit fail-close statuses for missing authority, missing input, missing credential, invalid projection, unsupported primitive.
- [ ] Route external port `execute_abstract_function` through existing `external_port_runtime` lane without new API route.
- [ ] Keep concrete compute adapters for checksum, crypto, HTTP client, DB connection, and Phase Attention engine.

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

- [ ] Backend abstract function executor exists and is registered through DI.
- [ ] Primitive registry is generic and data-driven.
- [ ] Provider/bundle-specific branching does not enter the generic executor.
- [ ] Existing external port policy execution can call abstract function manifests.
- [ ] Failure paths are explicit and covered by tests.
- [ ] Tests prove no silent fallback and no frontend judgment authority.

---

## Bundle `sql-recommendation-primitive-migration`

Status: `not_started`

### Problem

SQL Attention and recommendation are conceptually candidate/evidence/ranking primitives, but runtime behavior is still represented through dedicated resolver/runtime classes and projection blocks. This risks preserving backend-specific function islands.

### Purpose

Move SQL Attention and recommendation execution into abstract function primitives while keeping route mutation explicit and user-driven.

### Improvement plan

- [ ] Define `sql_attention` primitive as relational observation, evidence append, ranking, and projection.
- [ ] Define `recommendation_attention` primitive as candidate source, eligibility, feature resolve, score, rank, diversify/suppress, projection, feedback/event.
- [ ] Preserve lane separation: `ui_pressure`, `state_pressure`, `sql_attention_projection`.
- [ ] Preserve prohibition on auto-overwriting route state or canonical topology state.
- [ ] Migrate `ContextRouteRecommendationResolver` algorithm into primitive composition or isolate it behind a primitive adapter.
- [ ] Keep Phase Attention internals behind `phase_attention_adapter` only.

### Materials

- `docs/design/sql-attention-logs-ssot.yaml`
- `docs/design/sql-attention-logs-ssot.md`
- `docs/design/context-route-recommendation.yaml`
- `docs/design/context-route-recommendation.md`
- `docs/design/runtime-orchestration-ssot.yaml`
- `backend/runtime/ContextRouteRecommendationResolver.cs`
- `backend/schema/SqlAttentionContracts.cs`
- `backend/schema/ContextRouteContracts.cs`
- `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs`
- `frontend/components/RecommendNavigationIsland.tsx`
- `frontend/components/SqlAttentionProjectionBlock.tsx`

### Target functions / classes

- `ContextRouteRecommendationResolver.ResolveAsync`
- `ContextRouteRecommendationResolver.ResolveNextOperations`
- `ContextRouteRecommendationResolver.ResolveNextTokens`
- `ContextRouteRecommendationResolver.ResolveNextEnumItemsAsync`
- `ContextNeighborSearch.FindNearestPrefixes`
- `ContextVectorBuilder.BuildMultiHotVector`
- SQL Attention projection/runtime functions currently responsible for list/projection

### Acceptance conditions

- [ ] SQL Attention remains observation/evidence/candidate projection only.
- [ ] Recommendation produces ranked candidates but does not mutate route state automatically.
- [ ] `ui_pressure`, `state_pressure`, and `sql_attention_projection` cannot mix lanes.
- [ ] Missing policy and insufficient history remain explicit statuses.
- [ ] Tests cover candidate ranking, no silent fallback, lane separation, and append ordering.

---

## Bundle `credential-primitive-hardening`

Status: `not_started`

### Problem

Credential flow already has generic primitives, but refresh request/response handling remains thin and must be hardened before treating credential flow as a reusable abstract function substrate.

### Purpose

Preserve credential flow as abstract function primitives while keeping actual secret materialization in runtime-only adapters.

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

---

## Bundle `file-storage-db-function-to-abstract-function-migration`

Status: `not_started`

### Problem

`NpgsqlExternalPortDbFunctionRepository` currently maps specific `topology.fs_*` names through a C# switch and extracts parameters from request payload/context per function. Attachment operations also read `record_table_ref` from payload, which is a table authority boundary risk.

### Purpose

Move file-storage DB operations into abstract function manifests and primitives, leaving only hard-runtime compute and opaque PostgreSQL function calls as adapters.

### Improvement plan

- [ ] Replace `functionName switch` with manifest-driven `call_postgres_function`, `db_query`, or `db_mutation` primitives.
- [ ] Move parameter binding into abstract function input binding rows/specs.
- [ ] Resolve `record_table_ref` through manifest/physical table binding/current route context, not frontend payload.
- [ ] Keep `compute_checksum` as hard-runtime primitive.
- [ ] Keep complex opaque functions such as signed download authorization behind `call_postgres_function` where appropriate.
- [ ] Ensure projection denies signed URL, bucket, endpoint, storage path, credential, and raw storage refs.

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

- [ ] `record_table_ref` is no longer payload-authoritative.
- [ ] Attachment bind/list/unbind use manifest/physical table authority.
- [ ] File-storage operation bodies are expressible through abstract function manifests/primitives.
- [ ] `NpgsqlExternalPortDbFunctionRepository` no longer grows per-bundle/per-function switch cases for simple DB operations.
- [ ] `compute_checksum` remains the only file-storage hard-runtime compute handler unless SSOT explicitly authorizes more.
- [ ] Tests prove DB state/projection result and deny-list secret projection behavior.

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
- [ ] Add integration tests for representative DB mutation returning to projection/refetch/SSE where applicable.
- [ ] Add recommendation tests for lane separation and explicit status.
- [ ] Add credential tests for secret non-projection and refresh hardening.
- [ ] Update `.agent/tasks/todo.md` and `docs/system-roadmap.yaml` only after actual code/tests prove status changes.

### Materials

- `.agent/tests/check-worktype-routing.sh`
- `.agent/tests/check-completion-judgment.sh`
- `.agent/tests/check-structure.sh`
- `.agent/tests/check-system-roadmap.sh` if roadmap changes
- `backend/tests/Topolactor.Runtime.Tests/*`
- `backend/tests/Topolactor.Integration.Tests/*`
- `frontend/tests/*`
- `docs/design/pipeline-continuity-ssot.yaml`

### Acceptance conditions

- [ ] Required checks are documented per changed lane.
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

## Non-goals

- Do not create a new frontend route/island/API wrapper merely to expose this refactor.
- Do not introduce provider-specific SMTP/SFTP/Stripe/object-storage runtime handlers.
- Do not create a standalone credential admin plane or credential runtime.
- Do not move Phase Attention semantic/ID-space exploration internals into abstract function manifest steps.
- Do not treat this file as completion evidence. It is a refactor carry-over todo only.
