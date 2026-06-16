# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `retire-legacy-demo-seed-runtime` | 旧 demo seed/runtime 退役 cleanup | partial | 10 | `cleanup.legacy_demo_seed_runtime` | `docs/framework-core.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `external-port-substrate-implementation` | external_port_substrate / external 8 bundle 実装 todo | partial | 1 | `product.external_port_substrate` | `docs/design/external-port-substrate-ssot.yaml` |
| `file-storage-port-consumer` | file_storage_bundle port substrate 接続実装 | not_started | 1 | - | `docs/design/runtime-bundle-file-storage-ssot.yaml` |
| `email-port-consumer` | email_bundle port substrate 接続実装 | not_started | 1 | - | `docs/design/runtime-bundle-email-ssot.yaml` |
| `stripe-port-consumer` | stripe_bundle port substrate 接続実装 | not_started | 1 | - | `docs/design/runtime-bundle-stripe-ssot.yaml` |
| `webhook-inbox-port-consumer` | webhook_inbox_bundle port substrate 接続実装 | not_started | 1 | - | `docs/design/runtime-bundle-webhook-inbox-ssot.yaml` |
| `job-scheduler-port-consumer` | job_scheduler_bundle port substrate 接続実装 | not_started | 1 | - | `docs/design/runtime-bundle-job-scheduler-ssot.yaml` |
| `audit-approval-port-consumer` | audit_approval_bundle port substrate 接続実装 | not_started | 1 | - | `docs/design/runtime-bundle-audit-approval-ssot.yaml` |
| `export-sftp-port-consumer` | export_sftp_bundle port substrate 接続実装 | not_started | 1 | - | `docs/design/runtime-bundle-export-sftp-ssot.yaml` |

---

## Bundle `retire-legacy-demo-seed-runtime`

**Status:** partial
**Roadmap/status SSOT:** `cleanup.legacy_demo_seed_runtime`（TODO cleanup lane。実装状態の正本は実コード・テスト・SSOT 確認）
**SSOT:** `docs/framework-core.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`

問題点:
旧 `db/demo_seed.sql` と demo runtime scaffold が、現在の debug 実利用範囲である auth / UI Builder components / CSS / preset bootstrap と混在している。SSOT・DB init・frontend fixture・docs・tests・CI checks に旧 demo 参照が残ることで、標準 bootstrap の正本境界と seed projection surface が混線している。

目的:
旧 public scaffold demo topology / demo context recommendation / static demo fixture を段階的に退役し、標準 seed を `db/seed_empty.sql` + `db/auth_seed.sql` + UI Builder components / CSS / preset bootstrap に収束させる。

改善方針:
- [x] `AGENTS.md`、`.agent/rules/rule.md`、`.agent/README.md`、該当 worktype prompt を読んでから作業する
- [x] `db/demo_seed.sql` と旧 demo runtime scaffold の参照を DB / docs / frontend / backend tests / frontend tests / shell CI / `.agent/tests` / GitHub Actions / SSOT から再帰探索する
- [x] 削除可能な旧 demo seed / demo runtime / demo docs / demo static fixture を Bundle 範囲で削除し、partial のまま次探索へ carry-over する
- [x] `db/init.sql`, `db/README.md`, `docs/demo-walkthrough.md`, `docs/design/runtime-orchestration-ssot.yaml` の旧 demo seed 前提を更新または削除する
- [x] `docs/system-roadmap.yaml` の demo 参照を探索済み — demo 参照なし（変更不要）
- [x] `frontend/runtime/operationPresets.ts`, `frontend/routes/demo-static.tsx`, `frontend/routes/demo/debug.tsx`, `frontend/structure_map.ts`, `frontend/registry/componentRegistry.ts` の demo preset / demo UUID / static demo fixture 前提を更新または削除する
- [x] frontend tests を旧 demo seed 前提から更新（operationPresets.test.ts, userDemoStepper.test.ts, uiRenderedInteraction.test.ts, uiHandlerBehavior.test.ts, authTopologySecretBoundary.test.ts）
- [x] `.agent/tests/check-ssot-vocabulary-contract.sh` を `db/demo_seed.sql` 非依存へ更新（seed_empty.sql のみ使用）
- [x] SSOT docs の demo_seed 参照を削除（`docs/design/runtime-orchestration-ssot.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`, `docs/design/auth-db-session-credential-ssot.yaml`）
- [x] backend tests の demo attractor key / demo UUID 依存を現行 bootstrap 前提へ更新する（2026-06-14 完了）
- [x] demo:entity dispatch 協調削除 — `backend/runtime/TargetDispatchOverride.cs`（IsDemoEntity/ApplyDemoStateLoopAsync 削除）, `backend/repository/TopologyRepository.cs`（demo メソッド/レコード削除）, `backend/repository/NpgsqlTopologyRepository.cs`（demo 実装削除）, `backend/repository/DiffLogRepository.cs`（コメント更新）, `backend/Program.cs`（コンストラクタ引数削除）, `backend/runtime/ManifestDispatcher.cs`（コメント更新）, 各テストファイル, `db/topology_tables.sql`（demo_state_transitions テーブル/index 削除）, `docs/design/db-schema.yaml`（demo_state_transitions 参照削除）（2026-06-14 完了）

