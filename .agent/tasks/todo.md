# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `in_progress` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `helper-manual` | helper reference artifact / admin helper projection | not_started | 1 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 2 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `test-orchestration-review` | Seed conversion後の proof / test orchestration review | not_started | 1 | proof surface carry-over | `docs/design/pipeline-continuity-ssot.yaml` |
| `admin-console-workflow-step-wording-boundary` | Seed conversion後の admin console workflow wording boundary | not_started | 1 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml` |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (email / audit_approval) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。UI Builder form preset は docs/design/ui-builder-preset-ecosystem-ssot.yaml / db/physical_search_crud_aggregate_preset_seed.sql の CRUD preset seed の写像/派生であり、新規 UI runtime / 専用 component 実装ではない。

注: `frontend-canonical-surface-structure-label-boundary` は PR#610 により完了済み（wiring inspector canonical taxonomy統一・internal_api projection接続・canvas workspace sequential framing除去・panel fixed/docked化・normal label / technical disclosure boundary）。5ラウンドの監査を経て全SSOT scope整合を確認 — 3ラウンド目時点の完了宣言後、全体監査で `topology_naming_ssot.user_facing_topology_label.display_rule`（visibleName = userFacingTopologyLabel ?? topologySystemName）が `/admin/manifests` / hub_navigation production projectionへ届いていないcross-layer gapと、`/auth`・`/super_auth` normal error pathへraw diagnostic messageがprimary表示されるgapが見つかり、4ラウンド目で解消（新規DBカラムは追加せず、既存 `hubs.topology_manifests.topology_jsonb` の screen_data_shape entryを読む形で解決）。5ラウンド目の全体監査で、naming field双方欠落時にSSOT未定義の `?? manifestKey` をnormal primaryへ昇格していた残fallbackと、`HubNavigationAdmin` のlifecycle（create/update/deprecate/reorder）error pathがraw internal vocabulary（related_hub_id/hub_relations/topology manifest等）を含むbackend messageをそのままnormal primary表示していた残件を発見・解消（fail-close friendly placeholder採用、raw manifestKey/error code/messageは技術情報disclosureへ維持、SSOTのvisibleNameルール自体は変更なし）。6ラウンド目の全体監査で、同一surfaceのcreate/update/deprecate**成功時**のraw backend carrier message（"Hub relation created."等）がsurface自身のuser-facing語彙「ナビ遷移」と異なるままnormal primary表示されていた残件を発見・解消（action契機ベースのfriendly文へ置換、raw messageは技術情報disclosureへ維持。全repo success message翻訳への一般化はせず）。上位 Roadmap `product.admin_topology_authoring` はこのsubBundle単独では昇格しない。

---

## Report scope migration classification (2026-07-07; reorganized 2026-07-09)

削除前 ref `018b80fa23949a67a7b03f1853cc9c3f2e45ce3c` の `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md` と `.agent/reports/ui-projection-surface-gap-audit-2026-07-07.md` を全文確認した分類。report 由来 scope は finding 番号や route 名ではなく owning SSOT / Bundle / 意味要素単位で扱う。プロンプト発行者の scope が狭い可能性があるため、実装 Agent は関連箇所を追加調査し、SSOT / wiring / test-proof surface の不足を blocking として記録してから product 実装へ進む。

- `test-orchestration-review`: **seed conversion 完了後の後段**。旧 `pipeline-continuity-frontend-route-seed-proof` は実装 Bundle ではなく、seed conversion 後に test tier / scenario harness / route-presence-test replacement を点検・見直す proof orchestration review として扱う。route absence 単独や hardcoded route presence test を canonical proof として残さない。
- `frontend-canonical-surface-structure-label-boundary`: **完了済み（PR#610）**。wiring inspector canonical taxonomy統一・internal_api projection接続・canvas workspace sequential framing除去・panel fixed/docked化・normal label / technical disclosure boundary、`/admin/manifests` / hub_navigation の visibleName SSOT projection、`/auth` / `/super_auth` normal error path label boundary、`HubNavigationAdmin` lifecycle error pathのnormal/technical分離、および同surface成功時messageのnormal語彙統一を6ラウンドの監査を経て実装。
- `admin-console-workflow-step-wording-boundary`: **seed conversion 後の後段**。Step wording 修正は seed conversion 実装に混ぜず、conversion 完了後に admin console workflow wording scope として扱う。
- `product-nocode-loop-acceptance`: **acceptance_pending 維持**。Agent が仕様確定や受入完了を代行せず、オーナーが統合 UX / manual acceptance scope を精査する。
- `helper-manual`: **仕様確定後 scope 維持**。helper manual は仕様確定前に実装へ進めず、user-facing helper manual SSOT に従う後段 scope とする。
- `ui-projection-surface-architecture-reinforcement`: **移管済み / 維持**。PR574 reference evidence、`/demo` cleanup、UI Builder inspection、`ProjectionShell` route/package/manifest awareness、`projectionInput` collection preservation、`runtimeInteraction identity / projection-time idempotency identity` future direction はこの bundle の PR574後残 scope として維持する。route seed 化 / label boundary / admin Step wording / broad pipeline proof は無理に混ぜ潰さない。

---

## Bundle `test-orchestration-review`

**Status:** `not_started`
**Primary SSOT:** `docs/design/pipeline-continuity-ssot.yaml`
**Position:** `admin-surface-topology-seed-conversion` 完了後の後段 review。実装 Bundle ではなく、動作証明 / test orchestration 点検 scope。

### 問題点

旧 `pipeline-continuity-frontend-route-seed-proof` は seed conversion と proof orchestration を同じ実装 Bundle に混ぜる危険があった。route absence や old route-presence test を proof とすると、seed CRUD renderability / projection render / backend action wiring の証明が欠落する。

### 目的

seed conversion 完了後に test tier / scenario harness / route-presence-test replacement を点検し、route registry proof、seed CRUD renderability proof、route removal replacement proof、label boundary proof、admin Step wording proof の位置づけを整理する。

### 改善方針

- `admin-surface-topology-seed-conversion` 完了後にのみ開始する。
- proof orchestration review は実装完了判定ではなく、pipeline-continuity SSOT に基づく test tier / scenario harness の点検として扱う。
- old route-presence tests は canonical proof として残さず、seed/render/action wiring proof へ置換する。
- route absence 単独を proof としない。
- 必要に応じて frontend route registry / seed renderability / backend action wiring / admin wording / label boundary の proof surface を再分類する。

### 対応資料

- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/react-schema-topology-seed-translator-ssot.yaml`
- `.agent/tasks/todo.md`

