# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `in_progress` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `helper-manual` | helper reference artifact / admin helper projection | not_started | 1 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 2 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `test-orchestration-review` | Seed conversion後の proof / test orchestration review | not_started | 1 | proof surface carry-over | `docs/design/pipeline-continuity-ssot.yaml` |
| `frontend-canonical-surface-structure-label-boundary` | Seed conversion後の frontend canonical surface label boundary | not_started | 1 | frontend canonical UI structure/wiring surfaces | canonical surface UI structure/wiring SSOTs, `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml` |
| `admin-console-workflow-step-wording-boundary` | Seed conversion後の admin console workflow wording boundary | not_started | 1 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml` |
| `structural-subtree-conditional-visibility-implementation` | credential-management manifest 092 category-collapse実装 (generic visibilityBinding contract の実装/seed適用) | not_started | 1 | credential-management category-collapse audit | `docs/design/runtime-orchestration-ssot.yaml` |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (email / audit_approval) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。UI Builder form preset は docs/design/ui-builder-preset-ecosystem-ssot.yaml / db/physical_search_crud_aggregate_preset_seed.sql の CRUD preset seed の写像/派生であり、新規 UI runtime / 専用 component 実装ではない。

---

## Report scope migration classification (2026-07-07; reorganized 2026-07-09)

削除前 ref `018b80fa23949a67a7b03f1853cc9c3f2e45ce3c` の `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md` と `.agent/reports/ui-projection-surface-gap-audit-2026-07-07.md` を全文確認した分類。report 由来 scope は finding 番号や route 名ではなく owning SSOT / Bundle / 意味要素単位で扱う。プロンプト発行者の scope が狭い可能性があるため、実装 Agent は関連箇所を追加調査し、SSOT / wiring / test-proof surface の不足を blocking として記録してから product 実装へ進む。

- `test-orchestration-review`: **seed conversion 完了後の後段**。旧 `pipeline-continuity-frontend-route-seed-proof` は実装 Bundle ではなく、seed conversion 後に test tier / scenario harness / route-presence-test replacement を点検・見直す proof orchestration review として扱う。route absence 単独や hardcoded route presence test を canonical proof として残さない。
- `frontend-canonical-surface-structure-label-boundary`: **seed conversion 後の後段**。語彙・label boundary 修正は seed conversion 実装に混ぜず、conversion 完了後に canonical surface label / technical disclosure scope として扱う。
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

## Bundle `frontend-canonical-surface-structure-label-boundary`

**Status:** `not_started`
**Primary SSOT:** canonical surface UI structure/wiring SSOTs, including `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
**Position:** `admin-surface-topology-seed-conversion` 完了後の後段語彙修正 scope。

### 問題点

normal view label boundary と technical disclosure boundary を seed conversion 実装前に混ぜると、seed conversion の route retirement / projection render wiring と語彙修正が同時変更になり、証明範囲が不明確になる。

### 目的

seed conversion 完了後に、canonical surfaces ごとの UI structure/wiring と表示 label boundary を owning SSOT へ戻す。

### 改善方針

- seed conversion 実装には混ぜず、後段 scope として実施する。
- normal view label boundary では raw id / UUID / topology / manifest / screen_data_shape / DB / backend / Route / Primary Table / UI Builder Key 等を通常表示の意味にしない。
- technical details は explicit technical disclosure として扱う。
- operator visible labels は raw-first にしない。

### 対応資料

- canonical surface UI structure/wiring SSOTs
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `.agent/tasks/todo.md`

### 対象ファイル名

- `.agent/tasks/todo.md`
- `frontend/routes/index.tsx`
- `frontend/routes/auth.tsx`
- `frontend/routes/super_auth.tsx`
- `frontend/routes/admin/index.tsx`
- `frontend/routes/admin/contents.tsx`
- `frontend/routes/admin/ui-builder.tsx`
- `frontend/routes/admin/manifests.tsx`
- frontend components / islands that render canonical surface labels and technical disclosure

### 対象関数名

