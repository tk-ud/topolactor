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
| `secret-credential-implementation-contract` | secret_credential_bundle 実装契約 | acceptance_pending | 1 | - | `docs/design/runtime-bundle-secret-credential-ssot.yaml` |
| `file-storage-implementation-contract` | file_storage_bundle 実装契約 | not_started | 1 | - | `docs/design/runtime-bundle-file-storage-ssot.yaml` |
| `email-implementation-contract` | email_bundle 実装契約 | not_started | 1 | - | `docs/design/runtime-bundle-email-ssot.yaml` |
| `stripe-implementation-contract` | stripe_bundle 実装契約 | not_started | 1 | - | `docs/design/runtime-bundle-stripe-ssot.yaml` |
| `webhook-inbox-implementation-contract` | webhook_inbox_bundle 実装契約 | not_started | 1 | - | `docs/design/runtime-bundle-webhook-inbox-ssot.yaml` |
| `job-scheduler-implementation-contract` | job_scheduler_bundle 実装契約 | not_started | 1 | - | `docs/design/runtime-bundle-job-scheduler-ssot.yaml` |
| `audit-approval-implementation-contract` | audit_approval_bundle 実装契約 | not_started | 1 | - | `docs/design/runtime-bundle-audit-approval-ssot.yaml` |
| `export-sftp-implementation-contract` | export_sftp_bundle 実装契約（file-storage 後） | not_started | 1 | - | `docs/design/runtime-bundle-export-sftp-ssot.yaml` |

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

## Bundle `secret-credential-implementation-contract`

**Status:** acceptance_pending  
**SSOT:** `docs/design/runtime-bundle-secret-credential-ssot.yaml`

問題点:
secret_credential_bundle 設計 SSOT 点検済み（authority_boundary: admin_config_and_runtime_secret_store / trigger_kind: admin_config_credential_registration）。credential 管理基盤（参照登録 / rotation / validation / runtime injection）および admin UI を bundle 単位で実装完了。

目的:
secret_credential_bundle を後続 bundle（email / stripe / webhook_inbox / export_sftp）の credential injection 基盤として実装完了とする。

実装済み範囲:
- [x] credential_reference_schema（db/credential_reference_tables.sql: topology.credential_references + logs.credential_audit_log）
- [x] secret_store_adapter（ICredentialStore / EnvironmentVariableCredentialStore）
- [x] credential_validation_service（CredentialValidationService.ValidateAsync: registration / rotation での検証）
- [x] credential_rotation_service（rotate action: rotation_actor_id 必須 / post-rotation ValidateAsync / explicit audit failure）
- [x] credential_injection_pattern（ICredentialStore.GetAsync / SetAsync）
- [x] register: ICredentialStore.SetAsync → fail-close（SECRET_STORE_UNAVAILABLE / CREDENTIAL_STORE_BINDING_FAILED）
- [x] register: 登録成功時に credential_registered audit event 記録（audit failure = explicit error）
- [x] audit write failure を silent に swallow しない（CREDENTIAL_AUDIT_WRITE_FAILED で明示返却）
- [x] 実 credential 値を public SSOT / コード / audit log に含めない設計
- [x] IDispatchableRuntime.ExecuteAsync として実装（SecretCredentialBundleRuntime）
- [x] AdminRuntime.credential_registry 層（credential_registry:list/register/validate/rotate → SecretCredentialBundleRuntime 委譲）
- [x] credential_registration_ui（AdminCredentialPanel.tsx: 登録フォーム / 一覧 / 検証 / rotation 操作フロー）
- [x] admin UI は credential 実値を一切表示しない境界を維持
- [x] rotation: JWT sub（admin username）を rotation_actor_id として使用（auth/admin 境界に接続）
- [x] /admin/credentials route（AdminAuthGate 下に配置）
- [x] 24 unit tests（全ての SSOT 境界を網羅）

