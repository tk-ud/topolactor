# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | not_started | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |
| `admin-relationship-active-manifest-targets` | Step 2.5 relationship / Step3 関連項目表示 | partial | 1 | `docs/design/admin-console-workflow-ssot.yaml` / `docs/design/db-schema.yaml` |
| `admin-contents-data-editor-conformance` | Step3 データ編集 / 型式診断 / CI Attention表層 | not_started | 4 | `docs/design/admin-console-workflow-ssot.yaml` / `docs/design/db-schema.yaml` |
| `search-aggregation-runtime-operator-contract` | Step3 read/query wiring runtime実行契約 / UIイベント接続 | not_started | 6 | `docs/design/admin-console-workflow-ssot.yaml` |
| `admin-frontend-normal-view-copy-polish` | Admin frontend 通常表示コピー調整 | not_started | 5 | `docs/design/admin-console-workflow-ssot.yaml` |
| `sql-attention-m7` | SQL Attention phase_vector 生成 | partial | 1 | `docs/design/sql-attention-logs-ssot.yaml` |

---

## Bundle `admin-contents-data-editor-conformance`

**Status:** not_started  
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/runtime-orchestration-ssot.yaml`

**実行前:** AGENTS.md を読む。

**残差の性質:** Step3 のデータ入力は手入力 `initialDataRows` と CSV/JSON import preview/apply が別 surface で、contents 上で同じ表として継続修正できない。型指定済み column に型式外値を保存できること自体は許容してよいが、型式外・nullable違反・未知列などを CI Attention / admin表層に非blocking warning として露出する checker/read model が Step3 manual path にはない。

**未実装 todo:**
- [ ] `/admin/contents` Step3 のデータ入力を `ContentsDataEditor` 等の共有コンポーネントへ切り出し、手入力行と CSV/JSON import preview/staged rows を同一グリッドで編集できるようにする
- [ ] `AdminImportRuntime.ValidateRow` / `ValidateFieldType` 相当を import 専用から `contentDataConformance` 等の共有 checker へ抽出・拡張し、manual `initialDataRows` と import rows の両方に同じ型式診断を適用する
- [ ] import snapshot/records 由来の行を contents Step3 で再読込・修正・再保存できる read/update API または staged data source 境界を定義し、manual row / imported row / edited row の source lineage を保持する
- [ ] 型式外値、nullable違反、未知列、relation由来項目の未解決を blocking 保存エラーではなく CI Attention / Step3 表層 warning として表示し、必要に応じて `/admin/manifests` / promotion前診断にも集約する

**対象ファイル候補:**
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/db-schema.yaml`
- `frontend/islands/ContentsScreenDesignPanel.tsx`
- `frontend/islands/AdminImport.tsx`
- `frontend/components/ContentsDataEditor.tsx`（新規候補）
- `frontend/lib/contentDataConformance.ts`（新規候補）
- `frontend/lib/manifestScreenDesign.ts`
- `frontend/lib/contentsAssign.ts`
- `frontend/api/adminApi.ts`
- `backend/runtime/AdminImportRuntime.cs`
- `backend/runtime/AdminRuntime.cs`
- `backend/repository/AdminImportRepository.cs`
- `backend/repository/NpgsqlAdminImportRepository.cs`
- Step3 data editor / import edit / conformance diagnostics tests

**完了条件:**
- 手入力で追加した行と CSV/JSON 取り込み後の行を、`/admin/contents` Step3 上で同じ編集グリッドから修正できる
- column `dataType` / `nullable` / relation field source に基づく型式診断が manual/import の両経路で同一に実行される
- 型式外値は保存可能だが、CI Attention / Step3 表層に非blocking warning として露出する
- import 由来行は source snapshot/record lineage を失わず、修正後データの保存・再診断ができる
- frontend/backend tests で manual row と import row の統一編集、型式外 warning 表示、保存ブロックしない挙動を固定する

---

## Bundle `admin-frontend-normal-view-copy-polish`

