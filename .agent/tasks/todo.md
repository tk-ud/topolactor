# Agent Task List

---
未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | not_started | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |
| `test-proof-manifest-ci-gate` | test 証明 Manifest / CI gate | not_started | 1 | `docs/design/test-proof-manifest-ssot.yaml` |
| `frontend-admin-projection-expression-e2e-completion` | admin 投影登録/更新 E2E 完全化 | not_started | 1 | `docs/design/admin-console-workflow-ssot.yaml` |

---

## Bundle `future-external-bundle-gate`

**Status:** not_started  
**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

- [ ] Notion/Sheets/Slack/GitHub/Webhook/REST-API-Connector/NoCode-Loop — 個別 SSOT 揃うまで実装しない

---

## Bundle `helper-manual`

**Status:** not_started  
**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

- [ ] helper/manual category 候補の実装設計
- [ ] Desktop AI / CLI / MCP Reader 向けライティング方針
- [ ] ヘルプコンポーネント実装（SSOT カテゴリ構造ゲート）

---

## Bundle `product-nocode-loop-acceptance`

**Status:** not_started

- [ ] `product.dynamic_support_nocode_loop` 手動受入（roadmap 追従）

---

## Bundle `test-proof-manifest-ci-gate`

**Status:** not_started  
**SSOT:** `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/test-bundles.yaml`（reverse lookup）, `docs/system-roadmap.yaml`（参照）

- [ ] test 証明 Manifest と CI gate 最適化
  - 問題点: 既存 test は個別に存在するが、各 test bundle が何を証明し、何を証明せず、どの時系列順序で frontend/admin projection expression chain を構成するかが SSOT として固定されていない。
  - 目的: `docs/design/test-proof-manifest-ssot.yaml` を正本として、既存 test 群を proof graph として扱える状態にする。
  - 改善方針: `proof_id`, `proof_order`, `scope_phase`, `depends_on`, `unblocks`, `source_contract`, `target_contract`, `proves`, `does_not_prove` を軸に `.agent/docs/test-bundles.yaml` と CI gate を整合させる。巨大 E2E 前提ではなく、未証明 edge のみ追加/補強する。
  - 対応資料: `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/test-bundles.yaml`, `.agent/tests/check-unified-test-gate.sh`, `.agent/tests/check-frontend-all-tests.sh`
  - 対象ファイル: `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/test-bundles.yaml`, `.agent/tests/check-unified-test-gate.sh`, `.agent/tests/check-frontend-all-tests.sh`
  - 対象関数/単位: `proof_id`, `proof_order`, `scope_phase`, `depends_on`, `unblocks`, `source_contract`, `target_contract`, `proves`, `does_not_prove`
  - OK軸: test bundle scope の時系列順序索引があり、admin 登録/更新 → projection readback → catalog/runtime/layout/visual DOM までの証明 edge が既存 test と未証明 gap に分解される。
  - NG軸: test file 一覧のみ、proves/does_not_prove のみで順序索引なし、seed-only 証明の許容、JSON/DOM表示だけの成功扱い、CI gate が証明 Manifest と無関係。
  - 後続: `frontend-admin-projection-expression-e2e-completion` は本 Bundle 完了後に proof gap を読んで処理する。

---

## Bundle `frontend-admin-projection-expression-e2e-completion`

**Status:** not_started  
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/component-catalog-classification-ssot.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `docs/system-roadmap.yaml`（参照）

- [ ] admin/** 登録・更新由来の projection expression E2E 完全化
  - 前提: `test-proof-manifest-ci-gate` 完了後、proof manifest の未証明 edge を読んで処理する。
  - 問題点: 既存 E2E/seed/projection/layout/render test は seed 到達・projection JSON 表示・個別 utility 境界を証明するが、admin 登録/更新から readback された投影が catalog/runtime/layout/visual DOM まで成立することを一気通貫で証明していない。
  - 目的: seed を test data / 初期条件として使うことは許可しつつ、証明入口は admin/** の登録・更新操作に固定し、frontend projection expression chain の未証明領域を潰す。
  - 改善方針: 既存 E2E 相当 test を、seed 由来の test data を admin 登録/更新経路へ投入 → projection readback → catalog 解決 → props/event/design/layout validation → runtime component render → visual guard surface → rendered DOM assertion まで通す完全実装へ昇格する。componentIds 表示、projection JSON 表示、fallback/error/empty DOM のみを成功扱いしない。
  - 対応資料: `.agent/docs/test-bundles.yaml`, `frontend/tests/projectionLaneSeedHarness.test.ts`, `frontend/tests/uiRenderedInteraction.test.ts`, `frontend/tests/adminUxGuard.test.ts`, `frontend/tests/visualLayoutBuilder.test.ts`, `frontend/tests/runtimeComponentFactory.test.ts`, `frontend/tests/projectionConstructor.test.ts`
  - 対象ファイル: `frontend/components/ProjectionView.tsx`, `frontend/components/LayoutVisualAuditCanvas.tsx`, `frontend/components/catalog.ts`, `frontend/runtime/projectionConstructor.ts`, `frontend/runtime/renderEmission.ts`, `frontend/runtime/projectionRuntime.ts`, `frontend/runtime/runtimeComponentAdapter.ts`, `frontend/runtime/runtimePrimitiveRenderer.ts`, `frontend/runtime/layoutComponentPreview.ts`, `frontend/runtime/visualLayoutUtils.ts`
  - 対象関数: `constructProjection`, `projectionFromEmission`, `renderRuntimeComponents`, `renderEmission`, `adaptComponentDataHub`, `renderRuntimeComponent`, `buildLayoutPreviewRuntimeSpec`, `renderLayoutComponentPreview`, `parseVisualLayoutPatchJson`, `buildVisualLayoutPatchJson`, `LayoutVisualAuditCanvas`, `ProjectionView`
  - OK軸: seed を test data として使用し、admin/** 経由の登録/更新結果を readback し、catalog 登録済み component として分類整合・props/event/design/layout 境界を通過し、runtime component と visual guard DOM で user-facing 表示を確認できる。
  - NG軸: seedData fixture の到達確認のみ、componentIds/projection JSON 表示のみ、admin 画面 DOM 表示のみ、backend promote unit のみ、catalog 外 componentKey 通過、分類ズレ、更新後 readback/DOM 差分なし、invalid layout node の skip による empty 成功、fallback/error component 成功扱い。
  - 要確認: unknown props を global fail-close にするか、SSOT/schema で許可された追加属性のみ透過するかを実装前に確定する。