今回実装した範囲（全コミット）:
- 新規: `db/credential_reference_tables.sql`
- 更新: `db/init.sql`
- 新規: `backend/schema/SecretCredentialBundleContracts.cs`
- 新規: `backend/repository/CredentialReferenceRepository.cs`
- 新規: `backend/repository/NpgsqlCredentialReferenceRepository.cs`
- 新規: `backend/runtime/EnvironmentVariableCredentialStore.cs`
- 新規: `backend/runtime/CredentialValidationService.cs`
- 新規: `backend/runtime/SecretCredentialBundleRuntime.cs`
- 新規: `backend/runtime/AdminRuntime.Credential.cs`（credential_registry 層）
- 更新: `backend/runtime/AdminRuntime.cs`（credential_registry switch cases）
- 更新: `backend/Program.cs`（DI 登録 + handler dict + AdminRuntime への SecretCredentialBundleRuntime 注入）
- 新規: `frontend/api/adminApi.ts`（credential API functions）
- 新規: `frontend/islands/AdminCredentialPanel.tsx`
- 新規: `frontend/routes/admin/credentials.tsx`
- 更新: `frontend/fresh.gen.ts`（route + island 登録）
- 新規: `backend/tests/Topolactor.Runtime.Tests/SecretCredentialBundleRuntimeTests.cs`（24 tests）

対応資料:
- `docs/design/runtime-bundle-secret-credential-ssot.yaml`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

---

## Bundle `file-storage-implementation-contract`

**Status:** not_started  
**SSOT:** `docs/design/runtime-bundle-file-storage-ssot.yaml`

問題点:
file_storage_bundle 設計 SSOT 点検済み（authority_boundary: export_job_or_authorized_api / validate-preview-apply: export_job/checksum/manifest/signed_download 定義済み）。実装に必要な export_job_schema / file_artifact_storage_schema / checksum_record_schema / signed_url_generation_service / manifest_schema / storage_provider_adapter の設計詳細が未確定。export_sftp_bundle が依存する前提 bundle であるため優先着手が望ましい。

目的:
file_storage_bundle の実装契約を確定し、export_job を通じた file write / checksum / manifest / signed_url_download を bundle 単位で実装できる状態にする。

改善方針:
- [ ] export_job_schema の設計（DB schema）を確定する
- [ ] file_artifact_storage_schema の設計（export 生成ファイルの参照・メタデータ管理）を確定する
- [ ] checksum_record_schema の設計（ファイル整合性検証レコード）を確定する
- [ ] signed_url_generation_service の設計（有効期限付き download URL 生成 C# service）を確定する
- [ ] manifest_schema の設計（export package manifest 形式）を確定する
- [ ] storage_provider_adapter の設計（object storage provider 抽象化 C# interface、credential は secret_store 経由）を確定する
- [ ] IDispatchableRuntime.ExecuteAsync として実装し、checksum 必須・unauthenticated download 禁止を維持する

対応資料:
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

対象ファイル名:
- `backend/runtime/FileStorageBundleRuntime.cs`
- `backend/schema/FileStorageBundleContracts.cs`
- `backend/tests/Topolactor.Runtime.Tests/FileStorageBundleRuntimeTests.cs`
- `backend/Program.cs`（handler dictionary 登録）
- `db/init.sql` bootstrap 経路に export_job / file_artifact テーブル DDL を追加（`db/*.sql` canonical ファイルとして作成し `db/init.sql` に include する）

対象関数名:
- `FileStorageBundleRuntime.ExecuteAsync` (IDispatchableRuntime)
- `IFileStorageAdapter.WriteAsync`, `IFileStorageAdapter.GenerateSignedUrlAsync`
- `ChecksumService.ComputeAsync`, `ChecksumService.VerifyAsync`

---

## Bundle `email-implementation-contract`

**Status:** not_started  
**SSOT:** `docs/design/runtime-bundle-email-ssot.yaml`

問題点:
email_bundle 設計 SSOT 点検済み（authority_boundary: ui_approval_then_backend_dispatch / validate-preview-apply: draft/preview/approval/dispatch/delivery_log 定義済み）。実装に必要な email_draft_surface_schema / email_template_catalog / backend_email_dispatch_service / smtp_provider_adapter / delivery_log_schema / approval_confirmation_ui / idempotency_key_schema の設計詳細が未確定。CLI/MCP からの email send は禁止。AI 単独送信は禁止。

目的:
email_bundle の実装契約を確定し、UI approval → backend dispatch → SMTP 副作用の実装を bundle 単位で着手できる状態にする。