**Status:** not_started  
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`（v0.7.2 admin workflow / normal-view vocabulary）

**実行前:** AGENTS.md を読む。

**残差の性質:** admin frontend の構造は `コンテンツ → UIビルダー → ページ管理` の導線へ収束し、Step3 progressive disclosure も改善済み。ただし通常表示コピーに `pipeline`, `submit`, `layout / design`, `component design`, raw tab 名（例: `bucket`）, `add のみの既定セマンティクス`, legacy promote を連想させる「有効化」など、作業者には硬い内部寄り語彙が一部残っている。

**未実装 todo:**
- [ ] `/admin` / `adminGuides.ts` / `AdminMainFlowStepper` の通常表示から `pipeline`, `post-pipeline`, `layout / design` などの開発寄り表現を、ユーザー向けの「作業順」「配置」「デザイン設定」「保存反映」へ置換する
- [ ] `UiBuilderFlowStepper` / `UiBuilderAdmin` の通常表示で `submit`, `component design`, raw tab 名（`bucket` / `layout` / `design` / `css`）が主導線に出ないよう、表示ラベルをユーザー向けフェーズ名へ寄せる
- [ ] `/admin/contents` Step3 の通常表示コピーから `add のみの既定セマンティクス` など内部実装前提の文言を外し、「初期表示のデータ候補」「手入力 / CSV・JSON 取り込み」「プレビューして保存」に寄せる
- [ ] `/admin/manifests` の空状態・案内文で legacy promote / 有効化を連想させる表現を、現行導線（Step3保存 → UIビルダー → ページ管理）と矛盾しない文言へ揃える
- [ ] `frontend/tests/adminUxGuard.test.ts` などに normal-view copy guard を追加・更新し、上記の内部寄り語彙が details/技術情報以外へ再露出しないことを固定する

**対象ファイル候補:**
- `frontend/content/adminGuides.ts`
- `frontend/islands/AdminMainFlowStepper.tsx`
- `frontend/components/UiBuilderFlowStepper.tsx`
- `frontend/islands/UiBuilderAdmin.tsx`
- `frontend/islands/ContentsScreenDesignPanel.tsx`
- `frontend/islands/ManifestsAdmin.tsx`
- `frontend/tests/adminUxGuard.test.ts`

**完了条件:**
- admin通常表示の主導線が「何をする画面か」「次にどこへ進むか」をユーザー語彙で説明している
- 技術語彙・内部tab名・legacy promote連想語が通常表示の主導線から除去され、必要なものは `<details>` / 技術情報側へ隔離されている
- `adminUxGuard.test.ts` 等で通常表示コピーの退行が検知できる
- `deno check frontend/islands/ContentsScreenDesignPanel.tsx frontend/islands/UiBuilderAdmin.tsx frontend/islands/ManifestsAdmin.tsx frontend/components/UiBuilderFlowStepper.tsx` と関連 frontend tests が通る

---

## Bundle `search-aggregation-runtime-operator-contract`

**Status:** not_started  
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml` (step 3 `search_conditions` block)

**実行前:** AGENTS.md を読む。

**残差の性質:** `screen_data_shape` に保存された `searchConditions` / `havingConditions` / `displayColumnMode` は現在 Admin 投影（保存・topology intent round-trip）のみ実装済み。フロントエンド sample preview は評価を実施しているが、runtime entity（topology_transform_runtime）では WHERE 相当・HAVING 相当・displayColumnMode 反映が未実装。また Step3 の集計サンプルで作った search / aggregation / display read wiring が UI Builder の event/action binding 候補へ露出しておらず、検索条件・絞り込み条件の値も固定文字列寄りで runtime input 変数として接続できない。

