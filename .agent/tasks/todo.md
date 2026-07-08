# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `in_progress` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `helper-manual` | helper reference artifact / admin helper projection | not_started | 1 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `ui-projection-surface-architecture-reinforcement` | UI projection surface architecture reinforcement | partial | 1 | `product.dynamic_support_nocode_loop` / projection surface carry-over | `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 2 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `runtime-route-taxonomy-hardcoded-route-retirement` | Runtime route taxonomy / hardcoded route retirement | not_started | 1 | `product.dynamic_support_nocode_loop` / canonical route taxonomy | `docs/design/runtime-orchestration-ssot.yaml` |
| `initial-projection-side-admin-crud-seed-route-retirement` | Initial projection-side admin CRUD seed route retirement | not_started | 1 | initial projection-side admin CRUD seed | `docs/design/initial-projection-side-admin-crud-seed-ssot.yaml`（requested owning SSOT; repository path may need materialization/connection） |
| `frontend-canonical-surface-structure-label-boundary` | Frontend canonical surface structure / label boundary | not_started | 1 | frontend canonical UI structure/wiring surfaces | canonical surface UI structure/wiring SSOTs, `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml` |
| `admin-console-workflow-step-wording-boundary` | Admin console workflow Step wording boundary | not_started | 1 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml` |
| `pipeline-continuity-frontend-route-seed-proof` | Frontend route / seed replacement proof continuity | not_started | 1 | proof surface carry-over | `docs/design/pipeline-continuity-ssot.yaml` |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (email / audit_approval) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。UI Builder form preset は docs/design/ui-builder-preset-ecosystem-ssot.yaml / db/physical_search_crud_aggregate_preset_seed.sql の CRUD preset seed の写像/派生であり、新規 UI runtime / 専用 component 実装ではない。

---

## Report scope migration classification (2026-07-07)

削除前 ref `018b80fa23949a67a7b03f1853cc9c3f2e45ce3c` の `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md` と `.agent/reports/ui-projection-surface-gap-audit-2026-07-07.md` を全文確認した分類。report 由来 scope は finding 番号ではなく owning SSOT / Bundle 単位で扱う。

- `ui-projection-surface-architecture-reinforcement`: **移管済み / 維持**。PR574 reference evidence、`/demo` cleanup、UI Builder inspection、`ProjectionShell` route/package/manifest awareness、`projectionInput` collection preservation、`runtimeInteraction identity / projection-time idempotency identity` future direction はこの bundle の PR574後残 scope として維持する。route seed 化 / label boundary / admin Step wording / broad pipeline proof は無理に混ぜ潰さない。
- `runtime-route-taxonomy-hardcoded-route-retirement`: **薄い -> 補強**。canonical route taxonomy と non-canonical hardcoded route retirement を runtime-orchestration bundle として分離する。
- `initial-projection-side-admin-crud-seed-route-retirement`: **未移管 -> 追加**。`/admin/enums`, `/admin/users`, `/admin/team-dashboard`, `/admin/scheduler` は単純削除ではなく seed replacement 付き route retirement として扱い、`/demo`, `/runtime-status` は no seed replacement として分離する。
- `frontend-canonical-surface-structure-label-boundary`: **未移管 -> 追加**。canonical surface ごとの UI structure/wiring と normal/technical disclosure label boundary を分離する。
- `admin-console-workflow-step-wording-boundary`: **未移管 -> 追加**。`/admin/contents -> /admin/ui-builder -> /admin/manifests` の Step wording boundary を admin-console-workflow bundle として分離する。
- `pipeline-continuity-frontend-route-seed-proof`: **未移管 -> 追加**。route registry / seed CRUD renderability / route removal replacement / label boundary / admin Step wording proof を pipeline-continuity bundle として分離する。

---


## Bundle `runtime-route-taxonomy-hardcoded-route-retirement`

**Status:** `not_started`
**Primary SSOT:** `docs/design/runtime-orchestration-ssot.yaml`
**移管元 report:** `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`（削除済み report。削除前 ref `018b80fa23949a67a7b03f1853cc9c3f2e45ce3c` から全文確認済み。）

### 問題点

削除済み frontend UI audit report は canonical frontend route authority / route taxonomy / projection entry・gate・admin authoring settings boundary を `runtime-orchestration-ssot` 所有 scope として分離していたが、現行 TODO では `/demo` cleanup 以外の hardcoded route taxonomy が薄い。non-canonical hardcoded routes を canonical route registry 権威として残すと、projection / gate / admin settings の分類が混線する。

### 目的

canonical route taxonomy を明示し、non-canonical hardcoded routes は canonical route authority から退役させる。canonical route は `/`, `/auth`, `/super_auth`, `/admin`, `/admin/contents`, `/admin/ui-builder`, `/admin/manifests` のみとする。

### 改善方針

- canonical route taxonomy: `/` = projection entry, `/auth` and `/super_auth` = gates, `/admin`, `/admin/contents`, `/admin/ui-builder`, `/admin/manifests` = Topolactor projection authoring/settings surfaces。
- non-canonical hardcoded route retirement: `/admin/enums`, `/admin/users`, `/admin/team-dashboard`, `/admin/scheduler`, `/demo`, `/runtime-status` を canonical route registry authority として扱わない。
- `/auth` / `/super_auth` を projection pages と分類しない。`/admin` を business projection と分類しない。
- route retirement の実装順序は関連 seed / proof bundle と整合させ、実装既存状態を SSOT として採用しない。

### 対応資料

- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`（削除済み移管元）
- `.agent/tasks/todo.md`

