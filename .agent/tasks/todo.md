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
| `ui-builder-schema-composed-override-delta-reachability` | schema-composed override-delta nodeがUI-Builder再オープン時に消失するnode-loss gap | not_started | 1 | 未割当（`frontend-canonical-surface-structure-label-boundary` PR#610 9ラウンド目監査からの新規発見、別scope） | `docs/design/runtime-orchestration-ssot.yaml` / `docs/design/react-schema-topology-seed-translator-ssot.yaml` |
| `hub-relations-user-facing-name` | hubs.hub_relations に relation自身のuser-facing name authorityを追加する implementation_change | not_started | 1 | 未割当（design_change PRでSSOT契約のみ確定、実装は本Bundleの後段） | `docs/design/db-schema.yaml` / `docs/design/admin-console-workflow-ssot.yaml` / `docs/design/admin-normal-surface-projection-seed-ssot.yaml` |
| `hub-relation-target-manifest-canonical-migration` | `related_hub_id`をtarget-resolution authorityから退役させ、直接FKの`target_topology_manifest_id`へ物理移行する implementation_change | not_started | 1 | 未割当（design_change PRでcanonical契約とretirement sentinelのみ確定、物理移行は本Bundleの後段） | `docs/design/db-schema.yaml` / `docs/design/runtime-orchestration-ssot.yaml` / `docs/design/admin-console-workflow-ssot.yaml` / `docs/design/admin-normal-surface-projection-seed-ssot.yaml` |
| `manifest-canonical-human-name-remediation` | `topology_naming_ssot.user_facing_topology_label.display_rule`（`?? topologySystemName`）の全既存consumerを`canonical_human_name_principle`（explicit name only、未命名時はUI-only projection-local counter）へ置換する implementation_change | not_started | 1 | `product.admin_topology_authoring`（`frontend-canonical-surface-structure-label-boundary` PR#610でこのdisplay_ruleをproduction projectionへ配線した後段の再収束） | `docs/design/admin-console-workflow-ssot.yaml` |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (email / audit_approval) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。UI Builder form preset は docs/design/ui-builder-preset-ecosystem-ssot.yaml / db/physical_search_crud_aggregate_preset_seed.sql の CRUD preset seed の写像/派生であり、新規 UI runtime / 専用 component 実装ではない。

注: `frontend-canonical-surface-structure-label-boundary` は PR#610 により完了済み（wiring inspector canonical taxonomy統一・internal_api projection接続・canvas workspace sequential framing除去・panel fixed/docked化・normal label / technical disclosure boundary）。5ラウンドの監査を経て全SSOT scope整合を確認 — 3ラウンド目時点の完了宣言後、全体監査で `topology_naming_ssot.user_facing_topology_label.display_rule`（visibleName = userFacingTopologyLabel ?? topologySystemName）が `/admin/manifests` / hub_navigation production projectionへ届いていないcross-layer gapと、`/auth`・`/super_auth` normal error pathへraw diagnostic messageがprimary表示されるgapが見つかり、4ラウンド目で解消（新規DBカラムは追加せず、既存 `hubs.topology_manifests.topology_jsonb` の screen_data_shape entryを読む形で解決）。5ラウンド目の全体監査で、naming field双方欠落時にSSOT未定義の `?? manifestKey` をnormal primaryへ昇格していた残fallbackと、`HubNavigationAdmin` のlifecycle（create/update/deprecate/reorder）error pathがraw internal vocabulary（related_hub_id/hub_relations/topology manifest等）を含むbackend messageをそのままnormal primary表示していた残件を発見・解消（fail-close friendly placeholder採用、raw manifestKey/error code/messageは技術情報disclosureへ維持、SSOTのvisibleNameルール自体は変更なし）。6ラウンド目の全体監査で、同一surfaceのcreate/update/deprecate**成功時**のraw backend carrier message（"Hub relation created."等）がsurface自身のuser-facing語彙「ナビ遷移」と異なるままnormal primary表示されていた残件を発見・解消（action契機ベースのfriendly文へ置換、raw messageは技術情報disclosureへ維持。全repo success message翻訳への一般化はせず）。7ラウンド目のSSOT-first再監査で2件を発見・解消: (a) `side_effect_cycle_policy` の dependency graph derivation（`interactionWriteTargets`）が `targetNodeId` 型write のみを認識し `localStateMutation` の `targetRef`（`ui-local:<nodeId>.<stateKey>`）型writeを無視していたため、targetRef経由の direct/indirect self-loopがauthoring candidate filter・pre-save policy gate・runtime execution guardの全てで検出されなかった欠落を解消（`parseUiLocalTargetRef` をSSOT正本箇所 `uiBuilderWiringProjection.ts` へ集約し、3箇所の消費者全てに反映）。(b) canonical `WiringGraphPanel`（layout/wiringモード切替の第一級normal surface）のedge target表示が raw dispatcher-shaped targetRef文字列（`external-port:...`/`instance-port:...`/package-wiring manifest UUID）をそのままprimary表示していたのに加え、localStateMutationの `ui-local:` targetRef解決に parse不備があり常に生文字列へfallbackしていたbugを発見・解消（SSOT分類済みcategoryベースのfriendly文をprimaryとし、raw targetRefは技術情報disclosureへ維持）。componentKey||nodeIdフォールバックとUI監視割当の `nodeId.stateKey` 表示は、既存の一貫した規約かつauthor入力語彙であり本Bundleのraw internal vocabulary懸念に該当しないと判断し変更していない。1〜6ラウンドの既存closureは全体テストスイート再実行で回帰なしを確認。8ラウンド目で、7ラウンド目のこの判断（既存実装の一貫性をauthorityとして採用）自体を再監査し、`componentKey`/`nodeId`単独はcanonical schema/catalog identityではあるがuser-facing display label authorityではないと再整理。調査の結果、`UiBuilderAdmin.tsx`（同一canvas workspaceのLayer Tree・部品パレット・undo履歴・アクセシビリティannounce）に既に存在し広く使われていた `friendlyComponentLabel`/`friendlyNodeLabel`（componentKeyの末尾segmentを抽出する既存の正規表示名解決）を発見。これを `uiBuilderWiringProjection.ts` の `wiringNodeDisplayLabel()` としてcanonical化・共有化し、`WiringGraphPanel` のedge sourceLabel・部品パレット・target表示・UI監視割当表示を全てこのauthorityへ統一（componentKey欠如時はfail-close placeholder「（名称未設定の部品）」、raw nodeId/targetRefは技術情報disclosureへ維持）。新規のfixed label mapは追加していない。この変更により `findRuntimeInteractionPolicyErrors` のlabel文字列フォーマットが変わったため、`uiEventEffectRunner.ts`（fail-close guardのpolicyErrors prefix相関フィルタ、error message構築）と `visualLayoutUtils.ts`（`findRuntimeInteractionPatchErrors`）に残っていた同型 `componentKey || nodeId` の独立再構築2箇所が追従しておらず、fail-closeガードが黙って迂回される回帰が発生 — 全体テストスイートで検出し、両箇所を `wiringNodeDisplayLabel()` 参照へ同期し直して解消（再検証済み、回帰ゼロ）。`external_instance_integration` branchの未検証だったDOM proofも追加。9ラウンド目で、8ラウンド目の「既存frontendで広く使われている」という根拠を再度、既存実装非authorityの原則から監査し2点対応: (1) display authority — `runtime-orchestration-ssot.yaml` `authored_label_and_production_props` により schema-composed record の必須 `label` が `LayoutSchemaTensorComposer.Compose` を経て `LayoutNode.Label` としてproduction renderingへ届く実在のSSOT authorityを確認した上で、UI-Builder自身の読込経路（`ui_topology:get_layout_patch_draft` -> `GetLayoutPatchDraftAsync`）が `layout_patch_json`/`layout_draft_tmp_json` を直接読み `LayoutSchemaTensorComposer.Compose`・`layout_schema_json.records[]` を一切経由しないため、WiringNode/DraftNodeへこのLabelが到達不能であることを実装横断で確認・実証。`wiringNodeDisplayLabel`/`friendlyComponentLabel` はこのSSOT authorityとは無関係な、UI-Builder canvas自身のローカルな運用上fallback（他に到達可能な表示情報が無いために使われている）であるとdoc commentで訂正（8ラウンド目の「同一authority」という表現は誤解を招くため訂正）。調査の副産物として、schema-composed override-delta nodeの永続化済み `layout_patch_json` エントリがcomponentKey欠如を正当に許容する一方、frontend側 `readPatchNode`（`visualLayoutUtils.ts`）が非structural_htmlノードでcomponentKey欠如を検知すると当該ノードをnodes[]から完全除外（silent drop）する、本Bundle（表示ラベル境界）より重大な別種のnode-loss gapを新規発見 — UI-Builder再オープン時にノードが無名表示ではなく完全消失する不具合であり、修正にはbackend/frontend両方のDTO拡張を伴うOwner design_change判断が必要なため本Bundle scopeでは実装せず、doc comment + 専用test（`visualLayoutBuilder.test.ts` KNOWN GAP）で報告のみに留め、新規backlog `ui-builder-schema-composed-override-delta-reachability` として索引に追加した。(2) runtime identity coupling — `uiEventEffectRunner.ts` の `emitLifecycle` が `findRuntimeInteractionPolicyErrors` の人間可読文字列を `${label} #${idx+1}:` prefixで再検索してinteraction単位のfail-close可否を相関していた設計自体（8ラウンド目はlabel format同期のみで根本設計は温存）を、`admin-uibuilder-ui-structure-wiring-ssot.yaml` `projection_authority_runtime_interaction_identity`（runtimeInteractionId、nodeId+interactionIndexフォールバック）と同じ構造的identityで判定する方式へ再設計: `findRuntimeInteractionPolicyViolations()`（`{nodeId, interactionIndex, runtimeInteractionId, message}` 構造化配列、`findRuntimeInteractionPolicyErrors` の既存string[]はこれをラップするのみで shape 不変）を新設し、`uiEventEffectRunner.ts` の相関を nodeId+interactionIndex の構造一致へ置換。同一componentKey（同一表示label）を持つ2ノードが同一interactionIndexで異なる正当性を持つ場合に旧実装が正当な側の実行を誤ってblockする実バグを再現・確認した上で修正し、回帰proof（`uiEventEffectRunner.test.ts`・`uiBuilderWiringProjection.test.ts` 各1件）を追加。1〜8ラウンドの既存closureは全体テストスイート再実行（2102 passed / 83 failed、baseline一致）で回帰なしを確認。上位 Roadmap `product.admin_topology_authoring` はこのsubBundle単独では昇格しない。

