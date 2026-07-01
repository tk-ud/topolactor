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

### 実装境界

この bundle は test hardening が scope であり、投影基盤本体の修正 bundle ではない。
追加・拡張した test が runtime read-source / frontend render / canonical projection consumer の実装断線を露出した場合、その失敗は本 bundle の成果として扱い、断線修正は次 bundle に分離する。

この bundle 内で直してよい範囲:

- test harness の不備
- GitHub Actions workflow 未接続
- seed fixture の不足
- test log の不足
- assertion 粒度不足
- lane に seed が流れていない test 構成不備

この bundle 内で直してはいけない範囲:

- runtime read-source 本体修正
- frontend render 本体修正
- canonical projection consumer の実装
- 投影基盤そのものの修正を test hardening に混ぜること

### Test strategy

- DB なしで動く fast lane を優先して作る
- seed file を静的 parse し、抽出した seedData を loop で lane harness に流す
- lane に流す値は `seedData ?? explicitLiteralFixture` とする
- `seedData ?? null` の silent fallback は禁止する
- `null` を使うのは projectionDefinition missing / seed missing を検出する専用 NG test のみとする
- literal fixture を使う場合は `explicitLiteralFixture` として明示し、seed 欠落を隠さない
- seed test は文字列存在確認ではなく、seed → lane harness → projection assertion まで到達させる
- DB 環境がある場合は別 job / 別 lane で repository / constraint / pg_notify / SSE live path を追加検証する

DB なし fast lane で確認する範囲:

- `db/seed_empty.sql` / `db/demo_seed.sql` の静的 parse
- manifest topology JSON の抽出
- `dispatcher_mapping` / `runtime_mapping` / `projection_constructor_mapping` / `screen_data_shape` / `db_notify_projection_mapping` の整合
- seedData → projectionRuntime / renderEmission への投入
- frontend projection assertion
- workflow に含まれること

DB integration が必要な範囲:

- 実際の INSERT / FK / unique / constraint 検証
- `topology.physical_tables` と `topology.wiring_physical_to_package` の実DB join確認
- Npgsql repository 経由の promote / read 確認
- pg_notify / SSE live path

### OK軸

- 追加・拡張した test が GitHub Actions workflow に含まれること
- GitHub Actions の job / step 名で backend lane / frontend lane / seed contract / seed → lane integration のどこで落ちたか分かること
- test error 時のログが、どの lane / seed / projection mapping が壊れたか分かる粒度で出ること
- lane test が seed を実際に入力として流せること
- seed 存在確認だけでなく、seed → lane → projection assertion まで到達すること
- DB なし fast lane で seed static parse → lane simulation → projection assertion が回ること
- seedData が無い場合は silent null fallback せず、明示 fixture または NG test として扱うこと
- fail した場合に、frontend render 断線・backend read 断線・seed 整合不良を切り分けられること
- test failure が implementation gap を示す場合、その場で実装修正せず次 bundle の scope へ分離できること

### NG軸

- ローカルでしか動かない test を追加して完了扱いにする
- GitHub workflow に含まれない test を追加して完了扱いにする
- error log が単なる assertion failed で、壊れた lane / seed / mapping が分からない
- seed の文字列存在確認だけで lane に流していない
- `seedData ?? null` で seed 欠落を隠す
- literal fixture を seed 由来と誤認させる
- test failure が示した runtime read-source / frontend render / canonical projection consumer の実装不足を、この bundle 内で雑に潰す
- frontend-only 表示追加や seed-only 修正で implemented 扱いにする

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

CI / workflow:
- `.github/workflows/*`
- `.agent/tests/*`

### Tasks

- [ ] Lane test 作成・拡張
  - `ManifestDispatcher` → `Emission.ProjectionDefinition` 注入
  - `RuntimeExecutor` → `ScreenDataShapeQueryRuntime` → `emission.data`
  - SSE receiver / dispatcher / projection runtime / render helper
  - frontend user-facing render が `projectionDefinition` を無視した場合に検出できる test
  - test は GitHub Actions workflow に含める
  - error 時に対象 lane / mapping / seed id が分かるログを出す

- [ ] Seed test 作成・拡張
  - demo / admin manifest seed に必要な `dispatcher_mapping`, `runtime_mapping`, `projection_constructor_mapping`, `screen_data_shape`, `db_notify_projection_mapping` が lane 前提として整合すること
  - `screen_data_shape.tableRef` と `topology.physical_tables` / `topology.wiring_physical_to_package` の整合を確認すること
  - seed 存在確認だけでなく、lane test が消費する形になっていること
  - seed を lane に流し、projection assertion まで到達すること
  - seed file を静的 parse して loop で lane harness に流すこと
  - seedData が無い場合に silent null fallback しないこと

- [ ] Test error を潰す
  - 追加・拡張した lane / seed test を GitHub workflow 上で通す
  - test harness / workflow / seed fixture / log / assertion 粒度の不備を潰す
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
