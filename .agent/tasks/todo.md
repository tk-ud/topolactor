# Agent Task List

未処理は **bundle 単位**で実装・レビューする。完了済みは末尾アーカイブ。

## 未処理 bundle 索引

| Bundle ID | 名称 | 件数 | 主 SSOT |
|-----------|------|------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |
| `user-login-seed-manifest-auth-boundary` | 通常ユーザログイン seed manifest / 認証境界 | 1 | `docs/design/runtime-orchestration-ssot.yaml` / auth DB SSOT（要追記） |
| `admin-relationship-active-manifest-targets` | Step 2.5 relationship 有効manifest参照 / Step3関連項目表示 | 2 | `docs/design/admin-console-workflow-ssot.yaml` / `docs/design/db-schema.yaml` |
| `admin-contents-data-editor-conformance` | Step3 データ編集 / 型式診断 / CI Attention表層 | 4 | `docs/design/admin-console-workflow-ssot.yaml` / `docs/design/db-schema.yaml` |
| `search-aggregation-runtime-operator-contract` | Step3 read/query wiring runtime実行契約 / UIイベント接続 | 6 | `docs/design/admin-console-workflow-ssot.yaml` |
| `admin-frontend-normal-view-copy-polish` | Admin frontend 通常表示コピー調整 | 5 | `docs/design/admin-console-workflow-ssot.yaml` |

---

## 共通参照（完了 admin bundle の回帰時）

**方針（owner）:** SSOT 準拠でよい。設計意図に反する語彙・UI の残存は危険。収束は**反意図の削除・置換**を優先。

| パス | 読む節 |
|------|--------|
| `docs/design/admin-console-workflow-ssot.yaml` | v0.7.2 — `canonical_sequential_authoring_pipeline`, `edit_target_contract`, `admin_contents`, `admin_ui_builder` |
| `docs/design/runtime-orchestration-ssot.yaml` | `frontend_routes.admin` |
| `docs/framework-policy.yaml` | `ui_topology_tensor_persistence` |
| `docs/design/db-schema.yaml` | `manifest`, `packages`, `components_layout_design`, `components_style_design`, `ui_component_bucket` |

---
## Bundle `admin-contents-data-editor-conformance`

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

**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

- [ ] Notion/Sheets/Slack/GitHub/Webhook/REST-API-Connector/NoCode-Loop — 個別 SSOT 揃うまで実装しない

---

## Bundle `helper-manual`

**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

- [ ] helper/manual category 候補の実装設計
- [ ] Desktop AI / CLI / MCP Reader 向けライティング方針
- [ ] ヘルプコンポーネント実装（SSOT カテゴリ構造ゲート）

---

## Bundle `product-nocode-loop-acceptance`

- [ ] `product.dynamic_support_nocode_loop` 手動受入（roadmap 追従）

---

## Bundle `user-login-seed-manifest-auth-boundary`

**SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, auth DB/session/credential SSOT（要追記または既存正本特定）

**実行前:** AGENTS.md を読む。