---

## Report scope migration classification (2026-07-07; reorganized 2026-07-09)

削除前 ref `018b80fa23949a67a7b03f1853cc9c3f2e45ce3c` の `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md` と `.agent/reports/ui-projection-surface-gap-audit-2026-07-07.md` を全文確認した分類。report 由来 scope は finding 番号や route 名ではなく owning SSOT / Bundle / 意味要素単位で扱う。プロンプト発行者の scope が狭い可能性があるため、実装 Agent は関連箇所を追加調査し、SSOT / wiring / test-proof surface の不足を blocking として記録してから product 実装へ進む。

- `test-orchestration-review`: **seed conversion 完了後の後段**。旧 `pipeline-continuity-frontend-route-seed-proof` は実装 Bundle ではなく、seed conversion 後に test tier / scenario harness / route-presence-test replacement を点検・見直す proof orchestration review として扱う。route absence 単独や hardcoded route presence test を canonical proof として残さない。
- `frontend-canonical-surface-structure-label-boundary`: **完了済み（PR#610）**。wiring inspector canonical taxonomy統一・internal_api projection接続・canvas workspace sequential framing除去・panel fixed/docked化・normal label / technical disclosure boundary、`/admin/manifests` / hub_navigation の visibleName SSOT projection、`/auth` / `/super_auth` normal error path label boundary、`HubNavigationAdmin` lifecycle error/成功messageのnormal語彙統一、`side_effect_cycle_policy` の targetRef dependency-graph gap解消、`WiringGraphPanel` edge target表示のnormal/technical分離、`WiringGraphPanel` node/edge/watch-binding表示の統一（`wiringNodeDisplayLabel`；componentKeyから派生するUI-Builder canvas自身のローカルな運用上fallbackであり、SSOT authored-label authorityではない — `runtime-orchestration-ssot.yaml` `authored_label_and_production_props` が定義する schema-composed record の `LayoutNode.Label` はproduction rendering経路にのみ存在し、UI-Builder自身の読込経路（`ui_topology:get_layout_patch_draft`）はこれへ到達しないため、9ラウンド目で当該authority判断を訂正済み）、および `uiEventEffectRunner.ts` fail-close guardの相関をhuman-facing label文字列からnodeId+interactionIndexの構造的identityへ分離した修正（`findRuntimeInteractionPolicyViolations()`）を9ラウンドの監査を経て実装。schema-composed override-delta nodeのnode-loss gapは本Bundle scope外の別種issueとして新規backlog `ui-builder-schema-composed-override-delta-reachability` へ切り出し済み。
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

---

## Bundle `hub-relations-user-facing-name`

**Status:** `not_started`
**Primary SSOT:** `docs/design/db-schema.yaml` `db_schema.tables.hub_relations.key_columns`（name欄）/ `docs/design/admin-console-workflow-ssot.yaml` `admin_hub_relation_navigation_contract.authoring.relation_name_authoring` / `docs/design/admin-normal-surface-projection-seed-ssot.yaml` `hub_relation_navigation_binding.relation_display_label_contract`
**Position:** design_change 完了（本PR、SSOT契約確定のみ）の後段 implementation_change。SQL DDL / backend / frontend / seed / test実装はすべて未着手。

### 問題点

`hubs.hub_relations`（`hubs.hub -> hubs.topology_manifests -> hubs.hub_relations` 親子階層のnavigation sequence行）に、relation行自身のuser-facing name authorityが存在しなかった。productionの表示ラベル（`HubNavigationSequenceItem.relatedHubLabel`）は `topology.relation_registry.name`（related hubの抽象identity名）または `related_hub_id` UUIDへの `COALESCE` fallbackのみで構成されており、relation edge自身に固有の名前を付けられなかった。

### 目的

relation行自身に任意編集可能な`name`（nullable）を持たせ、未指定時のみ`sequence_position`由来のdefault（"Hub 1"/"Hub 2"/"Hub 3"相当、非永続・reorder追従）を表示するeffective label契約を実装する。

### 改善方針

`hubs.hub_relations`へnullableな`name`列を追加し、既存の`COALESCE(rr.name, hr.related_hub_id::text)`によるfallback表示を、明示name優先・未指定時は`sequence_position`実値による`"Hub {sequence_position}"`という単一のeffective label優先順位へ置換する。以下の各ラウンドで、この方針が内包していた内部矛盾（fallback禁止と例示COALESCE式の不整合、uniquenessの先送り、暗黙rank概念の導入）を同一PR内で収束させた。

### 本ラウンド（design_change 第1回）で確定したSSOT契約

- `docs/design/db-schema.yaml`: `hub_relations.key_columns`に`name`（text, nullable, role: user_facing_relation_name）を追加し、`user_facing_name_contract`でsemantic role・非identity境界を明示。`meaning_collision_guardrails`に`hubs_hub_relations_name_vs_relation_registry_name`を追加し、`relation_registry.name`との衝突境界を明示。`manifest_hub_chain.shape`ミラーにも`name`を反映。
- `docs/design/admin-console-workflow-ssot.yaml`: `admin_hub_relation_navigation_contract.authoring`に`relation_name_authoring`を追加し、`hub_navigation:create`/`update`への追加optional fieldとして位置づけ、新規route/新規relation editorを設計しないことを明示。
- `docs/design/admin-normal-surface-projection-seed-ssot.yaml`: `hub_relation_navigation_binding.relation_display_label_contract`を新設。

### 本ラウンド（design_change 第2回・矛盾解消パス、同一PR）で修正したSSOT契約

第1回のcontractは (a) `prohibited_fallback_authorities`が`relation_registry.name`/`related_hub_id`へのfallbackを禁止する一方、`next_implementation_change_bundle_acceptance`自身の例示COALESCE式がそれらへfallbackする自己矛盾、(b) uniquenessを「将来のimplementation_change判断」へ先送りしたまま未確定、(c) `sequence_position`とは別に「active sibling内のordinal position」という新しいrank概念を暗黙導入、(d) `db-schema.yaml`内の`next_implementation_change_bundle_acceptance below`という実体のないlocal参照、という4件の内部矛盾を含んでいたため、同一PR内で以下へ収束した:

- **唯一のeffective label authority**: `admin-normal-surface-projection-seed-ssot.yaml` `hub_relation_navigation_binding.relation_display_label_contract`を単一の正本とし、`db-schema.yaml`側は詳細を再掲せずそこを参照するのみに変更（二重定義のドリフト再発を防止）。
- **default labelのN**: 新しいordinal/rank計算を廃止し、`sequence_position`列の実値をそのまま`"Hub {sequence_position}"`へ代入する（非連続・非1始まりでも実値をそのまま使う）ことを明示。
- **prohibited fallback**: `relation_registry.name`/`related_hub_id`/target manifest UUID/manifestKeyへのfallbackを、`next_implementation_change_bundle_acceptance`の受入条件文からも完全除去し、既存の`COALESCE(rr.name, hr.related_hub_id::text)`は「拡張」ではなく「置換（廃止）」する契約へ修正。
- **uniqueness**: 「将来判断」から本design_changeでの必須contract (`uniqueness_contract`) へ確定。active (`status='active'`) sibling under同一`topology_manifest_id`のみを境界とし、未指定（NULL/空文字）行とdeprecated行はscope外。
- **未指定nameのcanonical semantics**: NULL と空文字("")を同一の「unspecified」として明示し、explicit name同士の比較は大小文字・trim等の正規化なしの厳密一致とすることを明記（新規normalization policyは発明していない）。
- **relation_registryのsemantic role**: 既存宣言`abstract_space_definition`を維持し、現行実装が表示fallbackとして利用している事実は「implementation fact」であって「SSOT上のhub identity authority宣言」ではないと明記し、role拡張と誤読されないよう`meaning_collision_guardrails`の記述を修正。
- **ローカル参照修正**: `db-schema.yaml`の`next_implementation_change_bundle_acceptance below`という実体のない参照を、正しいcross-file参照（`admin-normal-surface-projection-seed-ssot.yaml` `hub_relation_navigation_binding.relation_display_label_contract`）へ修正。

