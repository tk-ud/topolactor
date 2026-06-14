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
- `backend/runtime/TargetDispatchOverride.cs`: `demo:entity:*` dispatch は "dev/demo bypass" — 次 cycle で削除対象
- `backend/repository/TopologyRepository.cs:205-214`: `LoadDemoEntityListAsync`, `LoadDemoEntityDetailAsync`, `DemoEntityProjection`, `DemoTransitionResult` — 次 cycle で削除対象
- `backend/repository/NpgsqlTopologyRepository.cs:587-677`: demo entity/transition SQL実装 — 次 cycle で削除対象
- `backend/tests/.../ProductionHardeningBoundaryTests.cs`: demo entity hardening tests — 次 cycle で削除対象（上記 production code 削除と連動）
- `db/topology_tables.sql:329-341`: `topology.demo_state_transitions` テーブル — 次 cycle で削除対象（backend 実装削除と連動）
- `docs/design/db-schema.yaml:163`: `topology.demo_state_transitions` テーブル参照 — 次 cycle で更新対象
- `db/topology_tables.sql:290,306`: `demo_state_transitions` へのコメント参照 — 次 cycle で更新対象
- `backend/repository/DiffLogRepository.cs:9-10`: `demo_state_transitions` 分離コメント — 次 cycle で更新対象
- `backend/tests/.../RuntimeExecutorTests.cs:1119-1166`: `"demo"` target の test method（`DispatchAsync_DemoEntity*`）— TargetDispatchOverride demo 削除と連動。次 cycle で更新対象

remaining_todo (次 cycle 対象):
- **coordinated demo:entity dispatch removal** — 以下を同一 PR で削除/更新する必要あり（個別削除は不可）:
  - `backend/runtime/TargetDispatchOverride.cs`: `IsDemoEntity` check, `ApplyDemoStateLoopAsync` 削除
  - `backend/repository/TopologyRepository.cs`: `LoadDemoEntityListAsync`, `LoadDemoEntityDetailAsync`, `ApplyDemoTransitionAsync`, `LoadDemoTransitionHistoryAsync`, `DemoEntityProjection`, `DemoTransitionResult` 削除
  - `backend/repository/NpgsqlTopologyRepository.cs`: demo entity/transition 実装削除
  - `backend/tests/.../ProductionHardeningBoundaryTests.cs`: Gap-11 demo method tests 削除
  - `backend/tests/.../RuntimeExecutorTests.cs`: `DispatchAsync_DemoEntity*` test methods 更新（`OverrideRouteTopologyRepository` 利用 → 別シナリオへ）
  - `db/topology_tables.sql`: `topology.demo_state_transitions` テーブルと index 削除、関連コメント更新
  - `docs/design/db-schema.yaml`: `topology.demo_state_transitions` 参照削除

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