改善方針:
- [ ] email_draft_surface_schema の設計（DB schema + UI component 境界）を確定する
- [ ] email_template_catalog の設計（テンプレート構造・格納方式）を確定する
- [ ] backend_email_dispatch_service の設計（approval 後の SMTP / API provider 送信 C# handler）を確定する
- [ ] smtp_provider_adapter の設計（SMTP / email API provider 抽象化 C# interface、credential は secret_store 経由）を確定する
- [ ] delivery_log_schema の設計（runtime_event_log への送信結果記録形式）を確定する
- [ ] approval_confirmation_ui_component の設計を確定する
- [ ] idempotency_key_schema を確定する（同一 approval ID での二重送信防止）
- [ ] IDispatchableRuntime.ExecuteAsync として実装し、承認なし送信・AI 単独送信を禁止する

対応資料:
- `docs/design/runtime-bundle-email-ssot.yaml`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

対象ファイル名:
- `backend/runtime/EmailBundleRuntime.cs`
- `backend/schema/EmailBundleContracts.cs`
- `backend/tests/Topolactor.Runtime.Tests/EmailBundleRuntimeTests.cs`
- `backend/Program.cs`（handler dictionary 登録）
- `db/init.sql` bootstrap 経路に email_draft / email_delivery_log テーブル DDL を追加（`db/*.sql` canonical ファイルとして作成し `db/init.sql` に include する）

対象関数名:
- `EmailBundleRuntime.ExecuteAsync` (IDispatchableRuntime)
- `ISmtpAdapter.SendAsync`
- `EmailApprovalService.ConfirmAsync`

---

## Bundle `stripe-implementation-contract`

**Status:** not_started  
**SSOT:** `docs/design/runtime-bundle-stripe-ssot.yaml`

問題点:
stripe_bundle 設計 SSOT 点検済み（authority_boundary: verified_webhook_event_only / validate-preview-apply: webhook_inbox/event_verification/payment_state_projection/ledger_binding 定義済み）。実装に必要な webhook_inbox_schema / stripe_event_verification_service / payment_state_projection_schema / ledger_binding_schema / idempotency_key_schema の設計詳細が未確定。Stripe-Signature 検証なしの paid state 確定は禁止。

目的:
stripe_bundle の実装契約を確定し、webhook intake → signature verification → payment state projection → ledger binding の実装を bundle 単位で着手できる状態にする。

改善方針:
- [ ] webhook_inbox_schema の設計（Stripe webhook 受信 DB schema）を確定する
- [ ] stripe_event_verification_service の設計（Stripe-Signature ヘッダー検証 C# service、signing key は secret_store 経由）を確定する
- [ ] payment_state_projection_schema の設計（検証済み event からの paid state 投影 schema）を確定する
- [ ] ledger_binding_schema の設計（payment state 確定後の account 記録 schema）を確定する
- [ ] idempotency_key_schema を確定する（同一 stripe_event_id での二重処理防止）
- [ ] IDispatchableRuntime.ExecuteAsync として実装し、webhook_direct_runtime_execution を禁止する

対応資料:
- `docs/design/runtime-bundle-stripe-ssot.yaml`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

対象ファイル名:
- `backend/runtime/StripeBundleRuntime.cs`
- `backend/schema/StripeBundleContracts.cs`
- `backend/tests/Topolactor.Runtime.Tests/StripeBundleRuntimeTests.cs`
- `backend/Program.cs`（handler dictionary 登録）
- `db/init.sql` bootstrap 経路に stripe_webhook_inbox / payment_state テーブル DDL を追加（`db/*.sql` canonical ファイルとして作成し `db/init.sql` に include する）

対象関数名:
- `StripeBundleRuntime.ExecuteAsync` (IDispatchableRuntime)
- `StripeWebhookVerificationService.VerifyAsync`
- `PaymentStateProjectionService.ProjectAsync`

---

## Bundle `webhook-inbox-implementation-contract`

**Status:** not_started  
**SSOT:** `docs/design/runtime-bundle-webhook-inbox-ssot.yaml`

問題点:
webhook_inbox_bundle 設計 SSOT 点検済み（authority_boundary: scheduler_then_runtime_route_only / validate-preview-apply: intake/signature_verification/snapshot/validate/preview/explicit_apply 定義済み）。実装に必要な webhook_intake_schema / webhook_event_signature_verification_service / intake_snapshot_schema / scheduler_hook_trigger_wiring / idempotency_key_schema の設計詳細が未確定。webhook_direct_runtime_execution は禁止。