### 対象ファイル名

- `.agent/tasks/todo.md`
- `frontend/tests/adminUxGuard.test.ts`
- `frontend/tests/adminMainFlow.test.ts`
- `frontend/tests/visualLayoutBuilder.test.ts`
- `frontend/tests/uiBuilderPackageWiring.test.ts`
- `frontend/tests/runtimeUiInteractionScenario.test.ts`
- `frontend/tests/adminWiringExecutionLane.test.ts`
- `frontend/tests/uiBuilderWiringProjection.test.ts`
- future route registry / seed renderability / route-presence-test replacement proof files

### 対象関数名

- future canonical route registry proof helpers
- future non-canonical route absence assertions
- future seed CRUD renderability assertions
- future projection render proof helpers
- future backend action wiring proof helpers
- future label boundary assertions
- future admin Step wording assertions

### 受入条件

- seed conversion 実装後の後段 scope として扱われている。
- test tier / scenario harness / route-presence-test replacement の点検 scope として記述されている。
- proof bundle を seed conversion 実装 Bundle として扱わない。
- route absence 単独や hardcoded route presence test を canonical proof として残さない。

---

## Bundle `admin-console-workflow-step-wording-boundary`

**Status:** `not_started`
**Primary SSOT:** `docs/design/admin-console-workflow-ssot.yaml`
**Position:** `admin-surface-topology-seed-conversion` 完了後の後段語彙修正 scope。

### 問題点

admin console workflow Step wording を seed conversion 実装前に混ぜると、route retirement / seed render wiring と wording repair が同時変更になり、`/admin/ui-builder` を `/admin/contents` local Step 4 と誤表記する余地が残る。

### 目的

seed conversion 完了後に admin authoring workflow の Step wording boundary を明示し、local submit pipeline と whole-admin workflow を混同しない。

### 改善方針

- seed conversion 実装には混ぜず、後段 scope として実施する。
- `/admin/contents = local submit pipeline Step 1-3`。
- `/admin/ui-builder = whole-admin Step 4`。
- `/admin/manifests = whole-admin Step 5`。
- `/admin/contents -> /admin/ui-builder -> /admin/manifests` の flow を維持する。

### 対応資料

- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `.agent/tasks/todo.md`

### 対象ファイル名

- `.agent/tasks/todo.md`
- `frontend/routes/admin/contents.tsx`
- `frontend/routes/admin/ui-builder.tsx`
- `frontend/routes/admin/manifests.tsx`
- frontend admin navigation / header / stepper components
- tests that assert admin wording and flow labels

### 対象関数名

- future admin workflow step label builders
- future admin navigation view model functions
- future admin route stepper rendering functions

### 受入条件

- Step wording repair is seed conversion 後段 scope として扱われている。
- `/admin/ui-builder` is whole-admin Step 4 and `/admin/manifests` is whole-admin Step 5.
- `/admin/contents` local submit pipeline remains Step 1-3 and is not extended to own whole-admin Step 4/5 wording.

---

## Bundle `helper-manual`