### 対象ファイル名

- `frontend/routes/index.tsx`
- `frontend/routes/auth.tsx`
- `frontend/routes/super_auth.tsx`
- `frontend/routes/admin/index.tsx`
- `frontend/routes/admin/contents.tsx`
- `frontend/routes/admin/ui-builder.tsx`
- `frontend/routes/admin/manifests.tsx`
- future route registry / navigation files that enumerate canonical frontend routes

### 対象関数名

- future route registry builder / canonical route enumeration functions
- future admin navigation route filter functions
- future projection entry route resolution functions

### 受入条件

- canonical routes are explicitly limited to `/`, `/auth`, `/super_auth`, `/admin`, `/admin/contents`, `/admin/ui-builder`, `/admin/manifests`.
- non-canonical hardcoded routes are absent from canonical route registry authority.
- route taxonomy proof does not treat route presence tests for retired hardcoded routes as canonical proof.

---

## Bundle `initial-projection-side-admin-crud-seed-route-retirement`

**Status:** `not_started`
**Primary SSOT:** `docs/design/initial-projection-side-admin-crud-seed-ssot.yaml`（requested owning SSOT; if absent/disconnected at implementation time, repair SSOT/wiring before product code）
**移管元 report:** `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`（削除済み report。削除前 ref `018b80fa23949a67a7b03f1853cc9c3f2e45ce3c` から全文確認済み。）

### 問題点

削除済み report は initial projection-side admin CRUD seed が credentials auth / external api / external instance / enum CRUD / user-role-status CRUD / dashboard configuration CRUD / scheduler configuration CRUD を所有し、対応する hardcoded admin routes の退役時に seed replacement を要求していた。現行 TODO ではこの seed replacement scope が未移管で、`/admin/enums`, `/admin/users`, `/admin/team-dashboard`, `/admin/scheduler` を単純削除として誤処理する危険がある。

### 目的

required CRUD responsibilities を seed-backed canonical projection/admin mechanism へ移し、hardcoded route を退役させる。`/demo` と `/runtime-status` は seed replacement 対象ではないことを明確化する。

### 改善方針

- `/admin/enums -> enum CRUD seed`。
- `/admin/users -> user / role / status CRUD seed`。
- `/admin/team-dashboard -> dashboard configuration CRUD seed`。
- `/admin/scheduler -> scheduler configuration CRUD seed`。
- seed CRUD exists before route removal; seeded CRUD renders through canonical projection/admin mechanism.
- `/demo -> no seed replacement`。standalone demo domain / demo seed fallback / canonical demo revival は NG。
- `/runtime-status -> no diagnostics seed replacement`。runtime diagnostics を initial CRUD seed に移さない。
- UI Builder persistence model や diagnostics route replacement をこの seed bundle に混ぜない。

### 対応資料

- `docs/design/initial-projection-side-admin-crud-seed-ssot.yaml`（requested owning SSOT / future or missing path check required）
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`（削除済み移管元）
- `.agent/tasks/todo.md`

### 対象ファイル名

- future initial projection-side admin CRUD seed SQL / manifest files
- `frontend/routes/admin/enums.tsx`
- `frontend/routes/admin/users.tsx`
- `frontend/routes/admin/team-dashboard.tsx`
- `frontend/routes/admin/scheduler.tsx`
- canonical projection/admin render surfaces that consume seed-backed CRUD

### 対象関数名

- future seed CRUD registration functions
- future seed-to-admin-projection mapping functions
- future canonical admin CRUD render functions
- future route retirement proof helpers

### 受入条件

- `/admin/enums`, `/admin/users`, `/admin/team-dashboard`, `/admin/scheduler` are not treated as simple deletion targets; each has the seed replacement listed above.
- `/demo` has no seed replacement.
- `/runtime-status` has no diagnostics seed replacement.
- old route-presence tests are replaced by seed/render proof where CRUD replacement is required.

---

## Bundle `frontend-canonical-surface-structure-label-boundary`

**Status:** `not_started`
**Primary SSOT:** canonical surface UI structure/wiring SSOTs, including `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
**移管元 report:** `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`（削除済み report。削除前 ref `018b80fa23949a67a7b03f1853cc9c3f2e45ce3c` から全文確認済み。）

