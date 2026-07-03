# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `in_progress` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `helper-manual` | helper reference artifact / admin helper projection | not_started | 1 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 2 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `aggregate-trigger-substrate` | 集計トリガー基盤 | in_progress | 1 | - | `docs/design/runtime-orchestration-ssot.yaml` |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (email / audit_approval) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。UI Builder form preset は docs/design/ui-builder-preset-ecosystem-ssot.yaml / db/physical_search_crud_aggregate_preset_seed.sql の CRUD preset seed の写像/派生であり、新規 UI runtime / 専用 component 実装ではない。

---

## Bundle `helper-manual`

**Status:** not_started
**Roadmap/status SSOT:** `product.helper_manual_policy`
**Primary SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`
**Design prerequisite status:** schema / seed / admin helper viewer 実装前に `docs/design/user-facing-helper-manual-ssot.yaml` の clone lifecycle reference contract を正本として読むこと。

目的:
helper/manual をユーザー向け文章方針だけでなく、人間 / Agent / MCP / External AI / Local LLM / admin UI が共通参照する JSON helper reference artifact として実装可能にする。現行MCPの import-candidate / draft_operation / commit_candidate lane に topology authoring draft を載せる参照点を作り、admin では同じ内容を viewer として表示する。最新 `/admin/contents` Step 1 の `create_new_topology` / `clone_active_as_replacement_draft` / `clone_active_as_new_topology_draft`、`draft_origin`、`clone_mode`、replacement merge authority boundary、SQL Attention candidate boundary、`layoutPatchDraft` と production manifest replacement merge の分離を JSON contract 上で混同しない。

残問題:
- `helper_reference_artifact` の schema / seed がまだ無い。実装前に `admin_topology_clone_lifecycle_reference_contract` / `replacement_merge_authority_boundary` / `sql_attention_candidate_reference_boundary` / `layout_patch_draft_vs_manifest_replacement_boundary` を schema required / strongly-recommended fields へ写像する必要がある。
- MCPで topology authoring draft を構築する際の `structured_output_payload` / `assigned_business_object_candidate` / `assignment_target_scope` / `preview_diff` / `unresolved_fields` の具体例が未実装。`entry_mode` / `draft_origin` / `clone_mode` / `source_active_manifest_id` / `source_active_evidence` / `lineage_evidence_only` / `replacement_merge_intent` / `replacement_merge_blockers` / `backend_merge_authority` を含め、replacement clone と clone-as-new topology を payload 上で混同しない必要がある。
- admin 共通ヘッダから開く helper viewer が未実装。viewer は clone lifecycle badge、replacement-vs-lineage-only badge、backend authority notice、stale source / active identity conflict blocker、SQL Attention candidate boundary、layout patch not replacement merge notice を表示する必要がある。
- helper viewer が admin submit / apply / promote / approval / merge target decision / active mutation を実行しない projection-only surface であることを実装上確認する guard が無い。
- AI/MCP由来 candidate evidence、SQL Attention candidate、人間の admin 手作業 draft、manual replacement clone draft、clone-as-new topology draft の origin / lineage / authority を混同しない表示・保存・監査境界が未検証。
- source evidence / lineage evidence だけで replacement authority を得ないこと、replacement merge は backend AdminRuntime / ManifestRepository transaction のみが source evidence・validation・diff/log evidence・stale source check・active identity conflict check 後に existing active row update + working draft row delete として成立することを schema / seed / tests へ渡す必要がある。
- `layoutPatchDraft` / `layout_patch:apply` は UI Builder layout draft / layout persistence であり production manifest replacement merge ではない、という helper artifact 上の boundary が未実装。

改善方針:
implementation_change で、SSOTに従って helper schema / seed artifact を追加し、admin common header から Drawer helper viewer を開けるようにする。viewer は検索・カテゴリ選択・tree viewer・detail modal mount と clone lifecycle boundary 表示までに限定し、runtime/admin/MCP authority を持たせない。MCP新規tool surfaceは作らず、既存 import-candidate lane の payload reference として実装する。schema / seed は `create_new_topology`、`clone_active_as_replacement_draft`、`clone_active_as_new_topology_draft`、`manual_new`、`manual_clone_replacement`、`manual_clone_new_topology`、`sql_attention_candidate`、`none`、`replacement`、`new_topology` を明示し、SQL Attention candidate は explicit human/admin adoption まで candidate/evidence surface に留める。

対応資料:
- `docs/design/user-facing-helper-manual-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`
- `docs/design/cli-mcp-port-implementation-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/ci-contract-ssot.yaml`
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `docs/framework-policy.yaml`

対象ファイル名候補:
- `docs/helper/helper-manual.schema.json` (new helper artifact schema)
- `docs/helper/helper-manual.seed.json` (new helper reference seed)
- `frontend/islands/*Admin*.tsx` or common admin shell/header files (helper launch button)
- `frontend/islands/*Helper*.tsx` or future `AdminHelper*` component files
- `frontend/lib/*helper*` (helper artifact load/filter/tree utility)
- `backend/runtime/AuthorizedCliReaderPortRuntime.cs` (reference only; MCP operation expansionは原則しない)
- `backend/tests/Topolactor.Runtime.Tests/AuthorizedCliReaderPortRuntimeTests.cs` (reference only; candidate origin regression確認)

対象ブロック名:
- `helper_reference_artifact`
- `admin_topology_clone_lifecycle_reference_contract`
- `replacement_merge_authority_boundary`
- `sql_attention_candidate_reference_boundary`
- `layout_patch_draft_vs_manifest_replacement_boundary`
- `mcp_topology_authoring_draft_reference`
- `admin_helper_projection`
- `provenance_boundary`
- `user_facing_message_policy`
- `language_policy`
- `helper_manual_category_candidates`
- `safety_boundary`
- `relation_to_other_ssot`

対象関数名候補:
- future `loadHelperManualSeed`
- future `filterHelperManualItems`
- future `buildHelperManualTree`
- future `renderHelperManualDetail`
- future `openAdminHelperDrawer`
- future `mountDetailHelperModal`
- future `renderCloneLifecycleBadge`
- future `renderReplacementAuthorityNotice`
- future `renderLineageOnlyBoundaryNotice`
- future `renderSqlAttentionCandidateBoundaryNotice`
- future `renderLayoutPatchNotReplacementMergeNotice`

残受入条件:
- [ ] helper schema / seed artifact が追加され、SSOTの required fields と clone lifecycle reference contract を満たしている。
- [ ] helper seed に admin authoring flow / admin topology clone lifecycle / MCP topology authoring draft / UI Builder / CI Attention / approval boundary のカテゴリがある。
- [ ] internal vocabulary と user-facing vocabulary の対応が helper artifact に定義されている。
- [ ] MCP topology authoring draft の payload example が、既存 import-candidate lane の field に対応している。
- [ ] `create_new_topology` / replacement clone / clone-as-new topology が JSON contract 上で混同されない。
- [ ] source evidence / lineage evidence が replacement authority ではないことを schema / seed / viewer 表示で確認できる。
- [ ] SQL Attention candidate が explicit adoption 前に draft row / production merge authority へ化けない。
- [ ] `layoutPatchDraft` / `layout_patch:apply` が production manifest replacement merge ではないことを artifact と viewer で確認できる。
- [ ] admin common header から helper Drawer を開け、検索・カテゴリ選択・tree viewer・detail modal が使える。
- [ ] helper viewer は admin submit / apply / promote / approval / merge target decision / active mutation / MCP operation を実行しない。
- [ ] AI/MCP由来 candidate evidence と human manual admin draft の origin を混同しない表示・監査境界が確認できる。
- [ ] 新規 MCP tool surface / admin submit direct execution / active topology mutation は追加していない。

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する
- [ ] `product.admin_topology_authoring` の `/admin/contents` Step 1 3 entry mode → clone draft → edit → backend replacement merge を、統合UX手動受入 / hand-debug evidence として確認する

---


## Bundle `aggregate-trigger-substrate`

**Status:** in_progress
**SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/abstract-function-primitive-registry-ssot.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/ssot-map.yaml`

閉鎖済み範囲(実コード・live PostgreSQL executable proof で確認済み、proof manifest `aggregate_trigger_substrate_live_db_executable_proof` / `aggregate_trigger_substrate_step3_dynamic_authoring_proof` 参照):
- Npgsql transaction boundary: `event_append_aggregate_upsert_and_materialization` の場合、aggregate current upsert と materialization evidence append が単一 backend transaction(`AggregateTriggerRepository.AtomicUpsertAndMaterializeAsync`)で実行される。live DB で xmin 一致による同一トランザクション証跡を確認済み。`transaction_boundary` の他2値(`event_append_only` / `event_append_and_aggregate_upsert`)も runtime 側で実行深度をゲートする。
- live PostgreSQL executable proof: `db/runtime_orchestration_tables.sql` を適用した live PostgreSQL 上で event append 冪等性・aggregate upsert・threshold 評価・materialization evidence・duplicate guard を検証(`backend/tests/Topolactor.Integration.Tests/AggregateTriggerRepositoryLiveDbTests.cs`)。`.agent/tests/check-backend-tests.sh` / `.github/workflows/backend-tests.yml` / `.agent/tests/check-runtime-environment.sh` に登録済み。
- DB-backed manifest route proof: `AggregateTriggerSubstrateRouteLiveDbTests` は fake manifest repository ではなく、`manifest` テーブルへ実際に INSERT した active row を `NpgsqlManifestRepository.ResolveActiveManifestAsync` が live query で解決する経路を証明する(該当 row を deprecated に更新すると解決不能になることも確認済み)。
- AdminRuntime definition persistence route(完全 round-trip): `AdminRuntime.AssignScreenDataShapeAsync` が検証成功した aggregate trigger definition を `runtime_orchestration.aggregate_trigger_definitions`(正本永続化先)へ `AggregateTriggerRepository.SaveDefinitionAsync` 経由で保存する。`accepted_event_schema_ref` / `allowed_source_kinds_json` / `materialization_policy_ref` を専用列として追加し、空値復元せず完全 round-trip する(`db/runtime_orchestration_tables.sql`, `docs/design/db-schema.yaml` required_columns 追従済み)。`screen_data_shape` 側の格納は Step3 フォーム再表示用の authoring draft projection として残置(実行 authority ではない)。live DB proof: `backend/tests/Topolactor.Integration.Tests/AdminRuntimeAggregateTriggerDefinitionPersistenceLiveDbTests.cs`。
- cron/hook/client scheduler-to-manifest DB route: `RuntimeTimelineScheduler`(cron/hook/client 全trigger種別が同一経路を通る既存基盤)-> `ManifestDispatcher`(DB-backed manifest resolution)-> `AggregateTriggerRuntime` -> `NpgsqlAggregateTriggerRepository` の代表経路を live DB で証明(`backend/tests/Topolactor.Integration.Tests/AggregateTriggerSubstrateRouteLiveDbTests.cs`)。
- full event -> aggregate -> threshold -> materialization proof: threshold_not_satisfied / materialized / duplicate_event_evidence / duplicate_materialization_guard / approval_required→granted の各シナリオを live DB で確認済み(同上テストファイル)。
- DB SSOT追従: `docs/design/db-schema.yaml` の `aggregate_trigger_table_contracts.status` を更新し、`canonical_bootstrap_files` / `source_files` / `required_columns` に `db/runtime_orchestration_tables.sql` と新規3列を追記。
- admin/contents Step3 構造化 authoring(processing_function_scope の registry 選択を除く): `conflict_key_fields`(選択済み aggregate target の宣言済み field からの multi-select)、`delta_map`(counter 名/量の repeatable row、safe-identifier 検証)、`threshold_policy`(minimum_trial_count / target_ratio / comparison_operator の構造化入力、ratio_numerator_field / ratio_denominator_field は delta_map counter からの select)、`materialization_payload_map`(materialization target の宣言済み field への target_field select + source 種別に応じた source_field/constant_value picker)を `AggregateTriggerAuthoringPanel.tsx` に実装。`execution_scope` / `transaction_boundary` / `approval_policy` も固定値ではなく構造化 selector として公開(transaction_boundary の runtime gating 追加に伴う機能断絶の是正)。`ContentsScreenDesignPanel.tsx` の Step2 logicalTables.columns / Step2.5 relationIntents.localKey・remoteKey を `StepTarget.fields` として配線し、conflict_key_fields / materialization_payload_map の候補フィールドとして利用可能にした。
- proof manifest追従: `docs/design/test-proof-manifest-ssot.yaml` に `aggregate_trigger_substrate_live_db_executable_proof` と `aggregate_trigger_substrate_step3_dynamic_authoring_proof`(いずれも executable/integration proof)を追加し、`aggregate_trigger_substrate_executable_proof_gap`(known_gap)を当時判明していた残 scope のみへ縮小。`.agent/docs/test-bundles.yaml` 追従済み。
- processing_function_scope の registry authority(`aggregate_trigger_substrate_processing_function_authority_proof` 参照): `function_id` は既存正本 `topology.abstract_function_manifests.function_key` から `NpgsqlAbstractFunctionManifestRepository.LoadAsync` で解決し、`AggregateTriggerDefinitionValidator.ValidateProcessingFunctionAuthorityAsync` が missing / inactive / runtime_lane 不一致(`aggregate_trigger_runtime` を `runtime_lane` CHECK 制約に追加)/ authority_scope 不一致(`operation_definition_id` との一致)を fail-close する。`operation_definition_id` は既存の abstract_function_authority_bindings 等の別 surface には対応する正本写像が存在しなかったため、`AdminRuntime.AssignScreenDataShapeAsync` が manifest の `topologySystemName`(admin/contents Step1 由来、Step3 が owning_step とする既存 SSOT 文言と整合)から導出し、クライアント送信値を上書きする形で backend authority を確立した。frontend の `operation_definition_id` 入力は編集不可の導出値表示に変更(`AggregateTriggerAuthoringPanel.tsx` に `topologySystemName` prop を追加し `ContentsScreenDesignPanel.tsx` から配線)。live DB proof: `backend/tests/Topolactor.Integration.Tests/AggregateTriggerProcessingFunctionAuthorityLiveDbTests.cs`(5 tests)、`AdminRuntimeAggregateTriggerDefinitionPersistenceLiveDbTests.cs` の registry 登録 / 未登録 fail-close 2 tests、`AggregateTriggerSubstrateRouteLiveDbTests.cs` の cron/hook/client 4 シナリオが実 registry row を seed してこの authority check を経路上で実行するよう更新済み(以前は param が null で check を skip していた)。

残 scope(`aggregate_trigger_substrate_executable_proof_gap` known_gap 参照、未閉鎖 — 上記以外はすべて閉鎖済み):
- [ ] admin/contents Step3 の `processing_function_scope.function_id` は backend authority(`topology.abstract_function_manifests` registry, fail-close)により検証されるが、frontend 入力自体は依然 safe-identifier 検証済みの自由入力テキストであり、登録済み function を列挙する live dropdown ではない。理由: `topology.abstract_function_manifests` を `runtime_lane=aggregate_trigger_runtime` で絞り込んで一覧表示する admin 用 listing operation が AdminRuntime / adminApi にまだ存在しない。存在しない listing operation を本 selector のためだけに捏造することは authority データの偽装になるため、実施していない。`operation_definition_id` は本ラウンドで backend 導出値の読み取り専用表示になったためこの残 scope から除外(閉鎖済み)。
  - 目的/判断事項: 登録済み function_key を選択させる dropdown UX を追加するかどうかは、`topology.abstract_function_manifests` を一覧する新規 admin listing operation の新設を伴う product/design 判断であり、本 Bundle の implementation_change scope 単独では決定しない。backend fail-close は既に有効なため、誤った function_id を選んでも persist されない。
  - 対応資料: `docs/design/admin-console-workflow-ssot.yaml`(`aggregate_trigger_step3_extension_contract`)、`docs/design/abstract-function-primitive-registry-ssot.yaml`
  - 対象ファイル: `frontend/components/AggregateTriggerAuthoringPanel.tsx`, `frontend/lib/aggregateTriggerAuthoring.ts`
  - OK軸: dropdown を追加する場合は新規 admin listing operation を経由した構造化 selector とし、backend が引き続き validation authority を持つこと。
  - NG軸: 存在しない listing operation をでっち上げて selector の見た目だけ整える。この known_gap を証拠なく削除する。