今回削除・更新した範囲 (2026-06-13):
- 削除: `db/demo_seed.sql`, `docs/demo-walkthrough.md`
- 削除: `frontend/routes/demo-static.tsx`, `frontend/routes/demo/debug.tsx`（dir も削除）
- 削除: `frontend/islands/UserDemoStepper.tsx`, `frontend/components/UserDemoNextActions.tsx`, `frontend/components/UserDemoResultCard.tsx`
- 削除: `frontend/package/demoPackage.ts`, `frontend/schema/demoSchema.ts`
- 更新: `db/init.sql`（demo_seed.sql \i 行削除）, `db/README.md`（demo_seed 参照削除）
- 更新: `docs/design/runtime-orchestration-ssot.yaml`（seed_projection_surface から demo_seed.sql 削除）
- 更新: `docs/design/ui-ux-primitive-catalog-ssot.yaml`（demo_seed.sql bootstrap 参照削除）
- 更新: `docs/design/auth-db-session-credential-ssot.yaml`（demo_auth/demo_users 記述を削除）
- 更新: `frontend/structure_map.ts`（demo:hub:overview, demo:entity:list, demo:recommendation:view 削除）
- 更新: `frontend/registry/componentRegistry.ts`（demo-* entries と demo UUID entries 削除）
- 更新: `frontend/runtime/operationPresets.ts`（demo group, DEMO_CONTEXT_* 定数, demoPreviewOptions 削除）
- 更新: `.agent/tests/check-ssot-vocabulary-contract.sh`（demo_seed.sql 連結削除）
- 更新: `frontend/fresh.gen.ts`（demo-static, demo/debug route, UserDemoStepper island 削除）
- 更新: `frontend/routes/index.tsx`, `frontend/routes/demo.tsx`（/demo/debug リンク削除）
- 更新: `frontend/islands/AdminImport.tsx`（/demo/debug リンク削除）
- 更新: `frontend/runtime/emissionSummary.ts`（/demo/debug コメント削除）
- 更新: `frontend/README.md`（demo-walkthrough, /demo/debug 参照削除）
- 更新: `frontend/lib/contentDataConformance.ts`（demo-seed コメント更新）
- 更新: 各 test ファイル（UserDemoStepper/demoPreviewOptions/demo_seed 参照削除）
- 更新: `backend/tests/.../InMemoryContentBundleRepository.cs`（demo_seed.sql comment 削除）