### 次段（implementation_change）の受入条件

- [ ] SQL: `db/topology_tables.sql`の`hubs.hub_relations`へ`name text NULL`列を追加(destructive DROP CASCADE無し、bootstrap_policy維持)。
- [ ] backend: `hub_navigation:create`/`hub_navigation:update`が任意の`name`を受理・永続化し、`uniqueness_contract`（同一`topology_manifest_id`配下のactive sibling間でexplicit nameの重複を明示エラーで拒否、NULL/空文字は対象外）を実装する。`NpgsqlContentBundleRepository.ListHubRelationsByManifestAsync`/`LoadHubNavigationSequenceAsync`の既存`COALESCE(rr.name, hr.related_hub_id::text)`は**拡張ではなく置換**し、`effective_label_priority`（explicit `hr.name`、無ければ`"Hub " + hr.sequence_position`）のみに従う。`relation_registry`はこのlabel計算で一切参照しない。`selected_link_payload_required`(identity)には`name`を追加しない。
- [ ] Admin Manifests UI: `HubNavigationAdmin.tsx`(既存 `/admin/manifests` authoring surface、新規route/editorなし)に`name`入力を追加し、backendの重複拒否を明示バリデーションエラーとして表示し(silent overwrite/silent renameは禁止)、有効ラベル(explicit name or `"Hub {sequence_position}"`)表示を追加。reorder後、name無し行のdefault表示が新しい`sequence_position`に追従することを確認。
- [ ] runtime projection: `NavigationSequence`emissionの`relatedHubLabel`(またはその後継field)が上記effective label優先順位に従う。`ProjectionShell`/`CardList`/`frontend/runtime/projectionEntry.ts`が単一の解決経路のみを経由する。
- [ ] live-DB/DOM proof: 明示nameを持つrelationはそのnameを表示し、name無しrelationは自身の`sequence_position`実値による`"Hub {sequence_position}"`を表示し、name無し行のreorder後にdefaultが新しいsequence_positionへ追従し、name付き行のラベルが自身/兄弟行のreorderを経ても不変であり、同一`topology_manifest_id`配下で同名のactive sibling作成/更新が明示エラーで拒否される一方、異なる`topology_manifest_id`配下やdeprecated行では同名が許容されることを実DOM/live-DB経由で証明する。

### 対象ファイル名

- `db/topology_tables.sql`（`hubs.hub_relations`テーブル定義）
- `backend/repository/NpgsqlContentBundleRepository.cs`
- `frontend/islands/HubNavigationAdmin.tsx`
- `frontend/runtime/projectionEntry.ts`

### 対象関数名

- `NpgsqlContentBundleRepository.ListHubRelationsByManifestAsync`
- `NpgsqlContentBundleRepository.LoadHubNavigationSequenceAsync`（`COALESCE(rr.name, hr.related_hub_id::text)`のeffective label計算箇所）
- `NpgsqlContentBundleRepository.CreateHubRelationAsync`/`UpdateHubRelationAsync`（`uniqueness_contract`のバリデーション追加箇所）

### 対応資料

- `docs/design/db-schema.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/admin-normal-surface-projection-seed-ssot.yaml`

---

## Bundle `hub-relation-target-manifest-canonical-migration`

**Status:** `not_started`
**Primary SSOT:** `docs/design/db-schema.yaml` `db_schema.tables.hub_relations.target_reference_canonical_contract`（正本）/ `docs/design/runtime-orchestration-ssot.yaml` `ui_projection_render_reachability_contract.hub_navigation_resolution` / `docs/design/admin-console-workflow-ssot.yaml` `admin_hub_relation_navigation_contract` / `docs/design/admin-normal-surface-projection-seed-ssot.yaml` `hub_relation_navigation_binding`
**Position:** design_change（本PR、PR #612の同一design_changeの一部として追加）の後段 implementation_change。SQL DDL / backend / frontend / test実装はすべて未着手。**本Bundleは新しいnavigation mechanismを構築するBundleではない。** `docs/framework-core.yaml` `runtime_route_attention_boundary`が既に定義するfixed_route層（`hubs.relation_route_is_fixed_business_route_between_hubs`、`business_mandatory_fixed_path`、resolver_priority最上位）はそのまま維持し、既存の`HubNavigationResolver` → `ManifestDispatcher.EnrichWithHubNavigationAsync` → `Emission.NavigationSequence` → `frontend/runtime/projectionEntry.ts resolveHubNavigationLinks` → `ProjectionShell.tsx`というproduction pipelineを一切置換・複製しない。本Bundleが変更するのは、このpipelineの中で`TargetManifestId`へどのcolumnから値を注ぐかという一点（target identity normalization）のみである。

### 問題点

`hubs.hub_relations`のtarget resolution authorityが`related_hub_id`（target Hub UUID）経由の「そのHubに紐づくactive Manifestがexactly one」という推論であり、Owner設計意図（source Manifest → HubRelation → target Manifestの直接参照）と一致していなかった。全体監査により、Top SSOT（`docs/framework-core.yaml` `phase_attention_axis_mapping`）のcanonical Phase軸（`hub_relation_id -> topology_manifest_id -> hub_id`、常にrelationのSOURCE側から導出）は`related_hub_id`に一切依存しないことが確認され、`related_hub_id`はcanonical Phase Attention semanticsとは無関係な、target-resolutionのためだけの後付けfieldであることが判明した。同じくTop SSOT（`docs/framework-core.yaml` `runtime_route_attention_boundary`）は`hubs.hub_relations`をfixed_route業務必須経路として既に定義しており、SQL Attention / recommendationはこのfixed routeを上書きしない別laneであることも確認済み — 本Bundleはこのfixed routeの内部target identityのみを是正する。

### 目的

`related_hub_id`をcanonical target-resolution authorityから退役させ、直接FKの`target_topology_manifest_id`（Manifest UUID）へ物理移行する。既存fixed-navigation pipeline（`HubNavigationResolver`/`ManifestDispatcher`/`resolveHubNavigationLinks`/`ProjectionShell`）とSQL Attentionのcanonical Phase軸・遠方探索semanticは変更しない。

### 改善方針

`db-schema.yaml` `hub_relations.target_reference_canonical_contract`を正本として、`target_topology_manifest_id`を直接FKのcanonical target fieldとし、`related_hub_id`ベースのexactly-one-active-manifest推論を`first_implementation_change_replacement_scope`が列挙する各consumerで直接FK existence+status checkへ置換する。SEARCH/DISPLAY・SQLAT-EVIDENCE分類の消費者は`full_retirement_tracking_inventory`の後段deferred tierとして追跡は継続するが本Bundleの最初のimplementation_changeの手順・受入条件には含めない。以下の各ラウンドで、この方針を「新しいnavigation mechanismの構築」ではなく「既存fixed-navigation pipelineへのtarget identity正規化」として明確化した。

### 本ラウンド（design_change 第1回）で確定したSSOT契約

- **retirement sentinel**: `HUB_RELATIONS_RELATED_HUB_ID__RETIREMENT_PENDING_SENTINEL__TARGET_TOPOLOGY_MANIFEST_ID`（non-production metadata専用。runtime/DB/UIへ混入させないこと）。この文字列でrepo全体（SSOT/TODO）をgrepすれば、本Bundleが追跡すべき記述箇所を再発見できる。
- `docs/design/db-schema.yaml`: `hub_relations.key_columns`に`target_topology_manifest_id`（uuid, forward end-state NOT NULL, FK to hubs.topology_manifests, canonical）を追加し、`related_hub_id`のroleを`legacy_replacement_pending_no_canonical_authority`へ変更。新設`target_reference_canonical_contract`（`hub_relations`の`key_columns`と同階層）に、canonical target field・canonical resolution rule（direct FK existence+status check、exactly-one推論なし）・`related_hub_id`のretirement contract・legacy_target_resolution（現行実装の正確な記述、削除ではなく「canonical forwardではない」というマーキングのみ）を格納。`minimum_cardinality_completion_invariant`・`manifest_hub_chain.shape`/`navigation_target_resolution`・`meaning_collision_guardrails`・`compatibility_history`・`phase_attention_axis_mapping`へ同様のcanonical-forward-vs-既存実装層を追加。
- `docs/design/runtime-orchestration-ssot.yaml`/`docs/design/admin-console-workflow-ssot.yaml`/`docs/design/admin-normal-surface-projection-seed-ssot.yaml`: 各resolution ruleへ同様のforward pointerを追加。`canonical_default_entry_contract`は本redesignと無関係であることを明示。

### 本ラウンド（design_change 第2回・最終収束パス、同一PR）で確定したSSOT契約

前ラウンドに4件の未収束点が残っていたため、同一design_change内で以下へ収束した:

1. **retirement inventoryの二層化**: `related_hub_id_retirement_contract.known_current_consumers_to_replace`（単一list、SEARCH/DISPLAY・SQLAT-EVIDENCEを「このreplacementの対象外」と記述）は、SEARCH/DISPLAY・SQLAT-EVIDENCE分類のconsumerを最終retirement inventoryから恒久的に除外しているように読めたため、`first_implementation_change_replacement_scope`（次implementation_changeが実際に置換するtarget-resolution consumer）と`full_retirement_tracking_inventory`（target-resolutionか否かを問わず、related_hub_idを読み書きする全consumerの恒久追跡list — `ListContentHubRelationsAsync`/`ListHubRelationsByManifestAsync`の表示専用read、`adminUxTerms.ts`、`NpgsqlSqlAttentionLogsRepository.LoadHubRelationExplorationCandidatesAsync`、`HubAttractorExplorationRuntime.cs`のexpandedHubIds折り込みを含む）の二層へ分離。「対象外」は「後段のimplementation_changeで扱う」という順序上の判断であり、「追跡しない」という意味ではないことを`inventory_scope_note`で明示。
2. **sequence_position / reorder契約の明確化**: `hub_navigation:reorder`（`ReorderHubRelationsAsync`）が実relation rowの`sequence_position`を書き換える永続化mutationであり、その同一persisted値をUI順序・relation-vector test_input_shape・SQL Attention far-searchが共有すること、reorder後の位置変更は表示変化ではなくvector座標変更であること、UI用とSQL Attention用に別のordinal/rank authorityを新設しないこと、`sequence_position = 0`が`canonical_default_entry`等の新semanticを帯びないこと（そのroleは既存の`relation_config.transition="canonical_default_entry"` markerに独立して属する）を、`db-schema.yaml` `hub_relations.key_columns` `sequence_position.reorder_mutation_contract`（正本）と`admin-console-workflow-ssot.yaml` `admin_hub_relation_navigation_contract.authoring.reorder_semantics`（ポインタ）へ明示。
3. **Manifest naming原則のconsumer限定解消**: `topology_naming_ssot.user_facing_topology_label.hub_relation_target_picker_naming_note`（target-picker consumer限定）を、`canonical_human_name_principle`（explicit `userFacingTopologyLabel`のみがcanonical human name、未設定時はprojection-local counterで`Manifest {n}`を生成 — counterはprojection開始時に初期化し未命名itemのみincrement、named Manifestはconsumeしない、永続化せずidentity/sort/dispatch/target-resolution/SQL Attention authorityにしない、という原則を全surface共通のものとして明示）と`canonical_human_name_principle_vs_display_rule_conflict`（既存`display_rule`＝`?? topologySystemName`と本原則が「scope が異なるだけで両立する」のではなく**真に矛盾する**ことを明示し、新設Manifest-target-pickerのみを本原則で構築する一方、既存display_rule consumer全体の解消は別Bundle・別Owner判断として.agent/tasks/todo.mdで追跡する）へ置き換え。
4. **SQL AttentionのRelatedHubId折り込みの位置づけ強化**: `phase_attention_axis_mapping.related_hub_id_derived_hub_axis_note`を、`expandedHubIds`の`RelatedHubId`折り込みが「legacy/additional dependencyであり、canonical z/k軸そのものではない」ことを明示し、`full_retirement_tracking_inventory`との相互参照を追加。旧testがこのadditional axisを期待している場合でもtestをauthorityとして旧semanticを復活させず、証明対象を再判定する指針を明示（`runtime-orchestration-ssot.yaml` `canonical_forward_scope_note`と同じ判断規律を適用）。

### 本ラウンド（design_change 第3回・最終収束パス、同一PR）で確定したSSOT契約

前ラウンドで「Owner判断へ委譲」のまま残っていた3件を、本ラウンドで確定判断へ収束した:

1. **SELF_LOOP guardは移植しない、新guardも作らない**: 旧SELF_LOOP（`source hub_id == related_hub_id`、target-Hub model由来）は`target_topology_manifest_id`へ移植せず、`topology_manifest_id == target_topology_manifest_id`を禁止する新guardも追加しないことを確定。source ManifestとtargetManifestが同一UUIDであるrelationは合法な通常のrelationであることを`target_reference_canonical_contract.canonical_target_resolution_rule`本文へ明示し、`related_hub_id_retirement_contract.self_loop_guard_note`を「Owner判断待ち」から「確定した設計判断とその根拠」へ書き換えた。旧SELF_LOOP実装・test（`AdminRuntimeContentBundleTests.cs`等）はfirst_implementation_change_replacement_scopeのconsumerとして退役対象のまま、「移植先を作る」のではなく「削除して終わり」であることを明示。
2. **RelatedHubId由来SQLAT additional hub signalはretirement対象として確定**: `phase_attention_axis_mapping.related_hub_id_derived_hub_axis_note`を「keep/replace/dropのopen question」から「retirement対象として確定、ただし`target_topology_manifest_id`由来の新しいadditional hub signalへの単純な置き換え（parallel signal化）は禁止」という確定方針へ書き換え。次implementation_changeは`expandedHubIds`等を既存canonical z/k軸（source-side derivation）のみへ収束させる。
3. **Manifest naming: `display_rule`をlegacy降格し、実体のあるremediation Bundleを新設**: `admin-console-workflow-ssot.yaml` `topology_naming_ssot.user_facing_topology_label`に`display_rule_status`を追加し、既存`display_rule`（`?? topologySystemName`）を「`canonical_human_name_principle`と並立するcanonical」から「legacy/replacement_pending」へ明示的に降格。新設Bundle `manifest-canonical-human-name-remediation`（本ファイル下部）で、`ManifestsAdmin.tsx`・UI-Builder・wiring inspectorを含む全既存`display_rule` consumerのremediationを実体のあるBundleとして追跡する（target-pickerだけを直して終わりにしない）。

さらに、`related_hub_id_retirement_contract`へ`retirement_completion_condition`を新設し、sentinel／`related_hub_id`／`RelatedHubId`／旧exactly-one解決／旧SELF_LOOP／旧RelatedHubId SQLAT signal／旧`display_rule`のsemantic equivalentがrepo全体でcanonical authorityまたはlive dependencyとして残存しないことを最終closure条件として明示した（historical/compatibility_history記述は例外）。

### 本ラウンド（design_change 第4回・既存fixed-navigation pipeline reuseの再確認、同一PR）で確定したSSOT契約

前ラウンドまでの議論が「新しいtarget resolution mechanismを作る」という誤読を招きかねない書き方になっていたため、本ラウンドでOwner指示に基づき撤回・再確認した:

1. **既存fixed-navigation pipelineの再利用を明文化**: `target_reference_canonical_contract`へ`existing_fixed_navigation_pipeline_reuse_contract`を新設し、`docs/framework-core.yaml` `runtime_route_attention_boundary`（`hubs.relation_route_is_fixed_business_route_between_hubs`、resolver_priority最上位、SQL Attention/recommendationはfixed routeを上書きしない別lane）をTop SSOT authorityとして引用した上で、実コード読解により確認した事実を明示: `HubNavigationResolver.ResolveAsync`は`LoadHubNavigationSequenceAsync`への薄いdelegateでtarget再導出を一切行わない、`ManifestDispatcher.EnrichWithHubNavigationAsync`はその結果をそのまま`Emission.NavigationSequence`へ代入するのみ、`frontend/runtime/projectionEntry.ts resolveHubNavigationLinks`は`item.targetManifestId`をopaqueな値として扱い独自のtarget解決ロジックを持たない、`ProjectionShell.tsx`は解決済みhrefを描画するのみ。本Bundleが変更するのは`LoadHubNavigationSequenceAsync`（および`CreateHubRelationAsync`/`UpdateHubRelationAsync`の書き込み）内で`TargetManifestId`へ値を注ぐcolumnのみであり、このpipeline全体を置換・複製する新設計ではないことを明示した。
2. **`related_hub_id_retirement_contract.retirement_completion_condition`のBundle結合を是正**: 前ラウンドでこの条件が誤って`manifest-canonical-human-name-remediation`Bundleの完了まで要求していた（`related_hub_id`自身のsemantic residueとは無関係なBundleへの不要な結合）ため、`related_hub_id`固有のsemantic residue（旧exactly-one解決・旧SELF_LOOP・旧RelatedHubId SQLAT signal）のみを対象とするよう明示的に訂正し、Manifest naming remediationは完全に独立したBundle・独立したcompletionとして扱うことを明記した。
3. **legacy row disposition調査（investigation only、実装なし）**: `db/seed_empty.sql`（canonical bootstrap seed）は`hubs.hub_relations`行を1件のみ持ち（manifest 092自己参照のcanonical_default_entry行、`status='active'`）、`db/demo_seed.sql`も2件とも`status='active'`で、いずれもdraft/deprecated/dirtyな複雑ケースを含まない。この結果は次implementation_changeのbackfill設計の出発点として記録するが、任意の本番live-DB上の実データがこれと同じ形であることを保証するものではない — 次implementation_changeは自身のlive-DB調査で再確認すること。