### 問題点

削除済み report は canonical surface ごとの UI structure/wiring、visible labels、normal/technical disclosure boundary を frontend surface UI structure/wiring SSOT group として分離していた。現行 TODO では UI Builder architecture 残 scope に寄り過ぎており、全 canonical surface の normal view label boundary が独立 bundle として未移管である。

### 目的

canonical surfaces (`/`, `/auth`, `/super_auth`, `/admin`, `/admin/contents`, `/admin/ui-builder`, `/admin/manifests`) ごとの UI structure/wiring と表示 label boundary を、implementation-derived raw vocabulary ではなく owning SSOT へ戻す。

### 改善方針

- 各 canonical surface は owning UI structure/wiring SSOT を持ち、implementation/test はその SSOT へ map する。
- normal view label boundary: raw id / UUID / topology / manifest / screen_data_shape / DB / backend / Route / Primary Table / UI Builder Key 等を通常表示の意味にしない。
- raw route/page refs, `source_active_manifest_id`, `componentKey`, `componentKind`, `layoutClassRefs`, `orderIndex`, `relationIntents`, `operationEntityBindings` 等は internal value または明示的 technical disclosure として扱う。
- operator visible labels は raw-first にしない: `like` -> `含む`, `ilike` -> `含む（大小文字を区別しない）`, `between` -> `範囲内`, `in` -> `リストに含まれる`, `is null` -> `空欄`, `AND/OR/NOT` -> `すべて満たす / いずれか満たす / 除外`, `Res/Req` -> `表示 / 入力`。
- seed propsJson の visible labels は user-facing または explicit draft/technical とし、English-first labels を normal-view authority にしない。

### 対応資料

- canonical surface UI structure/wiring SSOTs
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`（削除済み移管元）
- `.agent/tasks/todo.md`

### 対象ファイル名

- `frontend/routes/index.tsx`
- `frontend/routes/auth.tsx`
- `frontend/routes/super_auth.tsx`
- `frontend/routes/admin/index.tsx`
- `frontend/routes/admin/contents.tsx`
- `frontend/routes/admin/ui-builder.tsx`
- `frontend/routes/admin/manifests.tsx`
- frontend components / islands that render canonical surface labels and technical disclosure

### 対象関数名

- future normal label mapping functions
- future technical disclosure rendering functions
- future operator label mapping functions
- future canonical surface view model builders

### 受入条件

- canonical surfaces have SSOT-mapped UI structure/wiring proof.
- normal user-facing views do not expose raw ids / UUIDs / internal vocabulary as primary meaning.
- technical details, if needed, are behind explicit technical disclosure and not normal operation labels.

---

## Bundle `admin-console-workflow-step-wording-boundary`

**Status:** `not_started`
**Primary SSOT:** `docs/design/admin-console-workflow-ssot.yaml`
**移管元 report:** `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`（削除済み report。削除前 ref `018b80fa23949a67a7b03f1853cc9c3f2e45ce3c` から全文確認済み。）

### 問題点

削除済み report は admin-console-workflow-ssot が `/admin/contents -> /admin/ui-builder -> /admin/manifests` と Step wording layer を所有すると定義していた。現行 TODO では workflow Step wording が UI projection architecture 残 scope へ混ざる危険があり、`/admin/ui-builder` を `/admin/contents` local Step 4 と誤表記する余地が残る。

### 目的

admin authoring workflow の Step wording boundary を明示し、local submit pipeline と whole-admin workflow を混同しない。

### 改善方針

- `/admin/contents = local submit pipeline Step 1-3`。
- `/admin/ui-builder = whole-admin Step 4`。
- `/admin/manifests = whole-admin Step 5`。
- `/admin/contents -> /admin/ui-builder -> /admin/manifests` の flow を維持する。
- `/admin/ui-builder` を `/admin/contents` local Step 4 と呼ばない。`/admin/manifests` の whole-admin Step 5 を落とさない。

### 対応資料

- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`（削除済み移管元）
- `.agent/tasks/todo.md`

