# Agent Task List

---
未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `projection-lane-seed-test-hardening` | 投影 lane / seed test hardening | not_started | 3 | `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | not_started | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |

---

## Bundle `projection-lane-seed-test-hardening`

**Status:** not_started  
**SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/framework-core.yaml`, `docs/framework-policy.yaml`

### Problem

投影登録・runtime read・frontend render の一連の lane に対して、登録→読取→表示までを閉じる test が不足している。
現状の調査では、`projection_constructor_mapping` 注入、`ScreenDataShapeQueryRuntime`、`ManifestCanonicalProjection`、SSE/projection runtime、frontend render helper が個別に存在する一方、seed 整合と lane 接続を横断して検査する test が薄い。

### Purpose

投影基盤の実装修正前に、以下の断線を test で検出できる状態にする。

- manifest topology の `projection_constructor_mapping` が dispatch emission に注入されること
- `screen_data_shape` read が想定 source と一致すること
- canonical registration（`hubs.topology_manifests` / `topology.wiring_physical_to_package`）と runtime read/render の接続有無を検出できること
- frontend 側で `emission.projectionDefinition` / `projectionRuntime` / render helper の接続欠落を検出できること
- seed が lane test の前提を満たしていること

### 改善方針

実装修正を先に広げず、まず test lane を追加・拡張する。
seed 存在確認だけで implemented 判定しない。
失敗 test を根拠に、次 bundle で projection registration / read / render の実装修正 scope を確定する。

### 対応資料

- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/README.md`
- `.agent/prompt/audit.md`
- `.agent/protocols/audit.md`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`
- `docs/system-roadmap.yaml`
- `docs/design/db-table-connectivity-audit.md`

### 対象ファイル / 関数

Backend:
- `backend/runtime/ManifestDispatcher.cs`
  - `DispatchAsync`
  - `ExtractProjectionConstructorMapping`
- `backend/runtime/RuntimeExecutor.cs`
  - `ExecuteAsync`
- `backend/runtime/ScreenDataShapeQueryRuntime.cs`
  - `TryExecuteAsync`
- `backend/repository/ManifestCanonicalProjection.cs`
  - `ProjectOnAuthoringDraftAsync`
  - `ProjectOnPromoteAsync`
  - `TryProjectWiringAsync`
- `backend/tests/Topolactor.Runtime.Tests/RuntimeExecutorTests.cs`
- `backend/tests/Topolactor.Runtime.Tests/AdminRuntimeManifestManagementTests.cs`

Frontend:
- `frontend/api/dispatch.ts`
- `frontend/runtime/projectionRuntime.ts`
- `frontend/runtime/renderEmission.ts`
- `frontend/runtime/sseDispatcher.ts`
- `frontend/runtime/sseReceiver.ts`
- `frontend/components/ProjectionView.tsx`
- `frontend/islands/OperationPanel.tsx`
- `frontend/islands/UserDemoStepper.tsx`
- `frontend/tests/sseLane.test.ts`

Seed / DB:
- `db/seed_empty.sql`
- `db/demo_seed.sql`
- `db/manifest_tables.sql`
- `db/topology_tables.sql`

### Tasks

- [ ] Lane test 作成・拡張
  - `ManifestDispatcher` → `Emission.ProjectionDefinition` 注入
  - `RuntimeExecutor` → `ScreenDataShapeQueryRuntime` → `emission.data`
  - SSE receiver / dispatcher / projection runtime / render helper
  - frontend user-facing render が `projectionDefinition` を無視した場合に検出できる test

- [ ] Seed test 作成・拡張
  - demo / admin manifest seed に必要な `dispatcher_mapping`, `runtime_mapping`, `projection_constructor_mapping`, `screen_data_shape`, `db_notify_projection_mapping` が lane 前提として整合すること
  - `screen_data_shape.tableRef` と `topology.physical_tables` / `topology.wiring_physical_to_package` の整合を確認すること
  - seed 存在確認だけでなく、lane test が消費する形になっていること

- [ ] Test error を潰す
  - 追加・拡張した lane / seed test を通す
  - test failure が実装不足を示す場合は、修正対象を次 bundle へ分離する
  - frontend-only 表示追加や seed-only 修正で implemented 扱いにしない

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