- future normal label mapping functions
- future technical disclosure rendering functions
- future operator label mapping functions
- future canonical surface view model builders

### 受入条件

- 語彙修正は seed conversion 実装後の後段 scope として扱われている。
- normal user-facing views do not expose raw ids / UUIDs / internal vocabulary as primary meaning.
- technical details, if needed, are behind explicit technical disclosure and not normal operation labels.

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

**Status:** `not_started`
**Primary SSOT:** `docs/design/runtime-orchestration-ssot.yaml` `ui_projection_render_reachability_contract.structural_subtree_conditional_visibility_contract`
**Position:** design_change (本 design_change で追加された generic contract) の後段 implementation_change。credential-management manifest 092 の category-collapse blocking finding に対する実装。

### 問題点

manifest 092 (`auth.external.credential_management.projection`) の `credential_category_filter` は SSOT 上「filter users external_api_credential external_instance_credential」責務を持つが、`LayoutSchemaTensorComposer.Compose()` は schema record を無条件・常設で LayoutNode へ合成するため、非選択カテゴリの CRUD/action/confirm 構造が同一 structural tree へ常設展開されていた。適用 SSOT にこの category collapse を data-defined に表現する契約が無かったため、この design_change で `structural_subtree_conditional_visibility_contract` を新設した。実装はまだ行われていない。

### 目的

`structural_subtree_conditional_visibility_contract` を実際に実装し、manifest 092 で category collapse を成立させる。

### 改善方針

- backend: `LayoutNodeRecord` に `VisibilityBindingJson` carrier を追加し、`LayoutSchemaTensorComposer` の schema-composed / tensor-only 両経路で `NodeLocalData` と同じパターンで通過させる（Compose() 自体は state を評価しない — static のまま）。
- frontend: `renderEmission.ts` が `visibilityBinding` を `ComponentSpec` へ verbatim で運び、`LayoutProjectionTree.tsx` の `ProjectionTreeNode` が同一の projection-local state store (`RuntimeGuardedStateStore`) を参照して mount/unmount を汎用的に判定する（componentKind/nodeId/manifest UUID 固有分岐を作らない）。
- `credential_category_filter` (select.template) の onChange を既存の UI状態更新 (`setState`/`setActiveKey` 系) lane 経由で `ui-local:<nodeId>.<stateKey>` へ書き込むよう wiring し、manifest 092 の既存 `default_category` metadata をこの state の `initialValue` として正規に宣言し直す。
- source fixture (`.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.*`) と React-like Schema 側で `visibilityBinding` を authoring し、translator (`generate-react-schema` → `generate-topology-seed`) で regenerate する。`db/seed_empty.sql` を直接手修正した独自派生物にしない。
- test/proof: `structural_subtree_conditional_visibility_contract.test_proof_contract` の positive/negative cases を実装し、既存の secret deny / live-DB dispatch proof を回帰させないことを証明する。

### 対応資料

- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/react-schema-topology-seed-translator-ssot.yaml`
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/admin-normal-surface-projection-seed-ssot.yaml`

### 対象ファイル名

- `backend/repository/LayoutSchemaTensorComposer.cs`
- `backend/repository/NpgsqlTopologyRepository.cs`
- `frontend/runtime/renderEmission.ts`
- `frontend/components/LayoutProjectionTree.tsx`
- `db/seed_empty.sql` (manifest 092 / layout `00000000-0000-0000-0000-0000000cd002`)
- `.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.input.json`
- `.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.topology-seed.input.json`

### 受入条件

- [ ] 初期表示で非選択categoryの操作群が同時常設表示されない(DOM上に存在しない)。
- [ ] category変更後に対象categoryの必要なfield/actionが到達可能になる。
- [ ] 別categoryの操作群が非表示(unmount)になる。
- [ ] category切替後も既存dispatch binding/payloadFromが維持される。
- [ ] zero error render / secret deny /既存live-DB dispatch・projection proofが回帰しない。
- [ ] credentials以外の再利用consumerで同一generic機構が動くことを証明する。