### 対象ファイル名

- `frontend/routes/admin/contents.tsx`
- `frontend/routes/admin/ui-builder.tsx`
- `frontend/routes/admin/manifests.tsx`
- frontend admin navigation / header / stepper components
- tests that assert admin wording and flow labels

### 対象関数名

- future admin workflow step label builders
- future admin navigation view model functions
- future admin route stepper rendering functions

### 受入条件

- Step wording proof qualifies `/admin/ui-builder` as whole-admin Step 4 and `/admin/manifests` as whole-admin Step 5.
- `/admin/contents` local submit pipeline remains Step 1-3 and is not extended to own whole-admin Step 4/5 wording.
- contents -> ui-builder -> manifests remains the canonical authoring order.

---

## Bundle `pipeline-continuity-frontend-route-seed-proof`

**Status:** `not_started`
**Primary SSOT:** `docs/design/pipeline-continuity-ssot.yaml`
**移管元 report:** `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`（削除済み report。削除前 ref `018b80fa23949a67a7b03f1853cc9c3f2e45ce3c` から全文確認済み。）

### 問題点

削除済み report は proof surface policy と route removal / seed CRUD renderability replacement tests を `pipeline-continuity-ssot` 所有 scope として分離していた。現行 TODO では UI Builder architecture 残 proof はあるが、canonical route only・non-canonical absence・seed CRUD renderability・label boundary・admin Step wording を横断する broad pipeline proof が未移管である。

### 目的

route removal を test-only deletion にせず、canonical routes only / seed replacement / renderability / wording / label boundary の proof chain へ置換する。

### 改善方針

- pipeline proof scope: canonical routes only.
- non-canonical hardcoded routes absent.
- required seed CRUD exists.
- seeded CRUD renders through canonical projection/admin mechanism.
- old route-presence tests replaced by seed/render proof.
- route registry proof, seed CRUD renderability proof, route removal replacement proof, label boundary proof, admin Step wording proof を入れる。
- target proof files are expected around `frontend/tests/adminUxGuard.test.ts`, `frontend/tests/adminMainFlow.test.ts`, `frontend/tests/visualLayoutBuilder.test.ts`, `frontend/tests/uiBuilderPackageWiring.test.ts`, `frontend/tests/runtimeUiInteractionScenario.test.ts`, `frontend/tests/adminWiringExecutionLane.test.ts`, `frontend/tests/uiBuilderWiringProjection.test.ts` or future equivalent proof surfaces.

### 対応資料

- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/initial-projection-side-admin-crud-seed-ssot.yaml`（requested owning SSOT / future or missing path check required）
- `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`（削除済み移管元）
- `.agent/tasks/todo.md`

### 対象ファイル名

- `frontend/tests/adminUxGuard.test.ts`
- `frontend/tests/adminMainFlow.test.ts`
- `frontend/tests/visualLayoutBuilder.test.ts`
- `frontend/tests/uiBuilderPackageWiring.test.ts`
- `frontend/tests/runtimeUiInteractionScenario.test.ts`
- `frontend/tests/adminWiringExecutionLane.test.ts`
- `frontend/tests/uiBuilderWiringProjection.test.ts`
- future route registry / seed renderability proof files

### 対象関数名

- future canonical route registry proof helpers
- future non-canonical route absence assertions
- future seed CRUD renderability assertions
- future label boundary assertions
- future admin Step wording assertions

### 受入条件

- route registry proof covers canonical routes only.
- non-canonical hardcoded routes are absent and old route-presence tests are not retained as canonical proof.
- required seed CRUD exists and renders through canonical projection/admin mechanism.
- proof covers label boundary and admin Step wording boundary.

---

## Bundle `ui-projection-surface-architecture-reinforcement`

**Status:** `partial`
**Primary SSOT:** `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`
**移管元 report:** `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`, `.agent/reports/ui-projection-surface-gap-audit-2026-07-07.md`（未処理 scope を本 bundle に統合移管済みのため report file は削除対象。削除後の未処理作業管理 source はこの TODO bundle に一本化する。）

### 問題点

`.agent/reports/` に UI projection / UI Builder audit report が残り、未処理 scope の管理面が report と todo に分散していた。PR574 で `/admin/ui-builder` の UI structure / wiring owning SSOT と実装が進んだため、report 内の PR574 blocking / non-blocking / future candidate の線引きを todo へ移管し、PR574 実装済み範囲を未実装扱いへ戻さない必要がある。

active report は `runtime_interaction_identity / projection_time_idempotency_identity` を future Bundle candidate として残し、subordinate report は `/demo` route ownership、production `ProjectionShell` default-bound、`projectionInputFromData` の `rows[0]` collapse を partial blocking として残していた。report 削除前に、未処理 scope・PR574 証跡・次 bundle 境界をこの bundle へ転記する。

### 目的

- report 由来の未処理 scope を `.agent/tasks/todo.md` の bundle として一本化する。
- PR574 で実装済みの `/admin/ui-builder` 作り込みを証跡化し、再実装対象または未実装扱いへ戻さない。
- PR574 後の残 scope を、次の bundle として実装可能な境界に整理する。
- 移管済み report file を削除し、未処理作業管理 source を `.agent/tasks/todo.md` へ寄せる。

### PR574 reference evidence（再実装対象ではない）

- PR: `#574`
- title: `Add admin UI Builder UI structure/wiring owning SSOT with wiring mode and trigger policies`
- merged: true
- merge_commit_sha: `018b80fa23949a67a7b03f1853cc9c3f2e45ce3c`
- changed_files: 35 / additions: 10610 / deletions: 3778
- evidence role: PR574 body / comments / merged diff are the reference evidence for implemented scope. This TODO keeps only a compact evidence pointer and does not duplicate the full PR574 proof transcript.
- implemented scope summary:
  - `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml` was added as the owning SSOT for `/admin/ui-builder` layout/wiring canvas boundary, `runtimeInteractions` persistence authority, trigger vocabulary, lifecycle policy, high-frequency policy, drag-drop wiring edit policy, and seed-registry external capability selection.
  - wiring mode / trigger vocabulary / lifecycle policy / high_frequency_policy / drag_drop_wiring_edit were implemented and gated by `.agent/tests/check-admin-uibuilder-wiring.sh` registered in `check-structure.sh`.
  - `frontend/lib/uiBuilderWiringProjection.ts`, `frontend/components/WiringGraphPanel.tsx`, `frontend/islands/UiBuilderAdmin.tsx`, and `frontend/components/NodeEventAuthoringPanel.tsx` were part of the implementation/proof surface.
  - runtime interaction execution/idempotency proof was added: `frontend/runtime/uiEventEffectRunner.ts`, `renderEmission` / `runtimeComponentFactory` event bindings, deterministic `computeDispatchIdempotencyKey` / `appendResolvedPayloadToIdempotencyKey`, backend `topology.runtime_dispatch_idempotency_ledger`, `NpgsqlRuntimeDispatchIdempotencyLedgerRepository`, `ExternalPortDispatchRuntime`, and `InstancePortRuntime` claim/complete/fail gating.
  - verification/proof details are traced in PR574 body, review comments, follow-up commits, and merged diff.

### 改善方針 / 残 scope

#### `/demo route ownership cleanup`

- `/demo` remains non-canonical. Standalone `/demo` route retention is NG.
- `/demo` seed replacement is NG; do not add a demo seed fallback, standalone demo domain, or canonical `/demo` revival.
- Before implementation, search SSOT/docs for `/demo`; if a canonical `/demo` reference exists, handle as `design_change` first and preserve active route taxonomy.
- Reusable inspection logic target is `/admin/ui-builder` component / panel / tab, not a standalone route.

#### `UI Builder projection inspection componentization`

- Move reusable inspection behavior into `/admin/ui-builder` component scope as read-only inspection.
- Required inspection role: production-equivalent render confirmation, canvas preview vs applied projection comparison, applied topology confirmation, route/package/manifest confirmation, read query confirmation, `propBindings` confirmation, `rows` / `activeColumns` / `displayColumnMode` confirmation.
- The inspection component is not persistence authority, not promotion authority, not canonical projection entry, and not seed fallback.

#### `production projection route/package/manifest awareness`

- `frontend/islands/ProjectionShell.tsx` must not remain default-bound to `default` / `screen_list` / `Search` as the product projection entry.
- Add route/package/manifest-aware projection entry so arbitrary UI Builder applied topology can be selected through the production projection surface.

#### `projectionInput collection outer shape preservation`

- `frontend/runtime/projectionInput.ts` `projectionInputFromData` must not collapse `screen_data_shape_query_result` to `rows[0]` unless an explicit single-row mapping is selected.
- Preserve collection outer shape from `backend/runtime/ScreenDataShapeQueryRuntime.cs`: `rows`, `aggregationResults`, `activeColumns`, and `displayColumnMode`.

#### `projection proof reinforcement`

- Add/keep proof that `rows`, `activeColumns`, and `displayColumnMode` survive the projection path.
- Add/keep proof that `propBindings` resolve from `emission.data` branches, not from first-row sample or `seedLabel` smoke.
- Do not accept `default/screen_list/Search` as arbitrary topology proof.