**Status:** not_started
**Roadmap/status SSOT:** `product.helper_manual_policy`
**Primary SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`
**Design prerequisite status:** schema / seed / admin helper viewer 実装前に `docs/design/user-facing-helper-manual-ssot.yaml` の clone lifecycle reference contract を正本として読むこと。

目的:
仕様確定後 scope として、helper/manual をユーザー向け文章方針だけでなく、人間 / Agent / MCP / External AI / Local LLM / admin UI が共通参照する JSON helper reference artifact として実装可能にする。現行MCPの import-candidate / draft_operation / commit_candidate lane に topology authoring draft を載せる参照点を作り、admin では同じ内容を viewer として表示する。最新 `/admin/contents` Step 1 の `create_new_topology` / `clone_active_as_replacement_draft` / `clone_active_as_new_topology_draft`、`draft_origin`、`clone_mode`、replacement merge authority boundary、SQL Attention candidate boundary、`layoutPatchDraft` と production manifest replacement merge の分離を JSON contract 上で混同しない。

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

### 問題点

統合 UX の手動受入 / hand-debug evidence gap は残っているが、これは product 実装 Bundle ではなくオーナーが仕様を精査する acceptance_pending scope である。Agent が仕様確定や受入完了を代行すると、manual acceptance を実装完了判定へ誤変換する危険がある。

### 目的

オーナーが `product.dynamic_support_nocode_loop` と `product.admin_topology_authoring` の統合 UX を手動受入し、仕様確定済み範囲と未確定範囲を判断できるようにする。

### 改善方針

- Agent は仕様確定や受入完了を代行しない。
- runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes、external port consumer projection、team Markdown dashboard は実装済みとして扱い、未実装扱いに戻さない。
- `scheduler-job-manifest-admin-ui` と `helper-manual` は別 canonical bundle で扱い、この手動受入に混ぜない。

### 対応資料

- `docs/system-roadmap.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `.agent/tasks/todo.md`

### 対象ファイル名

- `.agent/tasks/todo.md`
- `frontend/routes/admin/contents.tsx`
- `frontend/routes/admin/ui-builder.tsx`
- `frontend/routes/admin/manifests.tsx`
- `frontend/routes/admin/team-dashboard.tsx`
- ProjectionShell / SSE refresh / SQL Attention feedback projection files discovered by owner or Agent during acceptance support

### 対象関数名

- future acceptance checklist support helpers discovered by Agent
- future ProjectionShell refresh observation helpers discovered by Agent
- future admin import / clone / replacement merge acceptance helpers discovered by Agent

### 受入条件

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する。
- [ ] `product.admin_topology_authoring` の `/admin/contents` Step 1 3 entry mode → clone draft → edit → backend replacement merge を、統合UX手動受入 / hand-debug evidence として確認する。

### 手動受入 checklist
- [ ] `/admin/contents` で、作成・編集・import・apply の現在位置、draft / preview / validate / apply / saved / failed の関係、apply前後の変更差分と反映先を誤認しない。
- [ ] validation 失敗後、画面を離れずに修正へ戻れ、作業文脈が途切れない。
- [ ] `/admin/ui-builder` で、配置・style・binding の編集中状態と反映済み状態、modal / drawer / preview の関係を混同しない。
- [ ] advanced / internal vocabulary が通常操作の判断を邪魔せず、必要な説明だけが出ている。
- [ ] Admin import の CSV / JSON import → preview → editor merge → validate → apply が一連の体験として見え、apply後の projection 反映先を追える。
- [ ] recommendation / SQL Attention feedback は現在状態ではなく候補・観察結果として見え、採用しない限り route / topology / 画面状態が変わったように見えない。
- [ ] 古い・対象なし・根拠が弱い candidate が、ユーザーに採用を強制する表示に見えない。
- [ ] webhook / hook / external port consumer projection で、route / credential requirement reference、secret非表示、受信・拒否・成功・失敗、承認前・承認後・拒否後、provider未接続/future scope の状態を誤認しない。
- [ ] file export / transfer / email / audit approval の結果 projection が成功・失敗・保留として追え、失敗時に再試行すべきか設定を直すべきか判断できる。
- [ ] `/admin/team-dashboard` / MdViewer で、saved view / rendered Markdown / source / binding / completed_preset_seed summary の関係を誤認せず、Markdown body を runtime SSOT と見なさない。
- [ ] refresh / clone / rebind の可否、seed invalid の explicit error、md_viewer read projection boundary が画面上で自然に読め、mutation authority と混同しない。

---

## Bundle `structural-subtree-conditional-visibility-implementation`

**Status:** `implemented` (本ラウンドで全受入条件を満たした自己申告。`.agent/tools` 出力/本記述自体は completion judgment の根拠にならないため、監査役が全差分を読んだ上で最終確認すること — 確認後、本 Bundle セクション自体の削除可否も監査役判断とする)
**Primary SSOT:** `docs/design/runtime-orchestration-ssot.yaml` `ui_projection_render_reachability_contract.structural_subtree_conditional_visibility_contract`
**Position:** design_change (本 design_change で追加された generic contract) の後段 implementation_change。credential-management manifest 092 の category-collapse blocking finding に対する実装。