**本ラウンドで実装（SQL DDL / backend / frontend / test）は行っていない。** repo-wide調査の結果、target identity正規化それ自体はfixed-navigation pipelineの再利用により機械的である一方、(a) SQL DDL追加とbackfillの実データdisposition判断は本セッションからlive-DBへ接続しての確認を要し、(b) Admin Manifests target-picker正規化（既存`/admin/manifests` + `HubNavigationAdmin.tsx` authoring surfaceのtarget field切り替え、新規surfaceではない）はDDL/backfill/DTO/runtime/Admin authoring/test-proofをBundle単位で同時に意味整合させる一括integrationが必要であり、(c) SELF_LOOP削除・DTO変更は関連する測定可能な数の既存test（backend unit/live-DB integration/frontend DOM、3フレームワーク横断）の個別判定・更新を要し、これら全てをBundle単位で同時に意味整合させる（NG軸: 小粒patchの積み重ね禁止）には、本ラウンドの範囲を超えるDocker/Postgres bootstrapと反復検証が必要と判断した。設計追加やparallel authorityの発明ではなく実装規模・検証手段の制約が理由であるため、SSOT/TODO側の収束のみを本ラウンドで完了し、実装は次のimplementation_change専用セッションへ引き継ぐ。

### 本ラウンド（design_change 第9回・当初症状「Enumがfixed navigationに出ない」の再帰監査、同一PR）で判明した production connectivity gap（Owner判断待ち、本PR全体をpartial扱いにする理由）

**第10回で3件のfactual driftを訂正済み（訂正後の内容を以下に反映）。** 元のRound 9記述は(a)active `hubs.topology_manifests`をSQL文数で16件と誤カウントし、multi-row `VALUES`（external-port consumer projection `a3`〜`a8`の6行）を1件として数えていた、(b) 5つのHubRelation live-DB proofを全て同型（source-outbound authoring mechanismのみ）と一般化していたが、Credential Management（092）とTeam Dashboard（dd010）にはsynthetic source→subBundle自身のreal target Manifestという別方向のcombined proofが存在した、(c)`subbundle_status.admin-dashboard: subBundle_not_applicable`を「sourceである」根拠として使っていたが、同SSOTはこの値を「この`target_surface_manifest_readiness` blocking idがadmin-dashboardをgateしない」という意味としてのみ定義しており、source authorityの根拠ではない。以下は訂正後の内容。

Owner指示により、本PRの設計正規化（`related_hub_id` → `target_topology_manifest_id`、relation-self `name`等）が完了しても、当初のproduction症状「Enum管理画面（manifest ae200）がfixed navigationに出てこない」がproductionで解消されるまで本PR全体をcompletion扱いにしない、という条件で再帰監査した。結果、以下を確認した（production SQL/backend/frontend/seed/testは今回も変更していない — 判断根拠を参照）。

1. **症状は現在のcanonical seedの実データとして再現する、PR #612の設計変更とは無関係な既存gap（訂正済み数値）**: `db/seed_empty.sql`の`INSERT INTO hubs.topology_manifests`を実row単位で数え直した結果、active row数は**21件**（`092`、`093`、`5c100`＝scheduler-settings、`ad200`＝admin-dashboard、`ae200`/`ae210`/`ae220`/`ae230`/`ae240`/`ae250`/`ae260`/`ae270`/`ae280`＝admin-enum一式9件、`0000a3`/`0000a4`/`0000a5`/`0000a6`/`0000a7`/`0000a8`＝external-port consumer projection 6件（1つのmulti-row `VALUES`にまとめて投入されている）、`dd010`/`dd020`＝team-dashboard）。`hubs.hub_relations`のactive行は全体でちょうど1件（manifest 092の`canonical_default_entry_contract`自己参照マーカー行、`sequence_position=1`、`topology_manifest_id`=`related_hub_id`=092自身のhub `...a1`）のみで、21件中**20件**がseed内でactive `hub_relations`行を1件も持たない（relation-less）。全て`NpgsqlManifestRepository.PromoteAsync`（`docs/design/db-schema.yaml` `hub_relations.minimum_cardinality_completion_invariant`の唯一のenforcement_boundary）を経由せずraw SQL `INSERT ... status='active'`で直接投入されているため、promotion時cardinality gateが一度も適用されていない。`frontend/islands/ProjectionShell.tsx`のnav barは`resolveHubNavigationLinks(emission?.navigationSequence)`が空ならリンクを一切描画しない（固定の「ホーム」リンクのみ）ため、Enumに限らずこれら20manifestは全てproduction上「他画面から辿れない」状態にある。
2. **既存proofのproof-directionをsubBundleごとに再分類（訂正済み）**: `docs/design/admin-normal-surface-projection-seed-ssot.yaml` `design_blocking.target_surface_manifest_readiness.navigation_binding_resolution_criterion`を正本として、5つの`*HubRelationUiProjectionLiveDbTests.cs`を個別に再監査した結果、一括で同型と扱っていたRound 9の一般化は誤りだった。
   - **`criterion_satisfied_target_side_combined_proof`（synthetic source → subBundle自身のreal target Manifest、full resolution chain証明済み）**: Credential Management（`CredentialManagementHubRelationUiProjectionLiveDbTests.DispatchAsync_HubNavigationCreate_AuthorsRelationTargetingManifest092_ResolutionChainReachesScalarEmission`、092を実targetとしてLayoutId/PackageId/`instance_settings_import_form`等の実content resolutionまで検証）、Team Dashboard（`TeamDashboardHubRelationUiProjectionLiveDbTests.DispatchAsync_HubNavigationCreate_AuthorsRelationTargetingAdminManifest_ResolutionChainReachesScalarEmission`、dd010を実targetとして`team_dashboard_admin_save_button`等の実content resolutionまで検証）。
   - **`source_outbound_authoring_mechanism_only`（subBundle自身のmanifestをsourceとして、内容の無いsynthetic targetへoutbound relationをauthor。target側のreachability/resolutionは一切検証していない）**: Admin Enum（`AdminEnumHubRelationUiProjectionLiveDbTests.DispatchAsync_AdminEnumManagementManifest_HubNavigationCreate_RealAuthoringPath_ThenResolutionChainReflectsIt`）、Scheduler Settings（`SchedulerSettingsHubRelationUiProjectionLiveDbTests.DispatchAsync_SchedulerSettingsManifest_HubNavigationCreate_RealAuthoringPath_ThenResolutionChainReflectsIt`）。**Enum自身のtarget-side reachabilityは、Credential Management/Team Dashboardと異なり、mechanicsのレベルでも一度も証明されていない。**
   - **`not_applicable_to_this_gate`（`target_surface_manifest_readiness.subbundle_status`が明示的に`subBundle_not_applicable`）**: Admin Dashboard。ただし別途、raw SQL insert（`hub_navigation:create`ではない）でad200からsynthetic targetへrelationを直接投入し、`hub_relation_link_list`のcard_list propBinding（`emission.navigationSequence` → `navigationLinksToCardItems`）が反映されることだけを検証する`AdminDashboardNavigationUiProjectionLiveDbTests.DispatchAsync_AdminDashboardNavigationManifest_CardListItemsReflectRealAuthoredHubRelation`が存在する（`other_if_evidence_requires`：authoring-mechanism proofでもtarget-side combined proofでもない、純粋なdisplay/propBinding render proof）。
   - Credential Managementにはこれとは別に、source/targetとも完全にsyntheticな汎用authoring-mechanism proof（`DispatchAsync_HubNavigationCreate_RealAuthoringPath_SourceManifestDispatchReflectsRelationInNavigationSequence_AndFailClosesOnZeroActiveTarget`）も存在するが、092自身をsource/targetいずれとしても使っていないため、092固有の証拠ではなく`hub_navigation:create`一般のmechanism proofとして扱う。
   - `navigation_binding_authoring_and_verification: resolved`は5subBundle全てに付与されているが、上記の通り実際に証明している内容はsubBundleごとに異なる。admin-enum自身の記述も「it does not by itself mean the admin-enum subBundle is fully implemented」と明示しているので、SSOT文言自体はこの意味で過大主張していない（誤読の危険はあったが、文言は正確）。
