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
| `core-runtime-bundles-gate` | core runtime bundle 実装ゲート（8 bundle） | not_started | 8 | `product.core_runtime_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |

---
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

## Bundle `core-runtime-bundles-gate`

**Status:** not_started  
**Roadmap bundle:** `product.core_runtime_bundle_gate`  
**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

問題点:
`extended-runtime-bundle-registry-ssot.yaml` の `core_runtime_bundles` 下に 8 bundle が `not_started` / `requires_separate_ssot` として定義されているが、`.agent/tasks/todo.md` の未処理 bundle 索引および `docs/system-roadmap.yaml` の `implementation_registry` に可視化されていなかった。email_bundle と stripe_bundle は設計 SSOT（`runtime-bundle-email-ssot.yaml` / `runtime-bundle-stripe-ssot.yaml`）が存在するが runtime 実装は未着手。残り 6 bundle は設計 SSOT 割り当て済み（owner_status: assigned_to_design_ssot）で実装未着手。

目的:
8 core runtime bundle の not_started 状態を TODO と Roadmap で bundle 単位に可視化し、将来の実装・設計フェーズへの入口を確保する。今すぐ実装は行わない。

改善方針（将来の実装サイクルで適用）:
- [ ] email_bundle: `runtime-bundle-email-ssot.yaml` を実装 SSOT として確定し、UI approval → backend dispatch → SMTP 副作用の実装 bundle を着手する
- [ ] stripe_bundle: `runtime-bundle-stripe-ssot.yaml` を実装 SSOT として確定し、webhook intake → verification → paid state projection の実装 bundle を着手する
- [ ] file_storage_bundle / export_sftp_bundle: CLI/MCP export job との連携 SSOT を確定してから実装する
- [ ] webhook_inbox_bundle: scheduler 経由 runtime route の実装 SSOT を確定してから実装する
- [ ] job_scheduler_bundle: cron/hook/client trigger 統合 SSOT を確定してから実装する
- [ ] audit_approval_bundle / secret_credential_bundle: それぞれの実装 SSOT を確定してから実装する
- 各 bundle 実装時は validate-preview-apply boundary を必須とし、direct runtime execution without scheduler は禁止する

対応資料:
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`（core_runtime_bundles）
- `docs/design/runtime-bundle-email-ssot.yaml`
- `docs/design/runtime-bundle-stripe-ssot.yaml`
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`
- `docs/design/runtime-bundle-webhook-inbox-ssot.yaml`
- `docs/design/runtime-bundle-job-scheduler-ssot.yaml`
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `docs/design/runtime-bundle-secret-credential-ssot.yaml`
- `docs/system-roadmap.yaml`（product.core_runtime_bundle_gate）

対象ファイル名（各 bundle 実装時に確定）:
- `backend/runtime/` 内に bundle ごとの handler クラス
- `backend/tests/Topolactor.Runtime.Tests/` 内に bundle テスト
- `backend/Program.cs`（handler dictionary 登録）

対象関数名（各 bundle 実装時に確定）:
- 各 bundle の `IDispatchableRuntime.ExecuteAsync` 実装

remaining_todo:
- 各 bundle は separate SSOT が実装レベルで確定するまで実装しない
- email / stripe は設計 SSOT が存在するため実装サイクルの優先候補だが、UI approval boundary / webhook verification boundary の実装設計が必要
- bundle 単位で実装フェーズに入る際は、`.agent/routes/worktype-required-protocols.yaml` の `implementation_change` worktype と `design_change` worktype を適用する

SSOT修正が必要な場合の required checks:
- `bash .agent/tests/check-worktype-routing.sh`
- `bash .agent/tests/check-system-roadmap.sh`
- `bash .agent/tests/check-structure.sh`
- 各 bundle 実装時の関連 dotnet tests