### 問題点 (audit 指摘・設計修正済み)

manifest 092 (`auth.external.credential_management.projection`) の `credential_category_filter` は SSOT 上「filter users external_api_credential external_instance_credential」責務を持つが、`LayoutSchemaTensorComposer.Compose()` は schema record を無条件・常設で LayoutNode へ合成するため、非選択カテゴリの CRUD/action/confirm 構造が同一 structural tree へ常設展開されていた。当初の design_change には (1) visibilityBinding の authoring scope 未確定、(2) DOM mount 判定と lifecycle-interaction reachability 判定の不整合、(3) 根拠のない `valueType` field、(4) 未使用の `watchedBy`/`readableFrom` grounding、(5) todo.md の対象ファイル名不整合という5件の監査指摘があり、本ラウンドで SSOT を是正した上で実装した（`authored_record_type_scope` を Category/Section に限定、DOM mount と lifecycle reachability を同一 `resolveNodeVisibility` evaluator に統一、`valueType` 記述を削除し scalar 厳密等価のみに縮小、`watchedBy_readableFrom_grounding` を退行させ `watchedBy_readableFrom_status` で未実装を明記）。

### 目的

`structural_subtree_conditional_visibility_contract` を実際に実装し、manifest 092 で category collapse を成立させる。

### 実装済み内容

- backend: `LayoutNodeRecord`/`SchemaRecordRow`/`LayoutNode` DTO に `VisibilityBindingJson`/`VisibilityBinding` carrier を追加し、`LayoutSchemaTensorComposer.ParseRecords`/`Compose()` の schema-composed 経路で authored_record_type_scope（Category/Section限定）・source shape・matchValue scalar 性を検証した上で verbatim 通過させる（Compose() 自体は state を評価しない — static のまま。`backend/tests/Topolactor.Runtime.Tests/LayoutSchemaStructuralCompositionTests.cs` に9件+2件のfixture-backed proofあり）。
- frontend: 新設 `frontend/runtime/structuralVisibility.ts`（`buildVisibilityGraph`/`resolveNodeVisibility`）を唯一の evaluator とし、`LayoutProjectionTree.tsx`（DOM mount）と `uiEventEffectRunner.ts` の `emitLifecycle`（lifecycle-interaction reachability）の両方が同一関数を呼ぶ形で統一した。componentKind/nodeId/manifest UUID 固有分岐は無い。
- `credential_category_filter` の onChange を既存の `payloadFrom.value = "event.<path>"` grammar 経由の動的値解決（新設 `resolveUiStateUpdateMutationValue`/`valueFrom`）で既存 UI状態更新 (localStateMutation/setState) lane へ配線し、`ui-local:credential_category_filter.selectedCategory` へ書き込む。
- manifest 092 の3カテゴリ (`users`/`external_api_credential`/`instance_settings`) レコードに `visibilityBinding` を採用し、`db/seed_empty.sql` の cd002 (layout_schema_json) / cd004 (tensor) を実際に更新。全128レコードの構造木は変更なし（backend は非選択カテゴリを除外しない — state-blind のまま）。
- **[本ラウンド追加] category selector の実 production 完成**: `credential_category_filter` の実 `<select>` に、`Topolactor.Schema.CredentialManagementCategories.All`（`users`/`external_api_credential`/`external_instance_credential` — バックエンドの検索 dispatch が既に使っている実在 enum）と厳密一致する実 options 3件を `db/seed_empty.sql` cd004 の `propsJson`（既存の node-local override 汎用機構、他の seed preset が既に使っているのと同じ lane）で採用。空 options の select を completion 扱いしていた実装漏れを解消。
- **[本ラウンド修正・重要バグ]** 3番目のカテゴリの `visibilityBinding.matchValue` を、物理カテゴリノードの key (`instance_settings`) から、実 `<select>`/検索 dispatch が実際に使う値 (`external_instance_credential`) へ訂正。前ラウンドは「matchValue は物理ノードkeyと同じはず」という未検証の前提で実装しており、この不一致により実UIで3番目のカテゴリへ絶対に切り替えられない状態だった（自己 `.set()` する旧テストはこの不一致を検出できていなかった）。両者が独立した文字列であることは SSOT (`admin-normal-surface-projection-seed-ssot.yaml` `external_instance_projection_columns`: "credential-management's external_instance_credential category (instance_settings)") で明示的に根拠づけられている — 新規に命名規則を発明したものではない。
- **[本ラウンド追加] tensor-only 経路の fail-close**: `NpgsqlUiTopologyRepository.ValidateLayoutPatchNodes` が `layout_patch_json.nodes[]` に `visibilityBinding` が(整形・不整形問わず)authorされていたら save 時点で明示的に `LAYOUT_PATCH_VISIBILITY_BINDING_NOT_ALLOWED` で fail-close するよう追加（旧実装は object 型なら黙って通過、それ以外は黙って握りつぶす silent omission だった）。`NpgsqlTopologyRepository.ParseNodesFromLayoutPatchJson` の対応する読み取り側 passthrough は削除（tensor-authored node が visibilityBinding を持つケースはもう save 時点で存在し得ない）。records[] 経路とは独立した negative test で証明。
- **[本ラウンド追加] SSOT 修正**: `runtime-orchestration-ssot.yaml` の `record_carrier_tensor_only`/`authored_record_type_scope`/`invalid_binding_fail_close` が「tensor-only ノードも visibilityBinding を持てる」という実装不可能な誤った記述をしていたのを訂正（`NodeLocalData`/`BuildNodeLocalDataByNodeId` の catalog-leaf-only merge は元々 Category/Section へ絶対に適用されないため、この tensor-only 経路は最初から実装され得なかった）。`prohibited`/`test_proof_contract` に該当項目を追加。
- test/proof: `frontend/tests/structuralVisibility.test.ts`（純粋ロジック9件）、`frontend/tests/uiEventEffectRunner.test.ts` 追加分（動的値解決・lifecycle可視性ゲーティング）、`frontend/tests/layoutProjectionTreeVisibilityRender.test.ts`（実 composer 出力 fixture 経由の DOM-connected proof、汎用 catA/catB）、`frontend/tests/credentialManagementCategorySelectorProductionPath.test.ts`（**本ラウンド新規** — 実 manifest 092 fixture・実 happy-dom・実 `<select>` への実 native "change" DOM event 発火による実 production category switch 経路の e2e proof。dispatcher.set() 直書きに頼らない）、backend 側 fixture-backed byte-exact proof（cd002 matchValue 訂正 + cd004 propsJson options の内容を `CredentialManagementCategories.All` と突合する新規テスト2件含む）、`NpgsqlUiTopologyRepositoryLayoutPatchValidationTests.cs` の tensor-only fail-close 負例5件。secret deny / 既存 live-DB dispatch・projection proof は既存テストスイート全体（backend 1760+/1760+、frontend 2138+/2138+、translator suite の 1件の既存無関係failureのみ変わらず）で回帰なしを確認。