- [x] 通常ユーザ向けログイン UI を seed manifest として作成し、認証コアは既存 AuthService/Auth runtime に委譲する境界をSSOT化・実装する
  - 問題: admin 操作用ログインと通常ユーザ向けログインを topology manifest で扱う場合、JWT 署名・password hash・refresh token・admin 権限判定まで topology/hub/jsonb 側へ混入すると security boundary が崩れ、admin/user realm が混線する危険がある。現状の demo auth は `topology.function_parameters(demo_auth/demo_users)` に bcrypt hash を置く仮実装であり、auth 専用DB正本ではない。
  - 目的: 通常ユーザ向けログイン画面はデータ駆動 UI として seed manifest で提供しつつ、credential 検証・password hash・JWT 署名・refresh token・session invalidation は既存ログイン基盤と auth 専用DB正本に隔離する。
  - 改善方針: まず auth DB/session/credential 境界のSSOTを追記または既存正本を特定し、`login UI topology manifest` と `AuthService/Auth runtime` の責務を分ける。auth DB 正本として `auth.users`, `auth.credentials`, `auth.sessions` / `auth.refresh_tokens`, `auth.login_events`, `auth.roles` / `auth.scopes` / `auth.grants` 相当の責務を定義する。`topology.function_parameters` の `demo_auth/demo_users` credential は demo-only として隔離し、通常ユーザ認証の正本にしない。seed は `/login` 等の user-facing manifest、入力フィールド、submit action binding、成功/失敗表示、遷移先だけを作る。submit は `auth_runtime.login` 等の既存認証 action に委譲し、claims には `realm=user`, `audience=user_app`, `scope/role=user` を付与する。admin は `realm=admin/system`, `audience=admin_console`, `scope/role=admin` として分離する。
  - 作らないもの: topology manifest 内の password_hash、JWT secret、token signing、refresh token 永続化、admin 判定ロジック、credential DB 直書き。`topology.function_parameters` を通常ユーザ credential store として使う実装も禁止する。
  - 対象ファイル候補: `docs/design/runtime-orchestration-ssot.yaml`, auth DB/session/credential SSOT（新規または既存）, `db/schema.sql` または auth DB migration, seed 実装ファイル, auth runtime/API, frontend login manifest/rendering tests。
  - 完了条件: auth 専用DB正本がSSOT化され、seed によって通常ユーザ向け login manifest が生成され、ログイン submit は既存認証基盤へ委譲される。admin/user realm・audience・scope が分離され、password hash/JWT secret/refresh token が topology/hub/jsonb/`function_parameters` に保存されないことをテストで固定する。

---

## Bundle `admin-relationship-active-manifest-targets`