目的:
webhook_inbox_bundle の実装契約を確定し、webhook → signature verification → intake snapshot → scheduler → runtime route の実装を bundle 単位で着手できる状態にする。

改善方針:
- [ ] webhook_intake_schema の設計（webhook 受信・署名前保存 DB schema）を確定する
- [ ] webhook_event_signature_verification_service の設計（provider ごとの署名検証 C# service、signing key は secret_store 経由）を確定する
- [ ] intake_snapshot_schema の設計（検証済み payload の canonical 入力候補 schema）を確定する
- [ ] scheduler_hook_trigger_wiring の設計（runtime_orchestration_ssot の hook trigger kind との整合）を確定する
- [ ] idempotency_key_schema を確定する（同一 webhook event ID での二重処理防止）
- [ ] IDispatchableRuntime.ExecuteAsync として実装し、scheduler 経由を強制する

対応資料:
- `docs/design/runtime-bundle-webhook-inbox-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`

対象ファイル名:
- `backend/runtime/WebhookInboxBundleRuntime.cs`
- `backend/schema/WebhookInboxBundleContracts.cs`
- `backend/tests/Topolactor.Runtime.Tests/WebhookInboxBundleRuntimeTests.cs`
- `backend/Program.cs`（handler dictionary 登録）
- `db/init.sql` bootstrap 経路に webhook_intake / intake_snapshot テーブル DDL を追加（`db/*.sql` canonical ファイルとして作成し `db/init.sql` に include する）

対象関数名:
- `WebhookInboxBundleRuntime.ExecuteAsync` (IDispatchableRuntime)
- `WebhookSignatureVerificationService.VerifyAsync`
- `IntakeSnapshotService.CreateAsync`

---

## Bundle `job-scheduler-implementation-contract`

**Status:** not_started  
**SSOT:** `docs/design/runtime-bundle-job-scheduler-ssot.yaml`

問題点:
job_scheduler_bundle 設計 SSOT 点検済み（authority_boundary: trigger_alignment_and_runtime_queue_only / cron/hook/client trigger 統合境界定義済み）。runtime_orchestration_ssot の scheduler_contract を基礎とするが、job_queue_schema / cron_driver_loop / hook_trigger_intake / client_trigger_intake / collision_control / scheduler_overflow_policy / job_execution_log_schema の実装設計が未確定。runtime_destination_selection は manifest_dispatcher が所有する（変更なし）。

目的:
job_scheduler_bundle の実装契約を確定し、cron / hook / client trigger を統合するスケジューラー基盤を bundle 単位で実装できる状態にする。

改善方針:
- [ ] job_queue_schema の設計（DB schema: trigger event キュー）を確定する
- [ ] cron_driver_loop の設計（既存 SqlAttentionScheduler との役割分担・周期実行境界）を確定する
- [ ] hook_trigger_intake の設計（webhook_inbox_bundle からの hook 受信インターフェース）を確定する
- [ ] client_trigger_intake の設計（API / frontend からの client trigger 受信インターフェース）を確定する
- [ ] collision_control の実装設計（同一 trigger の二重実行防止）を確定する
- [ ] scheduler_overflow_policy の実装設計（queue overflow 時の明示的エラー返却）を確定する
- [ ] job_execution_log_schema の設計（runtime_event_log への実行履歴記録形式）を確定する

対応資料:
- `docs/design/runtime-bundle-job-scheduler-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`

対象ファイル名:
- `backend/runtime/JobSchedulerBundleRuntime.cs`
- `backend/schema/JobSchedulerBundleContracts.cs`
- `backend/tests/Topolactor.Runtime.Tests/JobSchedulerBundleRuntimeTests.cs`
- `backend/Program.cs`（handler dictionary 登録）
- `db/init.sql` bootstrap 経路に job_queue テーブル DDL を追加（`db/*.sql` canonical ファイルとして作成し `db/init.sql` に include する）

対象関数名:
- `JobSchedulerBundleRuntime.ExecuteAsync` (IDispatchableRuntime)
- `IJobQueue.EnqueueAsync`, `IJobQueue.DequeueAsync`
- `CollisionControlService.CheckAsync`

---

## Bundle `audit-approval-implementation-contract`