### 本ラウンド（source fixture lineage完成 + 日本語UI化）で解消した残課題

前ラウンドで「本 Bundle の scope を超える design_change を要する」として保留した Confirm/Cancel flat-sibling 不整合、および storage budget 超過は、いずれも狭い実装/design 誤りであると判明したため、本ラウンドで実際に解消した（scope を切り詰めず、当初の残課題を最後まで閉じた）。

- **Confirm/Cancel Modal-nesting 修正（本番データの実バグ）**: `credentials_users_*`（create/update/delete/revoke_credential/revoke_sessions の5フロー）と `eic_*`（create/update/delete の3フロー、計8フロー）は、`external_api_credential_*` の正しいパターン（Confirm/Cancelボタンが対象ModalのDOM/react-schema上の子）と異なり、(a) preview buttonにModalを開くrutimeInteractionが一切無い（クリックしても確認モーダルが開かない）、(b) Confirm/Cancelボタンが Modal の子ではなく Section の flat sibling として author されている、(c) Cancel ボタンがそもそも存在しない、(d) Confirm ボタンの authorityMarker が preview 用の `preview_only` のままで Modal-owned Confirm 用の `draft_apply_not_execution_authority` になっていない、(e) revoke_credential/revoke_sessions フローは preview 側 `dryRun=literal:true` / confirm 側 `confirmed=literal:true` すら authorされていない、という**実際に動作していなかった本番バグ**であったと判明。react-schema (DSL) レベルで Modal-nested-children + 新規Cancelボタン + 正しい authorityMarker/dryRun/confirmed を author し、translator で再生成することで解消した。`configure_scheduler_job_credential_or_port_binding_cancel_button` と `instance_settings` カテゴリの `apply`/`approve`/`json_import`/`json_template_download`/`preview`/`validate` の各Actionも、同じ理由で従来tensorに実体が無かった(=クリックしても何も起きなかった)ことが判明し、re-generateにより正しく解消された。
- **storage budget (2712 byte) 誤適用の是正**: `flatten_topology_ui_seed_tree`/`validate_flat_seed_records` が `manifest.topology` / `idx_manifest_topology` GIN index の per-element budget を、実際には `manifest.topology` に一切 adopt されない `layoutAdoptionCandidates` 等の record にまで一律適用していたバグと判明（`adoption_candidate_separation_contract` が UI-entity payload を `manifest.topology` から追い出した際に、この budget check だけ古いスコープのまま取り残されていた）。budget check を実際に `manifest.topology` へ adopt される `manifestRefsCandidate` のみに再スコープし、`external_api_credential_update_confirm_button`(3793 byte) 等の4レコードは元々このbudgetの対象外であったことを明らかにした。2712定数は変更・無効化していない。
- **翻訳サイクル中に発見した2件の独立した翻訳器バグ**: (1) `convert_node_to_seed_record` の Projection 分岐が `categories` のみを見て直下の Section (`credential_search_section` — 3カテゴリ横断の共有検索バーで、単一の親カテゴリを持たない)を validationErrorゼロのまま**無条件silent dropしていた**（`converted_children`には含まれるが、Projectionレコード自身のフィルタで捨てられていた）。`sections`/`sectionKeys` バケットを追加して解消。(2) `validate_admin_runtime_preview_action_pairing` が SSOT (`wiring_lane_contract...allowed_authority_mapping_values_note`) の「read_only action は pairing対象外」という明文規定をコードに実装しておらず、`credential_search_button`（純粋な検索action）を誤って fail-close していた。`authority != "read_only"` の除外条件を追加して解消。
- **credential-management seed UI の日本語化（Owner指示）**: 上記 source fixture 再構築と同じ DSL 経由で、`docs/design/admin-normal-surface-projection-seed-ssot.yaml` `credentials.seed_contract.display_language_boundary` に従い、全 category/section/form/field/action/modal の `label`・Modal `title`/`body`・`credential_result_list` の表示列見出し・`credential_category_filter` の option label を日本語化。node key・recordType・componentKind・manifest key・targetRef・payloadFrom・statePath・`visibilityBinding.matchValue`・`CredentialManagementCategories.All` の値はすべて不変（canonical値のまま）。新規 i18n ランタイム・credential専用翻訳分岐は追加していない。
- **source fixture → translator → 物理 seed lineage 完全再生成**: `.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.input.json`/`.topology-seed.input.json` を実際の128レコード（+今回追加した8件のCancelボタン = 136レコード）に対応する内容へ再構築し、`generate-react-schema`→`generate-topology-seed` を実行して `gateStatus: pass, validationErrors: 0` を達成。translatorの出力（`adoptionCandidates`）から `db/seed_empty.sql` の cd001(component_group_bundle)/cd002(layout_schema_json)/cd003(wiring_schema_json)/cd004(layout_patch_json)/cd005(package layout) を実際に regenerate して採用。`credential_category_filter` の静的3択オプション（translatorにはliteral optionsをauthorする文法が無いため）、`credential_search_section`の`onChange→setState`配線、`instance_settings`カテゴリ4フォームの`dispatchInstanceOperation`/`localStateMutation`配線、`credential_result_list`の`propBindings.columns`(`activeColumnsToTableColumns`動的列)は、translatorが再現できないtensor-only leaf overrideとして温存し、regenerate後に再適用した（削除・欠落なし、旧cd004との nodeId set 差分は新規追加8件のみで一致確認済み）。