3. **この不一致はPR #612より前から存在する既知のgapである**: `docs/design/runtime-orchestration-ssot.yaml` `ui_projection_render_reachability_contract.production_projection_connectivity_invariant`は2026-08-16のOwner設計決定（PR #602 round 10-13）としてこの完了要件を明文化しているが、round 13の結論は「enforcement boundaryは`NpgsqlManifestRepository.PromoteAsync`（新規manifestの今後のauthoring/promotionに対する gate）」に留まり、既存seedデータへのbackfillは行っていない。`db/seed_empty.sql`のadmin-dashboard/scheduler-settings/admin-enum/credential-management各節のコメントは「deliberately seeds NO hubs.hub_relations row」「owns ZERO hubs.hub_relations rows of its own」と明示し、authoringをpost-deployment管理者操作へ委ねる設計を複数PRにわたり繰り返し明言している。この設計判断自体を本invariant導入後に再照合した記録は見つからなかった。
4. **`admin-dashboard`（`ad200`）は妥当な製品候補ではあるが、canonical sourceとして決定された事実は存在しない（訂正済み）**: `ad200`は`data_authority_ref: hubs.hub_relations`、`projection_responsibility: [display_manifest_scoped_hub_relation_links, use_selected_link_as_projection_change_trigger, ...]`（`admin-normal-surface-projection-seed-ssot.yaml` `surface_axes.admin.surfaces.dashboard`）を持つ唯一の「navigation landing surface」であり、この意味でproduct候補として筋は通る。しかし`target_surface_manifest_readiness.subbundle_status.admin-dashboard: subBundle_not_applicable`は、同SSOTの`subbundle_status.note`が明示する通り「この blocking id が admin-dashboardをgateしない」という意味であり、「sourceであると決定された」という意味ではない（Round 9はこれを誤って後者の根拠として扱っていた）。むしろ`docs/design/admin-console-workflow-ssot.yaml` `admin_hub_relation_navigation_contract.note`は「RELATION SOURCEはadminが`/admin/manifests`で選ぶ任意のexisting topology_manifestであり、`/admin`自身のlanding pageである必要は一切ない」と明示的に述べており、design authorityはsourceをad200に固定していない。同ファイル`connection_state_authority`も「特定のhub_relations行が存在するかはruntime/admin dataであり、SSOTが固定ledgerとして追跡するものではない」と明示している。したがって`ad200`は「plausible product source candidate」以上のものではなく、`ad200`から`ae200`/`5c100`/`dd010`等へ実際にどのsequence_position・`name`でrelationをseedするかは、いずれのSSOT/PRでも一度も決定されていない。db seedへ実relationを追加することは「これまで5つのsubBundleで繰り返し明言されてきた『seedはrelationを持たない、admin操作で後から繋ぐ』という設計方針を覆す」製品判断であり、Owner比較判断が必要と判断し、本ラウンドでも`ad200 -> ae200`等のrelationを決め打ちで追加しなかった（NG軸: source relationの決め打ち禁止、cardinalityを満たすためだけの無意味relation禁止）。
5. **設計衝突A/B/Cの整理（Owner判断が必要、SSOT内部矛盾ではなく既存contract群が同時に成立する状況を想定していない）**:
   - **A**（`db-schema.yaml hub_relations.minimum_cardinality_completion_invariant` / `runtime-orchestration-ssot.yaml production_projection_connectivity_invariant`）: active `hubs.topology_manifests`行は、他のactive行へresolveするactive outbound `hub_relations`行を最低1件持たなければならない。manifest kindによる例外は無く（`applies_to`）、唯一のenforcement機構は`NpgsqlManifestRepository.PromoteAsync`（draft→active遷移時のgate）。
   - **B**（`admin-console-workflow-ssot.yaml connection_state_authority` / `admin_hub_relation_navigation_contract.note` / 5つのsubBundle seedコメント）: 特定のhub relation行が存在するかどうかはruntime/admin data（`/admin/manifests`経由でobserveする対象）であり、SSOTが固定ledgerとして追跡するものではない。relation sourceはadminが自由に選ぶ既存manifestであり、canonical bootstrap seedにstatic relationを置かず、post-deployment authoringへ委ねる、という設計判断が複数subBundleで繰り返し明言されている。
   - **C**（`db/seed_empty.sql`の実データ）: 21件のactive `hubs.topology_manifests`行のうち20件がraw SQL `INSERT ... status='active'`で直接投入されており、`NpgsqlManifestRepository.PromoteAsync`のenforcementを一度も経由していない。
   - A自身の`applies_to`はmanifest kindやbootstrap起源による例外を規定しておらず、`draft_transient_state_carve_out`もdraftのみを対象とし、bootstrap seedで直接activeにされた行を対象外にしていない。一方Bは「relation connection stateはruntime dataである」と明言しており、seedに静的relationを置くこと自体を暗に避ける設計思想を反映している。CはAの唯一のenforcement機構を経由しない形で20件のactive行を生成しており、AとB/Cの間には、bootstrap seed rowをA運用開始後にどう扱うかを明示的に取り決めた記録が見当たらない。これは既存SSOT間の直接的な文言矛盾ではなく、「Aが要求する状態を、Bが指示する運用（relationはruntime dataとして後から足す）とCが生成した初期状態（promotion gateを経由しない直接active投入）の組み合わせの上でどう満たすか」が一度も明示的に設計されていない、という未決の接続点である。
   - 比較可能な解決candidate（採用を決定しない、Owner比較判断用）:
     - (i) bootstrap seedへ実際のfixed-route relation行を追加する（sourceはad200が有力候補だが、それ自体もOwner確認が必要）。
     - (ii) `minimum_cardinality_completion_invariant`のscope/lifecycle semanticsを見直し、bootstrap-seeded active行（あるいはadmin-authored-post-deploymentを前提とするmanifest）を`draft_transient_state_carve_out`と同様の扱いにする。
     - (iii) relation未authorのmanifestをbootstrap時にactiveとしてseedせず、promotion lifecycle（draft→PromoteAsync）へ統合する。
     - 比較軸: 当初症状（Enum非表示）への効果／既存fixed-navigation mechanismとの整合／`connection_state_authority`との整合／`minimum_cardinality_completion_invariant`との整合／bootstrap成立性（初回デプロイ時に他の機構を壊さないか）／Admin authoring UXへの影響／fake relation・hardcode導入の有無／`target_topology_manifest_id` migrationへの依存／後段implementation Bundle範囲。
     - 上記3案はrepo探索から導いた候補であり、決定ではない。他の既決定mechanismが見つかればそちらを優先する（今回の探索では見つからなかった）。
6. **Bundle境界に関する監査finding（Owner報告、Bundle強制分割はしない）**: bootstrap connectivity問題（「active manifestに実際にrelationがseedされているか」）は、`hub-relation-target-manifest-canonical-migration`が扱うtarget-identity column正規化（`related_hub_id` → `target_topology_manifest_id`のどちらのcolumnからTargetManifestIdへ値を注ぐか）とは意味的に独立した問題である——後者はrelationが存在する場合にそのtarget解決がどのcolumnに基づくかを扱い、前者はrelationがそもそも存在するかを扱う。両者は同じseed/proof層に触れるため今回は同一Bundle内のsemantic dependencyとして記録するが、後段のimplementation_change計画時にはBundle境界を分離するかどうかをOwnerが判断すべき、という監査findingとして記録する。プロンプト発行者都合による強制的な分割は行わない。
7. **結論、本PRの扱い**: 本Bundle（`hub-relation-target-manifest-canonical-migration`）およびPR #612全体のcompletion判定は、上記5のOwner判断（3候補のいずれか、または他の方針）と、それに基づくproduction実装・live-DB/DOM proofが揃うまで、`partial`のまま保持する。既存の設計normalization（`hub-relations-user-facing-name`/`hub-relation-target-manifest-canonical-migration`のSSOT契約確定/`manifest-canonical-human-name-remediation`）はそれ自体としては正しく完了しているが、それらの完了だけでPR #612全体を完了扱いにしない。新しいBundleは追加せず、本Bundle内のsemantic dependencyとして記録する（Bundle境界自体については上記6を参照）。

### 次段（implementation_change）が開始する手順

