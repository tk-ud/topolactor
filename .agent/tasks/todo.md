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
- [ ] `AGENTS.md`、`.agent/rules/rule.md`、`.agent/README.md`、該当 worktype prompt を読んでから作業する
- [ ] `db/demo_seed.sql` と旧 demo runtime scaffold の参照を DB / docs / frontend / backend tests / frontend tests / shell CI / `.agent/tests` / GitHub Actions / SSOT から再帰探索する
- [ ] 削除可能な旧 demo seed / demo runtime / demo docs / demo static fixture を Bundle 範囲で削除し、partial のまま次探索へ carry-over する
- [ ] `db/init.sql`, `db/README.md`, `docs/demo-walkthrough.md`, `docs/design/runtime-orchestration-ssot.yaml` の旧 demo seed 前提を更新または削除する
- [ ] `docs/system-roadmap.yaml` 自体も cleanup 対象として扱い、旧 demo seed を前提にした roadmap bundle / status / known_gap / public_summary / feature-bundle index を削除または現行 bootstrap 境界へ再分類する
- [ ] `frontend/runtime/operationPresets.ts`, `frontend/routes/demo-static.tsx`, `frontend/routes/demo/debug.tsx`, `frontend/structure_map.ts`, `frontend/registry/componentRegistry.ts` の demo preset / demo UUID / static demo fixture 前提を更新または削除する
- [ ] frontend tests / backend tests / integration tests を旧 demo seed 前提から現行 bootstrap 前提へ更新する
- [ ] `.agent/tests/check-ssot-vocabulary-contract.sh` などの shell CI checks を `db/demo_seed.sql` 非依存へ更新し、空検査・grep 対象消失による偽陽性・vocabulary extraction failure を防ぐ
- [ ] SSOT docs under `docs/framework-*`, `docs/design/*`, `docs/system-roadmap.yaml` に残る `demo_seed`, `demo:*`, fixed demo UUID, public scaffold demo walkthrough 前提を探索し、削除または現行 bootstrap 境界へ正規化する
- [ ] 各 PR / 監査で `demo`, `demo_seed`, `demo:*`, fixed demo UUID, public scaffold demo, demo walkthrough の残存参照探索結果と remaining_todo を記録する

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