### 対応資料

- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/react-schema-topology-seed-translator-ssot.yaml`
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/admin-normal-surface-projection-seed-ssot.yaml`

### 対象ファイル名

- `backend/repository/LayoutSchemaTensorComposer.cs`
- `backend/repository/NpgsqlTopologyRepository.cs`
- `backend/repository/NpgsqlUiTopologyRepository.cs`
- `backend/repository/TopologyRepository.cs`
- `backend/schema/Contracts.cs`
- `backend/runtime/StructureMapResolver.cs`
- `frontend/runtime/structuralVisibility.ts`
- `frontend/runtime/renderEmission.ts`
- `frontend/runtime/uiEventEffectRunner.ts`
- `frontend/runtime/runtimeComponentFactory.ts`
- `frontend/components/LayoutProjectionTree.tsx`
- `frontend/islands/ProjectionShell.tsx`
- `frontend/lib/uiBuilderWiringProjection.ts`
- `frontend/api/dispatch.ts`
- `.agent/scripts/react_schema_topology_seed_translator.py`
- `.agent/scripts/check_react_schema_topology_seed_translator.py`
- `db/seed_empty.sql` (manifest 092 / package `cd001`/`cd005` / layout `00000000-0000-0000-0000-0000000cd002` / wiring `cd003` / tensor `00000000-0000-0000-0000-0000000cd004`)
- `.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.input.json` (本ラウンドで再構築済み)
- `.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.topology-seed.input.json` (本ラウンドで再構築済み)
- `frontend/tests/fixtures/manifest_0092_bare_entry_layout_nodes.json` (本ラウンドで再生成済み)
- `backend/tests/Topolactor.Integration.Tests/CredentialManagementHubRelationUiProjectionLiveDbTests.cs`
- `frontend/tests/layoutSchemaStructuralRender.test.ts`
- `frontend/tests/credentialManagementCategorySelectorProductionPath.test.ts`