1. **retirement sentinel文字列でrepo全体をgrep**して、`db-schema.yaml` `target_reference_canonical_contract.related_hub_id_retirement_contract.full_retirement_tracking_inventory`に列挙済みの全consumerを確認する。まず`first_implementation_change_replacement_scope`（target-resolution consumer: backend `NpgsqlContentBundleRepository.CreateHubRelationAsync`/`UpdateHubRelationAsync`/`LoadHubNavigationSequenceAsync`、`ContentBundleRepository.HasResolvableActiveHubRelationAsync`、`HubNavigationHubRelationItemDto`/`HubNavigationSequenceItemDto`/`HubNavigationCreateRequestDto`/`HubNavigationUpdateRequestDto`；frontend `adminApi.ts`の`createHubRelation`/`updateHubRelation`、`dispatch.ts`の`HubNavigationSequenceItem`、`HubNavigationAdmin.tsx`の`draftRelatedHubId`；tests `HubRelationUiProjectionResolutionChainProof.cs`と依存する各`*HubRelationUiProjectionLiveDbTests.cs`、`ManifestDraftActivePromotionLifecycleLiveDbTests.cs`、`HubNavigationFallbackLinksTests.cs`、`AdminRuntimeContentBundleTests.cs`のSELF_LOOP assertion、`InMemoryContentBundleRepository.cs`）から着手する。SEARCH/DISPLAY・SQLAT-EVIDENCE分類の消費者（`ListContentHubRelationsAsync`/`ListHubRelationsByManifestAsync`の表示専用read、`adminUxTerms.ts`、`NpgsqlSqlAttentionLogsRepository`/`HubAttractorExplorationRuntime.cs`）は`full_retirement_tracking_inventory`から除外されているのではなく、`db-schema.yaml`が定義する二層のうち後段（deferred tier）として、この最初のimplementation_changeの手順・受入条件には含めず、`full_retirement_tracking_inventory`が定義する後段の別implementation_changeへ明確にdeferする。deferは除外ではなく、追跡自体は継続する。
2. **canonicalに置換した結果、発火するtest failureを影響検出器として使用する**。旧semanticを期待しているtest failureが見つかった場合、旧仕様（`related_hub_id`ベースのexactly-one推論、または`expandedHubIds`のRelatedHubId折り込み）を復活させて回帰を消すのではなく、そのtestが実際にどのcanonical contractを証明しようとしていたかを再判定し、(a) canonicalな表現へ追従させて更新するか、(b) 旧semantic自体が証明対象だった場合はstale proofとして更新・退役させるか、のいずれかを個別に判断する。full_retirement_tracking_inventoryの後段consumer（SEARCH/DISPLAY・SQLAT-EVIDENCE）に着手する回でも同じ判断規律を適用する。
3. `target_topology_manifest_id`のSQL DDL追加とbackfill（既存rowは現行の`related_hub_id`ベース解決結果から一度だけbackfillしてからNOT NULL化する、具体的なDDL/backfill手順は本design_change未確定）。source `topology_manifest_id`とtarget `target_topology_manifest_id`が同一UUIDであるrelationはvalidationで拒否しない（合法）。
4. 旧SELF_LOOPガード（`CreateHubRelationAsync`/`UpdateHubRelationAsync`のHub-identity比較）を削除する。移植先・代替guardは作らない。`canonical_default_entry_contract`の自己参照シード行（manifest 092）は、ガード削除後も特別扱い不要でそのまま到達可能である。
5. Manifest未命名時の`Manifest {n}`表示を、`canonical_human_name_principle`が定義するprojection-local counter（projection開始時に初期化、未命名itemのみincrement、永続化しない）としてAdmin Manifests target-pickerへ実装する。既存`display_rule`（`?? topologySystemName`）consumer全体の解消はBundle `manifest-canonical-human-name-remediation`（本ファイル下部）で別途扱う。
6. `first_implementation_change_replacement_scope`（target-resolution tier、手順1-5）の完了は、それ自体では本Bundle（parent retirement）をcloseしない。`db-schema.yaml` `retirement_completion_condition`が明示する通り、"first_implementation_change_replacement_scope landing does not by itself close this condition" — `full_retirement_tracking_inventory`の後段deferred tier（SEARCH/DISPLAY・SQLAT-EVIDENCE、`expandedHubIds`の`RelatedHubId`折り込み含む）が残っている限り、本Bundleは`partial`/`open`のまま、そのevidence（手順1-5の完了記録）とともにdeferred tierへcarry-overされる。`retirement_completion_condition`が定義するrepo-wide re-search（sentinel／`related_hub_id`／`RelatedHubId`／旧exactly-one target-Hub解決／旧SELF_LOOP／旧RelatedHubId-folded SQLAT/phaseAT additional hub signal — `expandedHubIds`折り込み除去を含む）を実行し、これら全てがcanonical authorityまたはlive dependencyとして残存しないことを確認できて初めて本Bundleをcloseできる。

**後段deferred（本Bundleの最初のimplementation_changeには含まれない、ただし本Bundleのclosureに必須）:** `full_retirement_tracking_inventory`が指摘する`expandedHubIds`（`HubAttractorExplorationRuntime.cs`）の`RelatedHubId`折り込み除去は、`phase_attention_axis_mapping.related_hub_id_derived_hub_axis_note`が既に確定した方針（既存canonical z/k軸、source-side derivation、`topology_manifest_id -> hub_id`のみへ収束させ、`target_topology_manifest_id`由来の新しいadditional hub signalを追加しない）に従って、後段の別implementation_changeで対応する。この後段implementation_changeでのRelatedHubId折り込み除去は任意ではなく必須の受入条件であり、これが完了して初めて手順6の`retirement_completion_condition`re-searchをclean判定でき、本Bundleをcloseできる。SQL Attentionのcanonical Phase軸（x/y/z, i/j/k）・far-search・exploration_budget_gate semanticsはいずれの段でも変更しない。

### 次段（implementation_change）の受入条件

- [ ] SQL: `hubs.hub_relations`へ`target_topology_manifest_id UUID`列を追加（FK to `hubs.topology_manifests`）。既存rowをbackfillしてから`NOT NULL`化する。destructive DROP CASCADE無し。
- [ ] backend: `hub_navigation:create`/`hub_navigation:update`が`target_topology_manifest_id`を受理・永続化する。`LoadHubNavigationSequenceAsync`等のtarget解決を、`related_hub_id`ベースのexactly-one推論から`target_topology_manifest_id`の直接FK existence+status checkへ置換する（`related_hub_id`は物理削除せず、legacy fieldとして残す；読み取り専用の新規consumerは作らない）。旧SELF_LOOPガードを削除し、代替guardを追加しない（手順4参照）。`source == target` Manifest UUIDのrelationがvalidationで拒否されないことを確認する。`hub_navigation:reorder`が`sequence_position`を書き換える既存の永続化mutation semanticsを回帰させないことを確認する。
- [ ] Admin Manifests UI: `HubNavigationAdmin.tsx`のauthoring formが、target Hub選択から既存Manifest一覧を再利用したtarget Manifest直接選択へ変わる。未命名Manifestの表示は`canonical_human_name_principle`のUI-only projection-local counterによる「`Manifest {n}`」に従う（永続化しない、新規index列を追加しない、named Manifestはcounterを消費しない）。
- [ ] runtime projection: `NavigationSequence`emissionが`target_topology_manifest_id`ベースの解決を反映する。`selected_link_payload_required`へ`target_topology_manifest_id`をadditiveに追加する。
- [ ] SQL Attention: `phase_attention_axis_mapping.canonical_ID_space_axes`（w/x/y/z/i/j/k）と遠方探索/exploration_budget_gate semanticsが変更されていないことを回帰確認する。本Bundleの最初のimplementation_changeでは`expandedHubIds`の`RelatedHubId`折り込みには着手しない（後段deferred、下記test判断項目参照）。
- [ ] test判断: `first_implementation_change_replacement_scope`列挙のtestそれぞれについて、canonical契約へ追従させたか、stale proofとして退役させたかを明示し、いずれの場合も理由を記録する。旧仕様を復活させて回帰を消すことは禁止。`full_retirement_tracking_inventory`の残りconsumer（SEARCH/DISPLAY・SQLAT-EVIDENCE、`expandedHubIds`の`RelatedHubId`折り込み含む）は本Bundleで未着手のまま追跡を継続する（除外ではない）ことを明示する。それらに着手する後段の別implementation_changeでは、`expandedHubIds`の`RelatedHubId`折り込みが削除され既存canonical z/k軸のみへ収束していること、`target_topology_manifest_id`由来の新規parallel signalが追加されていないことを受入条件とする。
- [ ] live-DB/DOM proof: `target_topology_manifest_id`による直接解決（zero/multiple-active-manifest推論の失敗モードが構造的に発生しないこと）、`target_manifest_missing_or_not_active`のfail-close、`source == target` Manifest UUIDのrelationが正常に機能すること、既存`canonical_default_entry_contract`（manifest 092自己参照行）がガード削除後も変わらず到達可能であること、reorder後の`sequence_position`永続値がUI表示とSQL Attention双方に反映されることを実DOM/live-DB経由で証明する。
- [ ] retirement completion: `related_hub_id_retirement_contract.retirement_completion_condition`が定義するrepo-wide re-search（sentinel文字列・`related_hub_id`/`RelatedHubId`・旧exactly-one target-Hub解決・旧SELF_LOOP・旧RelatedHubId-folded SQLAT/phaseAT additional hub signal、すなわち`expandedHubIds`の`RelatedHubId`折り込み除去を含む）を実行し、canonical authorityまたはlive dependencyとしての残存がゼロであることを確認する（historical/compatibility_history記述は例外）。本条件は`related_hub_id`自身のsemantic residueのみを対象とし、`manifest-canonical-human-name-remediation`Bundle（独立Bundle、独立completion）の完了を前提条件としない。`first_implementation_change_replacement_scope`（本チェックリストの他項目）の完了はこの項目を自動的には満たさない — `full_retirement_tracking_inventory`の後段deferred tier（`expandedHubIds`折り込み除去を含む）が別のimplementation_changeで完了し、このre-searchがclean判定を返すまで、本Bundleは`partial`のまま残る。
- [ ] 既存fixed-navigation pipeline（`HubNavigationResolver`/`ManifestDispatcher.EnrichWithHubNavigationAsync`/`resolveHubNavigationLinks`/`ProjectionShell.tsx`）が置換・複製されておらず、変更が`TargetManifestId`のsource column切り替えのみに収まっていることを確認する。新しいnavigation model・navigation store・parallel router・recommendation-as-fixed-route代替が追加されていないことを確認する。
- [ ] 本Bundleの完了判定はCI greenのみを根拠にしない。SSOT契約・実装・testの意味的整合を監査役が個別に確認したうえで判定する。

### 対象ファイル名