今回削除・更新した範囲 (2026-06-14):
- 更新: `backend/tests/Topolactor.Runtime.Tests/InMemoryContentBundleRepository.cs`（Demo* 定数 → Fixture*、"demo_relation"/"demo_manifest" 文字列 → "fixture_relation"/"fixture_manifest"）
- 更新: `backend/tests/Topolactor.Runtime.Tests/AdminRuntimeContentBundleTests.cs`（InMemoryContentBundleRepository.Demo* 参照 → Fixture*、"demo_relation" アサーション → "fixture_relation"）
- 更新: `backend/tests/Topolactor.Runtime.Tests/RuntimeExecutorTests.cs`（`DemoEntityValidRouteTopologyRepository` → `OverrideRouteTopologyRepository`、`DemoEntityListCalled` → `OverrideEntityListCalled`）
- 更新: `backend/tests/Topolactor.Runtime.Tests/InMemoryEnumDictionaryRepository.cs`（`DemoGroupId` → `FixtureGroupId`、`WithDemoSeed()` → `WithFixtureSeed()`、"demo_status"/"demo_active"/"demo_inactive"/"demo_pending" → "fixture_*"）
- 更新: `backend/tests/Topolactor.Runtime.Tests/AdminRuntimeMasterRosterTests.cs`（`WithDemoSeed()` → `WithFixtureSeed()`）
- 更新: `backend/tests/Topolactor.Runtime.Tests/AdminRuntimeManifestManagementTests.cs`（`WithDemoSeed()` → `WithFixtureSeed()`、`DemoGroupId` → `FixtureGroupId`）

今回削除・更新した範囲 (2026-06-15):
- 削除: `frontend/routes/demo-article.tsx`（コンポーネント認証ガードの static demo scaffold ページ）
- 削除: `frontend/islands/ReplyPanel.tsx`（demo-article.tsx 専用 island — 他に参照なし）
- 更新: `frontend/fresh.gen.ts`（demo-article route と ReplyPanel island の import・登録を削除）
- 更新: `.agent/tests/check-frontend-types.sh`（demo-article.tsx と ReplyPanel.tsx をチェック対象から削除）
- 更新: `backend/runtime/ManifestDispatcher.cs:63-65`（DispatchAsync コメントから削除済み demo/entity 言及を除去）
- 更新: `backend/schema/AuthContracts.cs:6`（"demo auth login endpoint" → "auth login endpoint"）
- 更新: `backend/runtime/TargetDispatchOverride.cs:11`（"dev/demo environments only" → "dev environments only"）