### 本ラウンド（tabs presentation source lineage完成 + 残Modal日本語化 + openModal/closeModal systemic bug修正）で解消した残課題

PR #608での `credential_category_filter` の select→tabs.template presentation移行（design_change/implementation_change、docs/design/admin-normal-surface-projection-seed-ssot.yaml `presentation_history`）の後、本Bundleのcanonical source fixture（上記2ファイル）は `control="form_input/select"` のまま更新されておらず、production（`db/seed_empty.sql`）とcanonical sourceが再び乖離していた。本ラウンドで両source fixtureを `disclosure/tabs` へ修正し、`generate-react-schema`/`generate-topology-seed` を実行して `gateStatus: pass, validationErrors: 0` を確認、出力された `control`/`componentKey` が `db/seed_empty.sql` cd002の既存内容と完全一致することを検証した（sourceのみの手直しでproductionを個別に合わせる、逆にproductionのみをtabs化してsourceをselectのまま残す、のいずれでもない）。

同時に、このBundleのcredential-management surfaceに残っていた最後の4件の英語Modal body（`configure_scheduler_job_credential_or_port_binding`, `external_api_credential` create/update/delete）を、`display_language_boundary`に従い日本語化し、source fixture→translator出力→`db/seed_empty.sql`の3層すべてへ反映した。

本ラウンドのproduction-path proof拡張（実クリックでConfirmation Modalを実際にopenする証明）作業中に、manifest 092のcd004 tensorが**credential-management surface全体の12個のconfirm-Modalペア全て**（users create/update/delete/revoke_credential/revoke_sessions、scheduler credential/port binding、external_api_credential create/update/delete、external_instance_credential create/update/delete）について、openModal/closeModalの `runtimeInteractions` をbutton/Modal自身のtensor nodeへself-scoped authoring（`sourceActionKey == nodeId`）していたため、`LayoutSchemaTensorComposer.Compose()`の`interactionsBySourceActionKey["{resolvedParentNodeId}::{key}"]`マージ規約（`credential_search_section`/`credential_category_filter`の正しいtabs配線が既に使っている規約と同一）と一致せず、実compose時に全34エントリが孤児化(orphan)していたという、本Bundleの完了時点で見過ごされていた深刻な既存production bugを発見した。実クリックで検証した結果、Modal open/closeが production で一切機能していなかったことを確認し、各エントリを実際の親（Modalの`toggle`closeModalは所属Sectionへ、Confirm/Cancelボタンの`click`closeModalは所属Modalへ）へ再配置して解消した。既存のtabs presentation/generic structural visibility/tabs event/value lane/activeKey live-value sync/`credential_result_list`のdynamic column bindingは全て回帰なく維持されている。

`credentialManagementCategorySelectorProductionPath.test.ts`に、実dryRun-preview button click→実`/api/dispatch`決着(mocked fetch, 実FIFOキュー)→実deferred openModal local-state mutation→実Modal DOMでの日本語title/body/Confirm/Cancel表示→実Cancel clickでのclose→再open→実Confirm clickでのConfirm自身のdispatchTargetRef/payload（`confirmed="true"`, preview側の`dryRun`copyではない）保持→決着→closeまでを、`dispatcher.set()`や文字列absenceのみに頼らず実DOM操作で証明する新規テストを追加した。

### 対応資料（本ラウンド追加分）

- `.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.input.json`（本ラウンドでcontrol修正・Modal body日本語化）
- `.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.topology-seed.input.json`（同上）
- `.agent/tools/logs/generate.log`（sha256再計算）

### 本ラウンド（confirmation Modal 12ペア全件のproof gap閉鎖 + Confirm-closeModal wiring欠落修正）で解消した残課題

前ラウンドの受入条件「credential-management surfaceの全confirmation Modal(12ペア)について...実DOM操作で証明した」は、実際には`credentialManagementCategorySelectorProductionPath.test.ts`がexternal_api_credential create/update/deleteとscheduler credential/port bindingの4ペアのみを実証しており、users category(create/update/delete/revoke_credential/revoke_sessions の5ペア)とexternal_instance_credential category(create/update/delete の3ペア)の計8ペアは実DOM証明が存在しないまま「12ペア証明済み」と記載されていた、という宣言とevidenceの不一致が監査で発見された。本ラウンドでこの8ペアを実際にproductionのtabs操作(実`credential_category_filter` tab click経由でのusers/external_instance_credential categoryへの実mount)を含む同一チェーンで証明する過程で、以下の追加の既存production bugを発見・修正した。