**Status:** not_started  
**SSOT:** `docs/design/runtime-bundle-audit-approval-ssot.yaml`

問題点:
audit_approval_bundle 設計 SSOT 点検済み（authority_boundary: ui_human_explicit_action_only / validate-preview-apply: request/review/approval/rejection 定義済み）。CLI/MCP read/export 境界との整合確認が必要。実装に必要な approval_request_schema / approval_state_machine / export_job_approval_schema / audit_log_schema / approval_notification / idempotency_key_schema の設計詳細が未確定。AI 単独承認・暗黙的承認は禁止。

目的:
audit_approval_bundle の実装契約を確定し、承認フロー（UI human action）/ 監査ログ / export_job approval を bundle 単位で実装できる状態にする。

改善方針:
- [ ] approval_request_schema の設計（DB schema: 承認リクエスト管理テーブル）を確定する
- [ ] approval_state_machine の設計（request → review → approved/rejected 状態遷移）を確定する
- [ ] export_job_approval_schema の設計（export_job に紐付く承認レコード）を確定する
- [ ] audit_log_schema の設計（runtime_event_log への監査記録形式、実値は記録しない）を確定する
- [ ] approval_notification の設計（承認要求通知境界）を確定する
- [ ] idempotency_key_schema を確定する（同一 approval_request_id での二重承認防止）
- [ ] cli-model-context-protocols-port-ssot.yaml との整合を確認してから実装する（CLI/MCP approval は禁止）

対応資料:
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

対象ファイル名:
- `backend/runtime/AuditApprovalBundleRuntime.cs`
- `backend/schema/AuditApprovalBundleContracts.cs`
- `backend/tests/Topolactor.Runtime.Tests/AuditApprovalBundleRuntimeTests.cs`
- `backend/Program.cs`（handler dictionary 登録）
- `db/init.sql` bootstrap 経路に approval_request / export_job_approval テーブル DDL を追加（`db/*.sql` canonical ファイルとして作成し `db/init.sql` に include する）

対象関数名:
- `AuditApprovalBundleRuntime.ExecuteAsync` (IDispatchableRuntime)
- `ApprovalStateMachine.TransitionAsync`
- `AuditLogService.RecordAsync`

---

## Bundle `export-sftp-implementation-contract`

**Status:** not_started  
**SSOT:** `docs/design/runtime-bundle-export-sftp-ssot.yaml`

問題点:
export_sftp_bundle 設計 SSOT 点検済み（authority_boundary: authorized_export_job_only / validate-preview-apply: export_job/package/manifest/checksum/transfer 定義済み）。file_storage_bundle の実装契約が前提（file_storage_bundle が file_generation / checksum_computation / manifest_generation を担当）。実装に必要な sftp_transfer_service / transfer_log_schema / retry_policy / credential_injection_pattern の設計詳細が未確定。manifest + checksum 必須。

目的:
export_sftp_bundle の実装契約を確定し、export_job が生成した package の SFTP 外部搬出を bundle 単位で実装できる状態にする。

改善方針:
- [ ] file-storage-implementation-contract の完了を前提条件とする
- [ ] sftp_transfer_service の設計（SFTP push C# adapter、credential は secret_store 経由）を確定する
- [ ] transfer_log_schema の設計（runtime_event_log への転送結果記録形式）を確定する
- [ ] retry_policy の実装設計（明示的 retry / silent fallback 禁止 / scheduler 経由 retry）を確定する
- [ ] credential_injection_pattern の設計（SFTP host / user / key を secret_store 経由注入）を確定する
- [ ] 転送前・転送後両方の checksum 検証を実装する
- [ ] IDispatchableRuntime.ExecuteAsync として実装し、manifest + checksum なし転送を禁止する

対応資料:
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`

対象ファイル名:
- `backend/runtime/ExportSftpBundleRuntime.cs`
- `backend/schema/ExportSftpBundleContracts.cs`
- `backend/tests/Topolactor.Runtime.Tests/ExportSftpBundleRuntimeTests.cs`
- `backend/Program.cs`（handler dictionary 登録）

対象関数名:
- `ExportSftpBundleRuntime.ExecuteAsync` (IDispatchableRuntime)
- `ISftpAdapter.TransferAsync`
- `TransferChecksumVerificationService.VerifyAsync`
- 各 bundle 実装時の関連 dotnet tests