探索済み箇所（削除対象なし）:
- `docs/system-roadmap.yaml`: demo 参照なし（確認済み）
- `.github/workflows/bootstrap-validation.yml:96`: `topolactor_demo` は DB 名（demo_seed 依存なし）
- `frontend/tests/sseLane.test.ts`: UUID は独立テスト fixtures（demo_seed 依存なし）
- `.agent/reports/2026-05-20-db-init-compose-bootstrap-validation.md`: 歴史的レポート（変更不要）
- `docs/design/pipeline-continuity-ssot.yaml`: demo 参照なし（確認済み）
- `frontend/lib/demoSession.ts`: demo prefix は auth session 管理ライブラリ（SESSION_TOKEN_KEY="demo_jwt_token" は実際の cookie 名）— 削除・変更不可
- `frontend/lib/demoSessionValidate.ts`: 実際の backend session 検証ライブラリ — 削除・変更不可
- `frontend/routes/demo.tsx`: /demo は Draft Preview（publish 前プレビュー）— 旧 demo scaffold ではなく active feature
- `backend/tests/.../AuthServiceProjectionLoginTests.cs`: `DEMO_JWT_SECRET`, `DEMO_JWT_EXPIRY_HOURS` は auth JWT 設定 env var（"demo_admin"/"demo_user" は auth_seed.sql のユーザー名）— demo seed 依存なし
- `backend/tests/.../EnvDependentTestCollection.cs:6`: `DEMO_JWT_SECRET` コメント — auth env var コメント（変更不要）
- `backend/runtime/TargetDispatchOverride.cs`: demo:entity dispatch 削除済み（2026-06-14 完了）
- `backend/repository/TopologyRepository.cs:205-214`: LoadDemoEntityListAsync 等削除済み（2026-06-14 完了）
- `backend/repository/NpgsqlTopologyRepository.cs:587-677`: demo entity/transition SQL実装削除済み（2026-06-14 完了）
- `backend/tests/.../ProductionHardeningBoundaryTests.cs`: Gap-11 demo tests 削除済み（2026-06-14 完了）
- `db/topology_tables.sql:329-341`: demo_state_transitions テーブル削除済み（2026-06-14 完了）
- `docs/design/db-schema.yaml:163`: demo_state_transitions 参照削除済み（2026-06-14 完了）
- `db/topology_tables.sql:290,306`: demo_state_transitions コメント参照更新済み（2026-06-14 完了）
- `backend/repository/DiffLogRepository.cs:9-10`: demo_state_transitions コメント削除済み（2026-06-14 完了）
- `backend/tests/.../RuntimeExecutorTests.cs:1119-1166`: DispatchAsync_DemoEntity* 削除済み、ATTRACTOR_RESOLVE_FAILED テスト追加済み（2026-06-14 完了）
- `frontend/tests/demoSession.test.ts`: demoSession.ts ライブラリのテスト（SESSION_TOKEN_KEY="demo_jwt_token" は実際の cookie 名）— 削除・変更不可
- `frontend/tests/demoSessionValidate.test.ts`: demoSessionValidate.ts のテスト — 削除・変更不可
- `frontend/tests/projectionAuthBoundary.test.ts:19`: `demo_jwt_token` は SESSION_TOKEN_KEY 定数 — auth session 参照（変更不要）
- `frontend/tests/frontendComponentEventRuntime.test.ts:39,46,174,183,194,205`: `demo_jwt_token` はセッションストレージ操作（auth, 変更不要）
- `frontend/tests/adminAuthMiddleware.test.ts:4,37,73,83`: SESSION_TOKEN_KEY 参照と認証テスト fixture — 変更不要
- `frontend/tests/draftPreviewProjection.test.ts:59-61`: `/demo?layoutId=` は Draft Preview (/demo) へのリンク — active feature
- `frontend/tests/adminUxGuard.test.ts:988`: `../routes/demo.tsx` は Draft Preview route — active feature
- `frontend/tests/adminUxGuard.test.ts:2026,2054`: `admin/demo` は route ref 文字列テスト fixture — 変更不要
- `frontend/tests/manifestProjectionEditor.test.ts:16,23-24,55,59`: `demo_form` は test fixture 文字列 — demo seed 依存なし
- `backend/tests/Topolactor.Runtime.Tests/DraftPreviewComposerTests.cs:127,143`: `Hello demo` は inline text テスト fixture — demo seed 依存なし
- `backend/tests/Topolactor.Runtime.Tests/AdminRuntimePackageWiringTests.cs:65,76,376,387`: `manifest:demo`/`route:demo` は route ref 文字列テスト — demo seed 依存なし
- `backend/tests/Topolactor.Integration.Tests/TriggerKindGuardTests.cs:28`: `dev/demo bypass` コメント — dev 環境説明（変更不要）
- `backend/runtime/AdminRuntime.cs:201`: `dev/demo bypass` コメント — dev 環境説明（変更不要）
- `backend/schema/Contracts.cs:239`: `/demo preview` コメント — Draft Preview 言及（変更不要）
- `backend/repository/ContentBundleRepository.cs:69`: `/demo draft preview surface` コメント — Draft Preview 言及（変更不要）
- `backend/Program.cs:237,488`: `local/demo HTTP exception`/`/demo draft preview UI` — auth/Draft Preview コメント（変更不要）
- `docs/file-structure.yaml:69,71,75,212,217`: `local_demo_bootstrap_hosting_boundary` は infra role 名 — demo seed 依存なし
- `db/seed_empty.sql:117`: `non demo/admin override` コメント — dispatch path 説明（変更不要）
- `db/auth_seed.sql:4,17-35`: `demo_public`/`demo_admin` は実際の auth users — 変更不可
- `db/migrations/ui_component_registry_preset_catalog_bootstrap.sql`: demo 参照なし（確認済み）
- `db/enum_seed.sql`: demo 参照なし（確認済み）
- `infra/docker-compose.yml`: container name / DB name / env var は "demo" naming convention — demo seed 依存なし
- `.agent/tests/check-runtime-environment.sh:136,150`: `demo_public`/`demo_admin` は auth_seed.sql ユーザー名（変更不要）
- `.agent/reports/2026-05-20-db-init-compose-bootstrap-validation.md:*`: 歴史的レポート（変更不要）
- `frontend/routes/demo.tsx`: Draft Preview (/demo) — active feature（変更不可）
- `frontend/routes/auth.tsx:25`: `/demo` リンク — Draft Preview 参照（変更不要）
- `frontend/routes/admin/index.tsx:211`: `/demo` リンク — Draft Preview 参照（変更不要）
- `frontend/routes/runtime-status.tsx:19`: `/demo` リンク — Draft Preview 参照（変更不要）
- `frontend/runtime/previewInertEventBinding.ts:2`: `/demo draft preview` コメント — active feature 説明
- `frontend/runtime/renderEmission.ts:18`: `/demo draft preview` コメント — active feature 説明
- `frontend/runtime/emissionSummary.ts:54`: `/demo preview face` コメント — active feature 説明
- `frontend/runtime/frontendScheduler.ts:149,368`: `demo_jwt_token` — SESSION_TOKEN_KEY 定数（変更不要）
- `frontend/runtime/draftPreviewProjection.ts`: `/demo` draft preview 参照 — active feature
- `frontend/runtime/sseReceiver.ts:131`: `demo_jwt_token` — SESSION_TOKEN_KEY 定数（変更不要）
- `frontend/components/LayoutPatchApplyModal.tsx:77-79,204,209`: `/demo?layoutId=` — Draft Preview リンク（変更不要）
- `frontend/components/UiBuilderFlowStepper.tsx:103`: `/demo` リンク — Draft Preview 参照（変更不要）
- `frontend/components/LayoutProjectionTree.tsx:135`: `demo (draft preview)` コメント — Draft Preview 説明（変更不要）
- `frontend/islands/UiBuilderAdmin.tsx:213`: `SESSION_TOKEN_KEY = "demo_jwt_token"` 内部定数 — auth session（変更不要）; `7371`: `route:admin:demo` は placeholder 文字列（変更不要）
- `frontend/islands/ProjectionShell.tsx:4,124`: demoSession.ts import と session token 読み取り — auth session（変更不要）
- `frontend/islands/SeedAdmin.tsx:6`: `SESSION_TOKEN_KEY = "demo_jwt_token"` 内部定数 — auth session（変更不要）
- `frontend/islands/DraftPreviewShell.tsx:20,41,277`: SESSION_TOKEN_KEY と `/demo` draft preview surface — auth session / active feature（変更不要）
- `frontend/islands/LoginManifestPanel.tsx:12`: demoSession.ts import — auth session（変更不要）
- `frontend/islands/OperationPanel.tsx:22`: SESSION_TOKEN_KEY 内部定数 — auth session（変更不要）
- `frontend/islands/AdminAuthGate.tsx:10`: demoSession.ts import — auth session（変更不要）
- `frontend/api/mockPresetApi.ts:11`: SESSION_TOKEN_KEY import（変更不要）
- `frontend/api/teamMarkdownApi.ts:16`: SESSION_TOKEN_KEY import（変更不要）
- `frontend/api/adminApi.ts:4`: SESSION_TOKEN_KEY import（変更不要）
- `.agent/docs/structure-map.yaml:62,65,70`: `local_demo_runtime_hosting_boundary` は infra role 名 — demo seed 依存なし（変更不要）