**未実装 todo:**
- [ ] runtime entity 側で `searchConditions` を抽象演算子として解釈する契約をSSOT化する（`docs/design/admin-console-workflow-ssot.yaml` に `runtime_execution_contract` セクションを追加）
- [ ] `topology_transform_runtime` が `screen_data_shape.searchConditions` を読んで WHERE 相当のフィルタリングを実施する（SQL直書き禁止・operator vocabulary 経由）
- [ ] `topology_transform_runtime` が `screen_data_shape.havingConditions` を読んで集計後フィルタリングを実施する
- [ ] `topology_transform_runtime` が `screen_data_shape.displayColumnMode` に従って返却列を制限する（none=集計値のみ、selected=displayColumns、all=全列）
- [ ] Step3 の集計サンプルで作成した `searchConditions` / `havingConditions` / `aggregationMeasures` / `displayColumns` / `displayColumnMode` を read/query wiring として命名・保存し、UI Builder の component event/action binding から選択・接続できるようにする。条件値は固定 literal だけでなく `runtimeParam` / `operationInput` / `routeQuery` / `authClaim` / `formValue` 等の value source に分離し、preview 用 sample value と runtime 変数 binding を混同しない
- [ ] 上記 runtime entity 実装に対する backend 統合テストを追加する

**対象ファイル候補:**
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `frontend/islands/ContentsScreenDesignPanel.tsx`
- `frontend/islands/UiBuilderAdmin.tsx`
- `frontend/components/PackageWiringPanel.tsx`
- `frontend/lib/manifestScreenDesign.ts`
- `frontend/lib/contentsAssign.ts`
- `frontend/api/adminApi.ts`
- `backend/runtime/AdminRuntime.cs`
- `backend/runtime/TopologyTransformRuntime.cs` または同等 runtime entity
- Step3 read/query wiring と UI Builder event binding の frontend/backend tests

**完了条件:**
- `screen_data_shape.searchConditions` が runtime entity 実行時に WHERE 相当として解釈される
- `screen_data_shape.havingConditions` が集計後フィルタとして解釈される
- `screen_data_shape.displayColumnMode` が結果列の制御に反映される
- Step3 で preview 確認した read/query wiring が UI Builder のイベント接続候補として選択できる
- 条件値は sample preview literal と runtime value source が分離され、UI event の入力・route query・auth claim・form value から bind できる
- runtime execution と UI event binding に対応する backend / frontend テストが通る
- `docs/system-roadmap.yaml` の `frontend.admin_routes` / `known_gap_ref` から `search-aggregation-runtime-operator-contract` が除去される

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

## Bundle `admin-relationship-active-manifest-targets`