#### `runtimeInteraction identity / projection-time idempotency identity`

- PR574 retry-safe dispatch idempotency is implemented evidence and must not be reclassified as missing: frontend runner plus backend ledger protect `dispatchExternalPort` / `dispatchInstanceOperation` retries, concurrent duplicates, reload/reconnect runner recreation, and failed-claim reclaim.
- Current UI-composed idempotency identity (`nodeId + interactionIndex` plus stable authored fields and resolved payload in `computeDispatchIdempotencyKey` / `appendResolvedPayloadToIdempotencyKey`) is compatibility/current-state, not final projection authority.
- Future direction: DB / projection emission assigns stable `runtime_interaction_id` / `idempotency_base_key` at the projection-authority layer; UI forwards assigned identity and appends resolved payload deterministically.
- Backend idempotency ledger remains execution gate and is distinct from `runtime_event_log`; do not turn event-log evidence into execution gate authority.
- **SSOT contract now defined (design_change, PR577 follow-up)**: `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml` `lifecycle_policy.projection_authority_runtime_interaction_identity` (`status: design_only_not_yet_implemented`) specifies the target `runtimeInteractionId` field shape, backend-assignment authority (layout_patch persistence boundary, not client-generated), JSONB storage location, duplication-must-not-carry-id rule, the `idempotency_base_key` formula update (`runtimeInteractionId` replacing `nodeId + interactionIndex` when present, backward-compatible fallback otherwise), and the lazy-backfill migration. This defines the boundary for a **future, separate `implementation_change` bundle** — it is not authorization to implement, and does not change PR574/PR577 completion status.

### 対応資料

- `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`（移管元、削除済みにする）
- `.agent/reports/ui-projection-surface-gap-audit-2026-07-07.md`（移管元、削除済みにする）
- `.agent/tasks/todo.md`
- PR574 `Add admin UI Builder UI structure/wiring owning SSOT with wiring mode and trigger policies`
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

### 対象ファイル名

- `.agent/tasks/todo.md`
- `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md`（移管後削除）
- `.agent/reports/ui-projection-surface-gap-audit-2026-07-07.md`（移管後削除）
- `frontend/islands/DraftPreviewShell.tsx`
- `frontend/islands/ProjectionShell.tsx`
- `frontend/runtime/projectionInput.ts`
- `backend/runtime/ScreenDataShapeQueryRuntime.cs`
- `frontend/runtime/renderEmission.ts`
- `frontend/runtime/runtimeComponentFactory.ts`
- `frontend/runtime/uiEventEffectRunner.ts`
- `frontend/lib/uiBuilderWiringProjection.ts`
- `backend/repository/NpgsqlRuntimeDispatchIdempotencyLedgerRepository.cs`
- `backend/runtime/ExternalPortDispatchRuntime.cs`
- `backend/runtime/InstancePortRuntime.cs`
- `db/topology_tables.sql`

### 対象関数名

- `projectionInputFromData`
- `computeDispatchIdempotencyKey`
- `appendResolvedPayloadToIdempotencyKey`
- `buildExternalPortEventBinding`
- `buildLocalUiStateEventBinding`
- `renderEmission`
- `emitBoundEvent`
- `createUiEventEffectRunner`
- `emitLifecycle`
- `updateNodes`
- `ClaimAsync`
- `CompleteAsync`
- `FailAsync`
- `rt_claim_dispatch_idempotency_key`
- `rt_complete_dispatch_idempotency_key`
- `rt_fail_dispatch_idempotency_key`

### 受入条件

- `/demo` remains non-canonical; standalone `/demo` route retention, canonical revival, demo seed replacement, and `/demo` as product projection proof are rejected.
- Projection inspection is located in `/admin/ui-builder` component/panel/tab scope and remains read-only.
- Production projection entry is route/package/manifest aware and not fixed to `default` / `screen_list` / `Search`.
- `projectionInputFromData` preserves `screen_data_shape_query_result` outer shape (`rows`, `aggregationResults`, `activeColumns`, `displayColumnMode`) unless explicit single-row mapping exists.
- Proof covers collection shape, `activeColumns`, `displayColumnMode`, and `propBindings` from `emission.data` branches.
- `runtimeInteraction identity / projection-time idempotency identity` is treated as PR574後の残 scope, not PR574 blocking and not PR574 completion scope.

## Bundle `helper-manual`