remaining_todo (次 cycle 対象):
- demo 参照の全探索を継続し、削除候補が見当たらなくなるまで partial として反復する
- 今回 cycle で探索した全ファイルが auth session / Draft Preview / infra naming のいずれかと確認。新規削除候補は demo-article.tsx と ReplyPanel.tsx のみ（今回削除済み）
- 次回 cycle では新たに追加されたファイルや変更差分を対象に再探索する

対応資料:
- `docs/framework-core.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/ui-builder-preset-ecosystem-ssot.yaml`
- `docs/system-roadmap.yaml`
- `.agent/tasks/todo.md`

対象ファイル名:
- `db/demo_seed.sql`
- `db/init.sql`
- `db/README.md`
- `docs/demo-walkthrough.md`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/system-roadmap.yaml`
- `frontend/runtime/operationPresets.ts`
- `frontend/tests/operationPresets.test.ts`
- `frontend/routes/demo-static.tsx`
- `frontend/routes/demo/debug.tsx`
- `frontend/structure_map.ts`
- `frontend/registry/componentRegistry.ts`
- `.agent/tests/check-ssot-vocabulary-contract.sh`
- `.github/workflows/*`
- `backend/tests/**`
- `frontend/tests/**`

対象関数名:
- `presetsForGroups`
- `presetById`
- `inferPresetId`
- `buildDispatchContext`
- `demoPreviewOptions`
- `lookupStructureMap`
- `lookupComponent`

remaining_todo:
- この bundle は初回削除で implemented 判定しない。削除候補が grep / SSOT / tests / CI / docs / runtime surface / roadmap の全探索で検出されなくなるまで partial として反復する。
- test / CI / SSOT / roadmap 更新なしで `db/demo_seed.sql` だけを削除する PR は partial 未満として扱う。
- `docs/system-roadmap.yaml` を変更する PR は `.agent/protocols/todo-carry-over.md` の Roadmap update judgment gate を適用し、`bash .agent/tests/check-system-roadmap.sh` を required check として扱う。
- 各 PR は「今回削除した範囲」と「残存探索対象」を必ず記録し、partial から partial への再帰 cleanup として扱う。

---

## Bundle `future-external-bundle-gate`

**Status:** not_started
**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

- [ ] Notion / Google Sheets / Slack / GitHub Issues / generic webhooks / external REST API connectors は、個別 SSOT と connector adapter contract が揃うまで optional external surface として実装しない（CSV/JSON admin import と M6 self-hosted no-code loop とは別 bundle）

---

## Bundle `helper-manual`

**Status:** not_started
**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

SSOT 上、helper/manual category candidates は実装ではなく方針整理。site page / UI component / help screen component 実装は explicitly out of scope。

- [ ] helper/manual category candidates を user promise / safety boundary / onboarding policy として整理する（ページ・コンポーネント実装はしない）
- [ ] Desktop AI / CLI / MCP Reader 向けに、plain business language と approval boundary のライティング方針を整理する

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する

---



## Bundle `external-port-substrate-implementation`

**Status:** partial
**Roadmap/status SSOT:** `product.external_port_substrate`
**SSOT:** `docs/design/external-port-substrate-ssot.yaml`

問題点:
external_port_substrate と external 8 bundle の SSOT 境界は確定済み。残作業は設計確定ではなく、DB seed / record / projection 解決、generic access/response/hook connect/receive、各 consumer bundle 接続を実装すること。

目的:
SSOT を再定義せず、`docs/design/external-port-substrate-ssot.yaml` と各 runtime bundle SSOT に従って external_port_substrate と external 8 bundle の実装残を管理する。詳細作業は `.agent/tasks/external-port-substrate-implementation-todo.md` へ委譲する。

実装方針:
- [x] `external-port-substrate-seed-coding` bundle increment: external port physical tables / seed policy-step surface / generic resolver-executor boundary を partial 実装する
- [x] `auth-external-credential-management-topology-projection` bundle increment: auth / external credential management を fixed-form topology / manifest / screen_data_shape / Step 2.5 relation projection として seed 実装する
- [x] DB repository atomic encrypted credential update を実装する
- [ ] `.agent/tasks/external-port-substrate-implementation-todo.md` の consumer bundle connection / canonical physical binding execution todo を進める

対応資料:
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- `docs/design/runtime-bundle-secret-credential-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/auth-db-session-credential-ssot.yaml`

対象ファイル名:
- `docs/design/external-port-substrate-ssot.yaml`
- `db/topology_tables.sql`
- `db/seed_empty.sql`
- `backend/runtime/ExternalPortCredentialRefresher.cs`
- `backend/repository/NpgsqlExternalPortPolicyRepository.cs`
- `backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs`
- `.agent/tests/check-external-port-substrate-seed-coding.sh`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- `docs/design/runtime-bundle-secret-credential-ssot.yaml`
- `docs/system-roadmap.yaml`

対象関数名またはruntime境界名:
- `ExternalPortRecord`
- `ExternalPortPolicy`
- `ExternalPortPolicyStep`
- `IExternalPortResolver`
- `IExternalPortPolicyRepository`
- `IExternalPortPolicyStepExecutor`
- `ExternalPortPolicyStepExecutor.ExecutePolicyAsync`
- `ExternalPortResolver.ResolveAsync`

対象 surface 名:
- `external_port_substrate`（共通基盤 SSOT surface）
- `external-port-substrate-seed-coding`（parent: `external-port-substrate-implementation`, partial）
- `auth-external-credential-management-topology-projection`（parent: `external-port-substrate-implementation`, implemented）
- `credential_requirement`（port record 付属要件 surface）
- `admin_setting_projection`（port 設定 admin role write surface）

---

## Bundle `file-storage-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-file-storage-ssot.yaml`

問題点:
file_storage_bundle の credential（object storage access key / secret key）が standalone credential 管理 plane の対象として設計されていた。port substrate との接続実装が未着手。

目的:
file_storage_bundle を external_port_substrate の access_port / response_port consumer として確立する。object storage credential は port record 付属の credential_requirement として管理し、standalone credential 管理 plane は作らない。

実装方針:
- [ ] file_storage_bundle の access_port / response_port consumer として seed / DB record / projection 接続を実装する
- [ ] object storage credential_kind を external として port record に付属させる実装を追加する（standalone 管理 plane 不使用）
- [ ] export_job → port record 解決 → generic access/response port connect の経路実装を追加する

対応資料:
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-file-storage-ssot.yaml`

対象 surface 名:
- `access_port`（object storage アクセス）
- `response_port`（object storage 返送）
- `credential_requirement`（object storage credential 付属要件）

---

## Bundle `email-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-email-ssot.yaml`

問題点:
email_bundle の SMTP credential が standalone credential 管理 plane の対象として設計されていた。response_port consumer としての接続実装が未着手。

目的:
email_bundle を external_port_substrate の response_port（provider_kind: smtp）consumer として確立する。SMTP credential は port record 付属の credential_requirement として管理し、standalone 管理 plane は作らない。

実装方針:
- [ ] email_bundle の response_port（smtp）consumer として seed / DB record / projection 接続を実装する
- [ ] SMTP credential_kind を external として port record に付属させる実装を追加する
- [ ] UI approval → response_port 解決 → SMTP dispatch の経路実装を追加する

対応資料:
- `docs/design/runtime-bundle-email-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-email-ssot.yaml`

対象 surface 名:
- `response_port`（SMTP 送信 port）
- `credential_requirement`（SMTP credential 付属要件）

---

## Bundle `stripe-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-stripe-ssot.yaml`

問題点:
stripe_bundle の webhook secret が standalone credential 管理 plane の対象として設計されていた。hook_port consumer としての接続実装が未着手。

目的:
stripe_bundle を external_port_substrate の hook_port（provider_kind: stripe）consumer として確立する。Stripe webhook secret は port record 付属の credential_requirement として管理し、standalone 管理 plane は作らない。

実装方針:
- [ ] stripe_bundle の hook_port（stripe）consumer として seed / DB record / projection 接続を実装する
- [ ] Stripe webhook secret の credential_kind を external として hook_port に付属させる実装を追加する
- [ ] hook_port → signature verification → payment state projection の経路実装を追加する

対応資料:
- `docs/design/runtime-bundle-stripe-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-stripe-ssot.yaml`

対象 surface 名:
- `hook_port`（Stripe webhook 受信 port）
- `credential_requirement`（Stripe webhook secret 付属要件）

---

## Bundle `webhook-inbox-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-webhook-inbox-ssot.yaml`

問題点:
webhook_inbox_bundle の webhook signing key が standalone credential 管理 plane の対象として設計されていた。hook_port consumer としての接続実装が未着手。

目的:
webhook_inbox_bundle を external_port_substrate の hook_port consumer として確立する。webhook signing key は port record 付属の credential_requirement として管理し、standalone 管理 plane は作らない。

実装方針:
- [ ] webhook_inbox_bundle の hook_port consumer として seed / DB record / projection 接続を実装する
- [ ] webhook signing key の credential_kind を external として hook_port に付属させる実装を追加する
- [ ] hook_port → signature verification → scheduler 境界の実装を追加する

対応資料:
- `docs/design/runtime-bundle-webhook-inbox-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-webhook-inbox-ssot.yaml`

対象 surface 名:
- `hook_port`（webhook 受信 port）
- `credential_requirement`（webhook signing key 付属要件）

---

## Bundle `job-scheduler-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-job-scheduler-ssot.yaml`

問題点:
job_scheduler_bundle の外部スケジューラー provider credential が standalone credential 管理 plane の対象として設計されていた。access_port / hook_port consumer としての接続実装が未着手。

目的:
job_scheduler_bundle を external_port_substrate の access_port / hook_port consumer として確立する。外部スケジューラー credential は port record 付属の credential_requirement として管理し、standalone 管理 plane は作らない。topolactor 内蔵 scheduler 利用時は credential_kind: none。

実装方針:
- [ ] job_scheduler_bundle の access_port / hook_port consumer として seed / DB record / projection 接続を実装する
- [ ] 外部スケジューラー credential_kind（external または none）の port record 付属実装を追加する
- [ ] scheduler → manifest_dispatcher 境界が port substrate に依存しないことを確認する

対応資料:
- `docs/design/runtime-bundle-job-scheduler-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-job-scheduler-ssot.yaml`

対象 surface 名:
- `access_port`（外部スケジューラーアクセス port）
- `hook_port`（スケジューラー hook 受信 port）
- `credential_requirement`（外部スケジューラー credential 付属要件）

---

## Bundle `audit-approval-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-audit-approval-ssot.yaml`

問題点:
audit_approval_bundle の承認通知 credential が standalone credential 管理 plane の対象として設計されていた。response_port consumer としての接続実装が未着手。

目的:
audit_approval_bundle を external_port_substrate の response_port consumer として確立する。承認通知 credential は port record 付属の credential_requirement として管理し、standalone 管理 plane は作らない。

実装方針:
- [ ] audit_approval_bundle の response_port consumer として seed / DB record / projection 接続を実装する
- [ ] 承認通知 credential_kind の port record 付属実装を追加する
- [ ] approval → response_port 解決 → 通知送信の経路実装を追加する

対応資料:
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`

対象 surface 名:
- `response_port`（承認通知送信 port）
- `credential_requirement`（承認通知 credential 付属要件）

---

## Bundle `export-sftp-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-export-sftp-ssot.yaml`

問題点:
export_sftp_bundle の SFTP credential（host / user / key）が standalone credential 管理 plane の対象として設計されていた。response_port consumer としての接続実装が未着手。file_storage_bundle との責務分担境界は SSOT に従い、実装時に崩さない。

目的:
export_sftp_bundle を external_port_substrate の response_port（provider_kind: sftp）consumer として確立する。SFTP credential は port record 付属の credential_requirement として管理し、standalone 管理 plane は作らない。

実装方針:
- [ ] export_sftp_bundle の response_port（sftp）consumer として seed / DB record / projection 接続を実装する
- [ ] SFTP credential_kind を external として response_port に付属させる実装を追加する
- [ ] export_job → port record 解決 → SFTP transfer の経路実装を追加する（file-storage-port-consumer の完了を前提）
- [ ] 転送前後の checksum 検証境界を port substrate と独立して実装 / テストする

対応資料:
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`

対象 surface 名:
- `response_port`（SFTP 転送 port）
- `credential_requirement`（SFTP credential 付属要件）