**Status:** partial  
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/runtime-orchestration-ssot.yaml`

**実行前:** AGENTS.md を読む。

- [ ] `/admin/contents` Step 3 の項目候補に、Step 2.5 relationship で接続した table の項目を表示・選択可能にする
  - 問題: Step 2.5 で draft/active manifest の table へ relationIntent を作成できても、Step 3 の `qualifiedColumns` / `columnKeys` が編集中 draft の `design.logicalTables` 由来のみの場合、接続先 table の項目が操作対象・表示列・検索/集計・初期データ/preview の候補に出ない。
  - 目的: Step 3 の項目候補を、編集中 draft の local logical tables だけでなく、Step 2.5 relationIntents で解決済みの draft remote / active remote table columns まで含む read model にする。
  - 改善方針: `relationIntents` の local/remote target を解決する Step 3 用 field source を追加し、候補集合に related table columns を合成する。未解決 remote は silent fallback せず blocking error または明示警告にする。
  - 対象ファイル候補: `docs/design/admin-console-workflow-ssot.yaml`, `frontend/islands/ContentsScreenDesignPanel.tsx`, `frontend/components/ContentsStep3FieldMatrix.tsx`, `frontend/lib/manifestLogicalTables.ts`, `frontend/lib/manifestScreenDesign.ts`, `frontend/lib/contentsAssign.ts`, `frontend/api/adminApi.ts`, `frontend/tests/adminUxGuard.test.ts`, relationship / Step3 frontend tests。
  - 完了条件: Step 2.5 で relation した draft/active table の項目が Step 3 の操作対象・表示列・検索/集計・サンプル表示の候補に出る。local/remote の同名項目が衝突せず、未解決 relation は silent fallback せず明示エラーになる。

---
## Bundle `recommendation-pressure-lane-boundary`

**SSOT:** 新規作成予定 `docs/design/recommendation-pressure-lane-ssot.yaml`
**関連SSOT:** `docs/design/sql-attention-logs-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/enum-dictionary-ssot.yaml`, `docs/design/db-schema.yaml`

**実行前:** AGENTS.md を読む。

**残差の性質:**
現在の recommendation engine は context route / operation / token prefix を中心に、hub 内の次操作候補を推薦している。一方で SQL Attention は logs / hub vector / projection pressure を観測し、次に注目すべき hub projection 候補を読む概念側の Attention 層である。
この2つを同じ recommendation として扱うと、hub間・projection間の概念推薦と、hub内の次候補推薦が混線する。

**責務境界:**

* SQL Attention

  * 役割: 概念自体の推薦
  * 対象: 次の hub projection 候補
  * 空間: hub間 / projection間 / topology概念空間
  * 出力: どの hub / projection / topology を次に見るべきか
* Recommendation Engine

  * 役割: hub内の次候補推薦
  * 対象: operation / enum item / component / route action
  * 空間: 現在hub内部の操作圧力・状態圧力
  * 出力: このhub内で次に何を選ぶ・押す・遷移するべきか

**未実装 todo:**

* [ ] `docs/design/recommendation-pressure-lane-ssot.yaml` を作成し、SQL Attention と Recommendation Engine の責務境界を定義する
* [ ] SQL Attention は「次の hub projection 候補」を返す概念推薦 lane として定義する
* [ ] Recommendation Engine は「現在 hub 内の次候補」を返す hub-local recommendation lane として定義する
* [ ] UI操作圧力 recommendation lane を定義する

  * source: `context_event`, `component_operation_event_log`
  * output: `next_operation`, `next_component`, `next_route_action`
* [ ] 状態圧力 recommendation lane を定義する

  * source: `logs.diff`, enum transition logs
  * output: `next_enum_item`, `likely_status`, `state_shift_candidate`
* [ ] enum_group + selected item index を状態圧力の線形空間座標として扱う契約を `enum-dictionary-ssot.yaml` と接続する
* [ ] operation transition stats と enum item transition stats を混同しない保存境界を定義する
* [ ] SQL Attention の projection recommendation を Recommendation Engine の hub-local candidate recommendation へ直接混入させない
* [ ] 必要なら `context_transition_stats` を `transition_kind` で汎用化するか、`context_enum_transition_stats` を別テーブルとして定義する
* [ ] recommendation result に `lane: ui_pressure | state_pressure` を持たせ、UI側で混線しないようにする
* [ ] SQL Attention result には `lane: sql_attention_projection` 等を持たせ、hub projection 候補であることを固定する
* [ ] 責務境界の regression test / SSOT vocabulary guard を追加する

**完了条件:**

* SQL Attention が「どの hub projection を見るか」の概念推薦として定義されている
* Recommendation Engine が「現在hub内で何を選ぶか」の候補推薦として定義されている
* UI操作圧力と状態圧力が別laneとして並列に扱われている
* enum index 線形空間による状態遷移推薦が operation recommendation と混線していない
* SQL Attention の出力と Recommendation Engine の出力が型・lane・SSOT上で区別されている
* tests / guard により、SQL Attention と hub-local recommendation の責務混同が検知できる


---
## Bundle `sql-attention-m7`

**Status:** partial  
**SSOT:** `docs/design/sql-attention-logs-ssot.yaml`, `docs/design/sql-attention-logs-ssot.md`

- [ ] phase_vector generation implementation（manifest / policy cap 由来ではない phase shift 候補ベクトル生成 — hubs 空間探索結果のみ使用）