- `db/topology_tables.sql`（`hubs.hub_relations`テーブル定義）
- `db/seed_empty.sql`（bootstrap connectivity gapの実データ）
- `backend/repository/NpgsqlContentBundleRepository.cs`
- `backend/repository/ContentBundleRepository.cs`
- `backend/repository/NpgsqlManifestRepository.cs`（`PromoteAsync`、唯一のminimum-cardinality enforcement boundary）
- `backend/runtime/HubNavigationResolver.cs`
- `backend/runtime/ManifestDispatcher.cs`
- `backend/tests/Topolactor.Runtime.Tests/InMemoryContentBundleRepository.cs`
- `backend/schema/ContentBundleContracts.cs`（`HubNavigationHubRelationItemDto`/`HubNavigationSequenceItemDto`/`HubNavigationCreateRequestDto`/`HubNavigationUpdateRequestDto`）
- `frontend/api/adminApi.ts`
- `frontend/api/dispatch.ts`
- `frontend/runtime/projectionEntry.ts`（`resolveHubNavigationLinks`）
- `frontend/islands/ProjectionShell.tsx`（nav bar rendering、固定「ホーム」リンク+`hubNavigationLinks`）
- `frontend/islands/HubNavigationAdmin.tsx`
- `backend/tests/Topolactor.Integration.Tests/HubRelationUiProjectionResolutionChainProof.cs`
- `backend/tests/Topolactor.Integration.Tests/AdminEnumHubRelationUiProjectionLiveDbTests.cs`
- `backend/tests/Topolactor.Integration.Tests/CredentialManagementHubRelationUiProjectionLiveDbTests.cs`
- `backend/tests/Topolactor.Integration.Tests/AdminDashboardNavigationUiProjectionLiveDbTests.cs`
- `backend/tests/Topolactor.Integration.Tests/TeamDashboardHubRelationUiProjectionLiveDbTests.cs`
- `backend/tests/Topolactor.Integration.Tests/SchedulerSettingsHubRelationUiProjectionLiveDbTests.cs`
- `backend/tests/Topolactor.Integration.Tests/ManifestDraftActivePromotionLifecycleLiveDbTests.cs`
- `backend/tests/Topolactor.Runtime.Tests/HubNavigationFallbackLinksTests.cs`
- `backend/tests/Topolactor.Runtime.Tests/AdminRuntimeContentBundleTests.cs`

### 対象関数名

- `NpgsqlContentBundleRepository.CreateHubRelationAsync`/`UpdateHubRelationAsync`（`related_hub_id`書き込み・旧SELF_LOOPガード）
- `NpgsqlContentBundleRepository.LoadHubNavigationSequenceAsync`（旧exactly-one-active-manifest推論、`TargetManifestId`算出箇所）
- `ContentBundleRepository.HasResolvableActiveHubRelationAsync`
- `NpgsqlManifestRepository.PromoteAsync`（minimum-cardinality invariantの唯一のenforcement boundary。bootstrap seedはこれを経由しない）
- `HubNavigationResolver.ResolveAsync`（`LoadHubNavigationSequenceAsync`への薄いdelegate）
- `ManifestDispatcher.EnrichWithHubNavigationAsync`（`Emission.NavigationSequence`への代入箇所）
- `resolveHubNavigationLinks`（`frontend/runtime/projectionEntry.ts`）
- `HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector`
- `HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync`
- 各`*HubRelationUiProjectionLiveDbTests`の`DispatchAsync_..._HubNavigationCreate_...`系test（source-outbound authoring proofとtarget-side combined proofの区別は上記round記録を参照）

### 対応資料

- `docs/design/db-schema.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/admin-normal-surface-projection-seed-ssot.yaml`
- `docs/framework-core.yaml`（`phase_attention_axis_mapping`は本Bundleの回帰確認対象、変更対象ではない）

---

## Bundle `manifest-canonical-human-name-remediation`

**Status:** `not_started`
**Primary SSOT:** `docs/design/admin-console-workflow-ssot.yaml` `topology_naming_ssot.user_facing_topology_label`（`canonical_human_name_principle`が正本、`display_rule`は`display_rule_status`によりlegacy/replacement_pending）
**Position:** design_change（PR #612の同一design_change、hub-relation canonical redesignの一部として新設）の後段 implementation_change。`frontend-canonical-surface-structure-label-boundary`（PR#610）がこの`display_rule`を`/admin/manifests` / hub_navigation production projectionへ配線した後段の再収束であり、PR#610の成果を破棄するのではなく、そのconsumer全体を新しいcanonical principleへ置換する。

### 問題点

`topology_naming_ssot.user_facing_topology_label.display_rule`（`visibleName = userFacingTopologyLabel ?? topologySystemName`）は、`userFacingTopologyLabel`未設定時にsystem identifier（`topologySystemName`、kebab-case、route/table/UI-Builder-key導出専用のmachine identity）をuser-facing表示名へ昇格させるfallbackであり、`ManifestsAdmin.tsx`の一般Manifest一覧・UI-Builder・wiring inspectorで既にproduction配線済み（PR#610 4ラウンド目）。一方、hub-relation canonical redesignで確定した`canonical_human_name_principle`（explicit `userFacingTopologyLabel`のみがcanonical human name、未命名時はUI-only projection-local counterによる`Manifest {n}`、system namespaceへのfallback禁止）はこのdisplay_ruleと構造的に矛盾しており、両者を並立させたままでは「どちらがcanonicalか」が読み手ごとに異なる状態になる。

### 目的

`display_rule`の全既存consumerを`canonical_human_name_principle`へ物理的に置換し、Manifest表示名のcanonical authorityを一意にする。

### 改善方針

- `canonical_human_name_principle`をcanonical、`display_rule`をlegacy/replacement_pendingとするSSOT上の優先順位（本design_changeで確定済み）に従って実装する。
- 置換対象は`ManifestsAdmin.tsx`の一般Manifest一覧、UI-Builderのnode/screen表示、wiring inspectorのManifest表示 — `frontend-canonical-surface-structure-label-boundary`（PR#610）がdisplay_ruleを配線した箇所全て。
- 未命名Manifestの表示はprojection-local counter（projection呼び出しごとに初期化、未命名itemのみincrement、named Manifestは消費しない、永続化しない、identity/sort/dispatch/target-resolution/SQL Attention authorityにしない）で`Manifest {n}`を生成する。新規永続index/rank列を追加しない。
- `topologySystemName`・`manifestKey`・table name・UUIDをcanonical human nameへfallbackさせない。
- hub-relation Manifest-target-pickerがこのBundleより先にcanonical_human_name_principleで構築されている場合、それを一次実装として再利用し、重複実装を作らない。
- 本Bundleの完了は独立して判定する。`hub-relation-target-manifest-canonical-migration`の`retirement_completion_condition`は`related_hub_id`固有semantic residueのみを対象とし本Bundle完了を前提条件としない（同ファイル内`related_hub_id_retirement_completion_condition`参照）。本Bundleも同様にそちらの完了を自身の前提条件としない。両Bundleは互いに独立した問題・目的・completionを持つ。

### 対応資料

- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/db-schema.yaml`（`hub_relations.target_reference_canonical_contract.related_hub_id_retirement_contract`は`related_hub_id`固有semantic residueのみを対象とし、本Bundleの完了を前提条件として参照しない）
- `.agent/tasks/todo.md`（`hub-relation-target-manifest-canonical-migration`Bundle。target-picker実装の重複回避のみを参照し、completionの前提条件としない）

### 対象ファイル名

- `frontend/lib/manifestTopologyExtensions.ts`（`hubNavigationManifestVisibleLabel`の定義箇所）
- `frontend/islands/ManifestsAdmin.tsx`
- `frontend/islands/HubNavigationAdmin.tsx`（新設target-pickerとの重複実装回避を確認）
- UI-Builder Manifest/screen表示コンポーネント（`frontend/islands/UiBuilderAdmin.tsx`等、`frontend-canonical-surface-structure-label-boundary`がdisplay_ruleを配線した箇所）
- wiring inspector Manifest表示コンポーネント（`WiringGraphPanel`関連）
- `backend/schema/ContentBundleContracts.cs`・関連DTO（`userFacingTopologyLabel`/`topologySystemName`を運ぶ型）
- `frontend/tests/`配下のManifest表示ラベルに関するtest（PR#610で追加されたもの含む）

### 対象関数名

- `frontend/lib/manifestTopologyExtensions.ts` `hubNavigationManifestVisibleLabel`（現行`visibleName = userFacingTopologyLabel ?? topologySystemName`を実装する関数。全consumerがこの単一関数経由で呼び出しているかを確認し、直接`?? topologySystemName`をinlineしている箇所があれば同様に置換する）
- 新設projection-local counter関数（`Manifest {n}`生成、既存のHubRelation`Hub {sequence_position}`相当の実装パターンを踏襲）

### 受入条件

- [ ] `display_rule`（`?? topologySystemName`）を参照する既存consumer全てが`canonical_human_name_principle`（explicit name only + projection-local counter fallback）へ置換されている。
- [ ] 未命名Manifestの`Manifest {n}`表示が永続化されず、named Manifestがcounterを消費しないことをDOM/live-DB経由で証明する。
- [ ] `topologySystemName`/`manifestKey`/table name/UUIDがcanonical human nameとして表示されるパスが残っていないことをrepo-wide検索で確認する。
- [ ] PR#610が達成した「raw internal vocabularyをnormal primaryへ露出しない」境界を回帰させない。
- [ ] 本Bundleの完了は上記条件のみで自己完結して判定する。`hub-relation-target-manifest-canonical-migration`の`retirement_completion_condition`（`related_hub_id`固有semantic residueのみが対象）を本Bundleのcompletion条件として流用・参照しない。