**Status:** not_started
**Roadmap/status SSOT:** `product.helper_manual_policy`
**Primary SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`
**Design prerequisite status:** schema / seed / admin helper viewer 実装前に `docs/design/user-facing-helper-manual-ssot.yaml` の clone lifecycle reference contract を正本として読むこと。

目的:
helper/manual をユーザー向け文章方針だけでなく、人間 / Agent / MCP / External AI / Local LLM / admin UI が共通参照する JSON helper reference artifact として実装可能にする。現行MCPの import-candidate / draft_operation / commit_candidate lane に topology authoring draft を載せる参照点を作り、admin では同じ内容を viewer として表示する。最新 `/admin/contents` Step 1 の `create_new_topology` / `clone_active_as_replacement_draft` / `clone_active_as_new_topology_draft`、`draft_origin`、`clone_mode`、replacement merge authority boundary、SQL Attention candidate boundary、`layoutPatchDraft` と production manifest replacement merge の分離を JSON contract 上で混同しない。

残問題:
- `helper_reference_artifact` の schema / seed がまだ無い。実装前に `admin_topology_clone_lifecycle_reference_contract` / `replacement_merge_authority_boundary` / `sql_attention_candidate_reference_boundary` / `layout_patch_draft_vs_manifest_replacement_boundary` を schema required / strongly-recommended fields へ写像する必要がある。
- MCPで topology authoring draft を構築する際の `structured_output_payload` / `assigned_business_object_candidate` / `assignment_target_scope` / `preview_diff` / `unresolved_fields` の具体例が未実装。`entry_mode` / `draft_origin` / `clone_mode` / `source_active_manifest_id` / `source_active_evidence` / `lineage_evidence_only` / `replacement_merge_intent` / `replacement_merge_blockers` / `backend_merge_authority` を含め、replacement clone と clone-as-new topology を payload 上で混同しない必要がある。
- admin 共通ヘッダから開く helper viewer が未実装。viewer は clone lifecycle badge、replacement-vs-lineage-only badge、backend authority notice、stale source / active identity conflict blocker、SQL Attention candidate boundary、layout patch not replacement merge notice を表示する必要がある。
- helper viewer が admin submit / apply / promote / approval / merge target decision / active mutation を実行しない projection-only surface であることを実装上確認する guard が無い。
- AI/MCP由来 candidate evidence、SQL Attention candidate、人間の admin 手作業 draft、manual replacement clone draft、clone-as-new topology draft の origin / lineage / authority を混同しない表示・保存・監査境界が未検証。
- source evidence / lineage evidence だけで replacement authority を得ないこと、replacement merge は backend AdminRuntime / ManifestRepository transaction のみが source evidence・validation・diff/log evidence・stale source check・active identity conflict check 後に existing active row update + working draft row delete として成立することを schema / seed / tests へ渡す必要がある。
- `layoutPatchDraft` / `layout_patch:apply` は UI Builder layout draft / layout persistence であり production manifest replacement merge ではない、という helper artifact 上の boundary が未実装。

改善方針:
implementation_change で、SSOTに従って helper schema / seed artifact を追加し、admin common header から Drawer helper viewer を開けるようにする。viewer は検索・カテゴリ選択・tree viewer・detail modal mount と clone lifecycle boundary 表示までに限定し、runtime/admin/MCP authority を持たせない。MCP新規tool surfaceは作らず、既存 import-candidate lane の payload reference として実装する。schema / seed は `create_new_topology`、`clone_active_as_replacement_draft`、`clone_active_as_new_topology_draft`、`manual_new`、`manual_clone_replacement`、`manual_clone_new_topology`、`sql_attention_candidate`、`none`、`replacement`、`new_topology` を明示し、SQL Attention candidate は explicit human/admin adoption まで candidate/evidence surface に留める。

対応資料:
- `docs/design/user-facing-helper-manual-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`
- `docs/design/cli-mcp-port-implementation-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/ci-contract-ssot.yaml`
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `docs/framework-policy.yaml`

対象ファイル名候補:
- `docs/helper/helper-manual.schema.json` (new helper artifact schema)
- `docs/helper/helper-manual.seed.json` (new helper reference seed)
- `frontend/islands/*Admin*.tsx` or common admin shell/header files (helper launch button)
- `frontend/islands/*Helper*.tsx` or future `AdminHelper*` component files
- `frontend/lib/*helper*` (helper artifact load/filter/tree utility)
- `backend/runtime/AuthorizedCliReaderPortRuntime.cs` (reference only; MCP operation expansionは原則しない)
- `backend/tests/Topolactor.Runtime.Tests/AuthorizedCliReaderPortRuntimeTests.cs` (reference only; candidate origin regression確認)

対象ブロック名:
- `helper_reference_artifact`
- `admin_topology_clone_lifecycle_reference_contract`
- `replacement_merge_authority_boundary`
- `sql_attention_candidate_reference_boundary`
- `layout_patch_draft_vs_manifest_replacement_boundary`
- `mcp_topology_authoring_draft_reference`
- `admin_helper_projection`
- `provenance_boundary`
- `user_facing_message_policy`
- `language_policy`
- `helper_manual_category_candidates`
- `safety_boundary`
- `relation_to_other_ssot`

対象関数名候補:
- future `loadHelperManualSeed`
- future `filterHelperManualItems`
- future `buildHelperManualTree`
- future `renderHelperManualDetail`
- future `openAdminHelperDrawer`
- future `mountDetailHelperModal`
- future `renderCloneLifecycleBadge`
- future `renderReplacementAuthorityNotice`
- future `renderLineageOnlyBoundaryNotice`
- future `renderSqlAttentionCandidateBoundaryNotice`
- future `renderLayoutPatchNotReplacementMergeNotice`

残受入条件:
- [ ] helper schema / seed artifact が追加され、SSOTの required fields と clone lifecycle reference contract を満たしている。
- [ ] helper seed に admin authoring flow / admin topology clone lifecycle / MCP topology authoring draft / UI Builder / CI Attention / approval boundary のカテゴリがある。
- [ ] internal vocabulary と user-facing vocabulary の対応が helper artifact に定義されている。
- [ ] MCP topology authoring draft の payload example が、既存 import-candidate lane の field に対応している。
- [ ] `create_new_topology` / replacement clone / clone-as-new topology が JSON contract 上で混同されない。
- [ ] source evidence / lineage evidence が replacement authority ではないことを schema / seed / viewer 表示で確認できる。
- [ ] SQL Attention candidate が explicit adoption 前に draft row / production merge authority へ化けない。
- [ ] `layoutPatchDraft` / `layout_patch:apply` が production manifest replacement merge ではないことを artifact と viewer で確認できる。
- [ ] admin common header から helper Drawer を開け、検索・カテゴリ選択・tree viewer・detail modal が使える。
- [ ] helper viewer は admin submit / apply / promote / approval / merge target decision / active mutation / MCP operation を実行しない。
- [ ] AI/MCP由来 candidate evidence と human manual admin draft の origin を混同しない表示・監査境界が確認できる。
- [ ] 新規 MCP tool surface / admin submit direct execution / active topology mutation は追加していない。

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes、external port consumer projection、team Markdown dashboard は実装済みとして扱い、未実装扱いに戻さない。`scheduler-job-manifest-admin-ui` と `helper-manual` は別 canonical bundle で扱い、この手動受入に混ぜない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する。
- [ ] `product.admin_topology_authoring` の `/admin/contents` Step 1 3 entry mode → clone draft → edit → backend replacement merge を、統合UX手動受入 / hand-debug evidence として確認する。

手動受入 checklist:
- [ ] `/admin/contents` で、作成・編集・import・apply の現在位置、draft / preview / validate / apply / saved / failed の関係、apply前後の変更差分と反映先を誤認しない。
- [ ] validation 失敗後、画面を離れずに修正へ戻れ、作業文脈が途切れない。
- [ ] `/admin/ui-builder` で、配置・style・binding の編集中状態と反映済み状態、modal / drawer / preview の関係を混同しない。
- [ ] advanced / internal vocabulary が通常操作の判断を邪魔せず、必要な説明だけが出ている。
- [ ] Admin import の CSV / JSON import → preview → editor merge → validate → apply が一連の体験として見え、apply後の projection 反映先を追える。
- [ ] recommendation / SQL Attention feedback は現在状態ではなく候補・観察結果として見え、採用しない限り route / topology / 画面状態が変わったように見えない。
- [ ] 古い・対象なし・根拠が弱い candidate が、ユーザーに採用を強制する表示に見えない。
- [ ] webhook / hook / external port consumer projection で、route / credential requirement reference、secret非表示、受信・拒否・成功・失敗、承認前・承認後・拒否後、provider未接続/future scope の状態を誤認しない。
- [ ] file export / transfer / email / audit approval の結果 projection が成功・失敗・保留として追え、失敗時に再試行すべきか設定を直すべきか判断できる。
- [ ] `/admin/team-dashboard` / MdViewer で、saved view / rendered Markdown / source / binding / completed_preset_seed summary の関係を誤認せず、Markdown body を runtime SSOT と見なさない。
- [ ] refresh / clone / rebind の可否、seed invalid の explicit error、md_viewer read projection boundary が画面上で自然に読め、mutation authority と混同しない。