**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/runtime-orchestration-ssot.yaml`

**実行前:** AGENTS.md を読む。

- [x] Step 2.5 relationship の接続先として有効manifest/tableを選択可能にする
  - 問題: 現状の Step 2.5 relationship UI/データ配線が編集中 draft manifest 配下のテーブルだけを参照点として扱うと、編集中マニフェストから既存の有効マニフェスト上のテーブルへ接続する関係を作れない。データ駆動OSでは既存有効トポロジへの参照が閉じると、manifest間・既存データ間の連続性を表現できない。
  - 目的: 編集中 manifest の local side は draft 配下の logical tables のみを参照しつつ、remote/target side は published/active topology manifests とその table refs も選択できるようにする。
  - 改善方針: まず `docs/design/admin-console-workflow-ssot.yaml` の Step 2.5 relationship_configuration に、local side は current draft manifest scoped、remote side は current draft tables に加えて active/published manifests の table refs を選択可能、という境界を追記する。その後 frontend の relationship selector と backend intent validation/read model を更新し、remote target が active manifest table である場合も fail-close で解決・保存できるようにする。
  - 対象ファイル候補: `docs/design/admin-console-workflow-ssot.yaml`, `frontend/islands/ContentsScreenDesignPanel.tsx`, `frontend/lib/contentsAssign.ts`, `frontend/api/adminApi.ts`, `backend/runtime/AdminRuntime.cs`, `backend/repository/ManifestRepository.cs`, `backend/repository/NpgsqlManifestRepository.cs`, relationship / manifest management tests。
  - 完了条件: Step 2.5 UI で local side は編集中 manifest の配下テーブルに限定され、remote side は有効manifest/tableも選択できる。backend は remote target の manifest/table_ref を検証し、未解決時は silent fallback せず blocking error にする。テストで draft-only 固定への退行を防ぐ。
- [ ] `/admin/contents` Step 3 の項目候補に、Step 2.5 relationship で接続した table の項目を表示・選択可能にする
  - 問題: Step 2.5 で draft/active manifest の table へ relationIntent を作成できても、Step 3 の `qualifiedColumns` / `columnKeys` が編集中 draft の `design.logicalTables` 由来のみの場合、接続先 table の項目が操作対象・表示列・検索/集計・初期データ/preview の候補に出ない。これにより Step 2.5 で作った relationship が Step 3 の表示設計へ投影されず、relation 設定と画面項目設計が分断される。
  - 目的: Step 3 の項目候補を、編集中 draft の local logical tables だけでなく、Step 2.5 relationIntents で解決済みの draft remote / active remote table columns まで含む read model にする。local 項目と関連 table 項目は区別可能な key / label で表示し、同名 column の衝突を避ける。
  - 改善方針: `relationIntents` の local/remote target を解決する Step 3 用 field source を追加し、`qualifiedColumnsFromLogicalTables` 相当の候補集合に related table columns を合成する。draft remote は `design.logicalTables` から、active remote は `listRelationshipRemoteTargets` の logicalTables から取得する。未解決 remote_manifest_id / joinTableRef / remoteKey / column は silent fallback せず blocking error または明示警告にし、`ContentsStep3FieldMatrix`, displayColumns, search/aggregation selectors, initialDataRows, `SamplePreviewPanel`, import preview の候補集合を同じ read model に揃える。
  - 対象ファイル候補: `docs/design/admin-console-workflow-ssot.yaml`, `frontend/islands/ContentsScreenDesignPanel.tsx`, `frontend/components/ContentsStep3FieldMatrix.tsx`, `frontend/lib/manifestLogicalTables.ts`, `frontend/lib/manifestScreenDesign.ts`, `frontend/lib/contentsAssign.ts`, `frontend/api/adminApi.ts`, `frontend/tests/adminUxGuard.test.ts`, relationship / Step3 frontend tests。
  - 完了条件: Step 2.5 で relation した draft/active table の項目が Step 3 の操作対象・表示列・検索/集計・サンプル表示の候補に出る。local/remote の同名項目が衝突せず、保存 payload に relation 由来 field key が保持される。未解決 relation は silent fallback せず明示エラーになる。テストで「Step2.5 relation 済み table 項目が Step3 に表示される」退行を固定する。

---



## 完了済みアーカイブ

### `admin-main-flow-step-ssot-alignment`（2026-06）

- [x] `/admin` トップの `ADMIN_MAIN_FLOW_STEPS` / `AdminMainFlowStepper` / `adminMainFlow.test.ts` を canonical admin workflow へ再整合（`/auth` 除外、contents subSteps 1/2/2.5/3）

### `admin-contents-data-input-import-subfeature`（2026-06）

- [x] `/admin/contents` step 3 データ入力に手入力 + `AdminImportPanel`（CSV/JSON preview→apply）を統合
- [x] `admin-console-workflow-ssot.yaml` に data_input_subfeature を明文化

### `ui-builder-layout-design-boundary`（2026-06）

- [x] `UiBuilderAdmin` layout / design タブ分離、`layout_patch` から cssTokenRefs 除去、backend strip + テスト

### `ssot-old-vocabulary-cleanup`（2026-06）

- [x] `dispatcher_legacy_alias: dispatchar` 削除（runtime-orchestration-ssot.yaml）
- [x] `ExistingSystemChangeIntake*` / `existing_system_intake` 語彙、JWT role claim 固定
- [x] `ExploreLegacyHubCurrentSupportCacheDiagnosticsAsync` 削除、テストは `ExploreAsync`（hubs.hub_relations）へ
- [x] `RuntimeJumpEvent` JSON `from`/`to`/`planned` 投影と SSOT 突合

### `main-data-wiring-ssot-audit`（2026-06）

- [x] `NpgsqlTopologyRepository` の demo transition 永続化参照を canonical `topology.demo_state_transitions` に統一

### `ui-topology-package-bucket-vector`（2026-06）

- [x] `package_schema_json.bucketItemIds` と 1 route = 1 `package_key`（`{routeKey}:pkg`）を契約固定
- [x] `package_generator:promote_package`（`routeKey` + `bucketItemIds[]`）— 1 トランザクションで 1 package・複数 `ui_package_component_map`・1 tensor
- [x] `generate` を全 `bucketItemIds` に対して実行してから promote（`packaging` 前提の整合）
- [x] `PackageGenerateBatchResponseDto` に `bucketItemIds[]` / `componentIds[]` 配列、`list_packages` が component_ids / bucket_item_ids を投影
- [x] Step 4.1 複数選択 → 1 回の `promote_package` submit（ループ promote 廃止）；Step 4.2 は返却 `packageId` 1 件を編集ルートに固定
- [x] 統合テスト（2 bucket → 1 package、map 2 行、tensor 1 行）+ package generate テスト更新
- [x] `package_schema_json` vector 上書き禁止 — 既存配列と今回 batch を union/dedup（後追い追加テスト付き）
- [x] `ui_package_component_map` 重複防止 — `slot_key='default'` canonical + `NULLS NOT DISTINCT` 制約（migration 追加）
- [x] mixed selection 状態表示 — promote 件数と skip 件数を分けて表示

### `admin-v072-audit-followup`（2026-06）

- [x] `audit-component-design-ui` — PackageDesignPanel: classname / tailwind / cssTokenRefs / reactionIntent + upsert payload
- [x] `audit-contents-step-payload` — `contentsAssign.ts` step 専用 payload（existing から非ステップ項目を保持）
- [x] `audit-ui-builder-aux-tabs` — catalog / CI を `<details>` 参照専用化、編集ルートバナー常設
- [x] `audit-layout-patch-package-gate` — `layout_patch` に `packageId`、tensor 所属検証、apply WHERE に package 固定
- [x] `audit-docs-v072-sync` — `adminGuides` / `UiBuilderFlowStepper` v0.7.2 文言同期

### `admin-v072-convergence`（2026-06 — plan WU1–WU5 + 続き）

- [x] `admin-ux-feedback` — `adminSubmitUx.ts` / `AdminSubmitStatus`、confirm・loading・status 統一
- [x] `admin-contents-v072` — Contents pipeline stepper 1/2/2.5/3、multi-op、operationEntityBindings、legacy promote を details へ
- [x] `admin-ui-builder-v072` — FlowStepper 4.1/4.2、複数選択パッケージ化、package スコープ layout、wiring 編集 API/UI、`component_style_design` dispatch（design 通常 UI の深度は `admin-v072-audit-followup` へ）
- [x] `admin-guides-regression` — `adminGuides.ts` / `ADMIN_MAIN_FLOW_STEPS` / `ADMIN_ROUTE_CARDS` v0.7.2、ux guard・mainFlow テスト拡張
- [x] `HubNavigationAdmin` — create/update/delete confirm

### `admin-blocking-verify`（merge 時確認）

- [x] `/admin/*` ルート registry — `runtime-orchestration-ssot.yaml`
- [x] contents / manifests 責務分割
- [x] contents promote guard fail-close
- [x] `table_ref` SSOT wiring
- [x] hub navigation on `/admin/manifests`
- [x] `ManifestScreenOperationDeriver` manifest-scoped axes

### `frontend.admin_routes`（旧 roadmap bundle — 実装済みだが v0.7.2 と乖離あり）

- [x] contents wizard 前半・列型 select・初期データ・relation・search・集計 UI
- [x] backend `screen_data_shape` 拡張・`WIRING_TABLE_REF_NOT_FOUND`
- [x] ui-builder catalog/CSS/wiring/apply/CI（**v0.7.2 package ルート・bucket ベクトルは `ui-topology-package-bucket-vector` bundle**）
- [x] UX 語彙・ContentsPromotionPanel ステップ表示

### `admin_visual_layout_builder`

- [x] layoutId round-trip・responsive token UI・tests

### `cli-mcp-port-ssot` / `core-runtime-bundle-ssot`

- [x] CLI/MCP 実装 SSOT・Email/Stripe/File/Export bundle SSOT
- [x] Webhook/Job/Audit/Secret bundle SSOT

### `legacy-debug-isolation`

- [x] `/dev/admin/*` wrapper 削除

### `sql-attention-m7`

- [x] SQLA-IDSPACE-STEP3/4
- [ ] phase_vector generation implementation（manifest / policy cap 由来ではない phase shift 候補ベクトル生成 — hubs 空間探索結果のみ使用）