**発見**: `external_api_credential_*`/`configure_scheduler_job_credential_or_port_binding`の4ペアはConfirmボタン自身に`secondaryDisclosureActionType="closeModal"`（source DSL）/`secondaryDisclosureAction`（react-schema tree）が authorされ、Confirm click成功後にModalが自動closeするが、`credentials_users_*`(5ペア)と`eic_*`(3ペア)の計8ペアのConfirmボタンにはこの属性が一切authorされておらず（cd004のconfirm buttonノード自身の`runtimeInteractions`が空配列`[]`のまま）、Confirm click成功後もModalが開いたまま残る状態だった。`docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`の`dryrun_preview_gated_confirm_modal`が明示する「a Confirm button's own secondaryDisclosureActionType=closeModal」という既存の generic 標準パターン(admin-enumの7 write actionsが全て採用)に対する、この8ペアだけの既存の authoring 抜け漏れであると判明した。

**修正**: source fixture (`credential-management-0092.input.json`/`.topology-seed.input.json`)の該当8 Confirmボタンに`secondaryDisclosureActionType="closeModal"`/`secondaryDisclosureTargetNodeId`/`secondaryDisclosureStatePath="open"`(DSL)、および対応する`secondaryDisclosureAction`(JSON tree)を追加し、`generate-react-schema`/`generate-topology-seed`で`gateStatus: pass, validationErrors: 0`を確認。生成されたtensor-shapeの`runtimeInteractions`エントリ(`sourceActionKey`=各Confirmボタン自身のkey)を、既存のopenModal/closeModal再配置規約(所属Modal自身のtensor nodeへ、Cancelエントリと同じ配列内へマージ)に従って`db/seed_empty.sql`のcd004へ適用。

`credentialManagementCategorySelectorProductionPath.test.ts`の既存Modal production-path proofを4→12シナリオへ拡張し、各シナリオの実行前に実`credential_category_filter` tab clickで対象category(`users`/`external_api_credential`/`external_instance_credential`)を実際にmountしてから、既存4ペアと完全に同一のchain(preview click→dryRun dispatch決着→Modal open→日本語title/body/Confirm/Cancel表示→Cancel close→reopen→Confirm click→Confirm自身のdispatchTargetRef/payload・confirmed="true"保持→決着→close)を12ペア全件に対して実証した。既存4ペアのproof・既存tabs/activeKey/dynamic-column-binding/日本語化evidenceは全て回帰なく維持されている。

### 受入条件

- [x] 初期表示で非選択categoryの操作群が同時常設表示されない(DOM上に存在しない)。
- [x] category変更後に対象categoryの必要なfield/actionが到達可能になる。
- [x] 別categoryの操作群が非表示(unmount)になる。
- [x] category切替後も既存dispatch binding/payloadFromが維持される。
- [x] zero error render / secret deny /既存live-DB dispatch・projection proofが回帰しない。
- [x] credentials以外の再利用consumerで同一generic機構が動くことを証明する。
- [x] production上で実際のcategory selector操作(実 `<select>` への実 native change event)からprojection-local state更新、structural visibility評価、DOM mount/unmountまでが連続して成立することを証明する（`credentialManagementCategorySelectorProductionPath.test.ts`）。
- [x] tensor-only 経路の不正 visibilityBinding が silent omit されず、SSOT準拠でexplicit fail-closeする（records[]経路とは独立したnegative testあり）。
- [x] source fixture (`credential-management-0092.input.json`/`.topology-seed.input.json`) を実際の manifest 092 seed 内容に合わせて再構築し、translator 経由で `db/seed_empty.sql` を regenerate した。Confirm/Cancelボタンの flat-sibling構造 vs. translatorのModal-nested-children前提の不整合、storage budget誤適用の2件を実際に解消。
- [x] credential-management seed UI (label/title/body/column見出し/option label) を日本語化し、machine vocabulary（node key/recordType/componentKind/targetRef/payloadFrom/statePath/matchValue/`CredentialManagementCategories.All`）は不変のまま維持した。旧英語operation文言が reachable surface に残っていないことを production DOM proofで監査した。
- [x] `credential_category_filter`のtabs presentationについて、canonical source fixture (`credential-management-0092.input.json`/`.topology-seed.input.json`) がtranslator経由で`db/seed_empty.sql`と同一のtabs内容を再生成できる（source→translator→physical seedのlineageが一致し、physical seedのみのtabs化・sourceのみの放置のいずれも発生していない）。
- [x] credential-management surfaceの全confirmation Modal(12ペア)について、実tab/preview button clickによるdryRun dispatch決着後、実際にModalがopenし、日本語title/body/Confirm/Cancelが表示され、実Cancel/Confirm clickで正しくclose・dispatchTargetRef/payloadFrom保持まで到達することを実DOM操作で証明した（文字列absenceのみの証明ではない）。

Bundleの全受入条件を満たしたため、Status を `partial` → `implemented` として扱ってよい（次回 audit/レビューで確認されるまでは Roadmap bundle index からの削除・完全クローズはオーナー/監査役判断とする）。
