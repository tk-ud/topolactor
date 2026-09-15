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
| `hub-relation-target-manifest-canonical-migration` | (1) Admin Enum (`ae200`) をHubRelation fixed-navigationからjump可能にする、(2) `related_hub_id` をfixed-navigation target-resolution authorityから退役させ直接FKの `target_topology_manifest_id` へ移行する、の2目的を扱う implementation_change | not_started | 1 | 未割当（design_change PRでSSOT契約のみ確定、実装は本Bundleの後段） | `docs/design/db-schema.yaml` / `docs/design/runtime-orchestration-ssot.yaml` / `docs/design/admin-console-workflow-ssot.yaml` / `docs/design/admin-normal-surface-projection-seed-ssot.yaml` |
| `hub-relations-deprecate-update-resolvability-guard-gap` | `DeprecateHubRelationAsync`のraw active-count guard、`UpdateHubRelationAsync`のresolvability guard欠如という2経路はimplementation_change着手可能。`NpgsqlManifestRepository.DeprecateAsync`のincoming-reference check欠如（3経路目）はfail-close/警告/許容のOwner判断待ちのdesign_investigation項目（`hub-relation-target-manifest-canonical-migration` Round34監査からの分離、Round36で区分訂正） | not_started | 1 | 未割当（既存本番条件、上記migration Bundleとは独立scope） | `docs/design/db-schema.yaml` / `docs/design/runtime-orchestration-ssot.yaml` |
| `ui-pressure-component-event-log-consumer-gap` | `component_operation_event_log` がSSOT上 `context-route-recommendation.yaml lanes.ui_pressure` のsourceとして宣言されているにもかかわらず、production consumer/aggregation mechanismが存在しないgapのdesign_investigation/design_change | not_started | 1 | 未割当（`hub-relation-target-manifest-canonical-migration` Round 27監査からの新規発見、Recommendation subsystem側の独立scope） | `docs/design/context-route-recommendation.yaml` / `docs/design/runtime-orchestration-ssot.yaml` |

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

## Bundle `hub-relation-target-manifest-canonical-migration`

**Status:** `not_started`
**Primary SSOT:** `docs/design/db-schema.yaml` `db_schema.tables.hub_relations.target_reference_canonical_contract`（正本）/ `docs/design/admin-console-workflow-ssot.yaml` `admin_hub_relation_navigation_contract`（`axis_navigation_membership` 含む）/ `docs/design/runtime-orchestration-ssot.yaml` `ui_projection_render_reachability_contract`（`hub_navigation_resolution` / `hub_relation_sequence_membership_and_navigation_island_contract`）/ `docs/design/admin-normal-surface-projection-seed-ssot.yaml` `hub_relation_navigation_binding`
**Position:** design_change（本PR）の後段 implementation_change。SQL DDL / backend / frontend / test実装はすべて未着手。

### 問題点

1. **Enum jumpできない**: Admin Enum管理画面（manifest `ae200`）が既存HubRelation fixed-navigationから到達できない。Admin axisのnavigation source指定（`dd010`、既存`/admin/team-dashboard`route）は確定済みだが、そこからae200へ実際にnavigateする際のprev/next導出semantic（sequence内でcurrent Manifestが出現する位置の直前直後のみを返す、という決定論的規則）が複数回のRoundを経てもまだ確定していなかった。
2. **target resolutionの二重設計**: `hubs.hub_relations`のfixed-navigation target解決が`related_hub_id`（target Hub UUID）経由の「そのHubに紐づくactive Manifestがexactly one」という推論に依存しており、Owner設計意図（source Manifest → HubRelation → target Manifestの直接参照）と一致していない。
3. **navigation presentationの所在誤り**: HubRelation fixed-navigationはnavbar専用edgeでもUI-Builder/ProjectionShell固有責務でもなく、manifest-scopedなsequence authority（`db/topology_tables.sql`の`hub_relations`定義：「Manifest-scoped hub sequence / UI transition order」「sequence_position is the sequence authority」「Not a global hub-to-hub relation graph」）である。現行`ProjectionShell.tsx`はこのsequence表示を自分のper-dispatch renderingへ埋め込んでおり、target surface遷移後は表示中のmanifest自身のsequenceに切り替わってしまう。

### 目的

1. 既存Admin/Normal projection surface axisと既存fixed-navigation mechanismを使って、Enum (`ae200`) をAdmin axisのnavigation targetとして到達可能にする。
2. `related_hub_id`をcanonical target-resolution authorityから退役させ、直接FKの`target_topology_manifest_id`（Manifest UUID）へ物理移行する。
3. HubRelation fixed-navigationの提示責務をProjectionShellからapp-shell navigation Islandへ収束し、current topologyをそのnavigation Islandへ`Context`経由で共有したうえで、current topologyについて成立し得る単一のordered-sequence adjacency規則（logical sequence = `[S, target(p1), ..., target(pn)]`、Sは自分自身の`topology_manifest_id`で位置0を占める直接的なschema fact、以降が`sequence_position`昇順のRESOLVED TARGETS。current topologyのUUIDが一致する全occurrence——Sそのものとして位置0で一致する場合、いずれかのtarget(pk)として位置kで一致する場合の両方を区別せず——について、prev=位置i-1の要素（i=0なら無し）、next=位置i+1の要素（末尾なら無し）を同一規則で導出する。owner専用/member専用の特殊規則は作らない）のうち該当する各occurrenceについて、immediate prev/nextのみを、失わず提示する（sequenceの他要素を列挙しない。複数のoccurrenceを持つ場合は各occurrenceのprev/nextを独立に失わず扱う。片側が存在しない場合は既存home navigationへ委ねる）。

いずれも既存fixed-navigation pipeline（`HubNavigationResolver`/`ManifestDispatcher`/`resolveHubNavigationLinks`）とSQL Attentionのcanonical Phase軸・遠方探索semanticsは変更しない。新しいaxis列/dimension列/navigation kind/role flag、新relation table、新graph store、Redux/Zustand等の新Storeは作らない。`hubs.hub_relations`をglobal hub-to-hub graphとして再定義しない。

### 改善方針・確定したSSOT契約

**(1) Admin axis navigation membership（目的1、本Roundで訂正）**

`admin-console-workflow-ssot.yaml admin_hub_relation_navigation_contract.axis_navigation_membership`で確定:
- **Admin bridge/sourceの訂正**: 以前のRoundは`ad200`（admin-dashboard, `admin.dashboard.navigation.projection`）をAdmin axisのcanonical navigation sourceとし、Round 29ではさらに`dd010`を「reverse readの具体例」として名目上使うに留めていた。これは誤り（残留設計）であり、本Roundで撤回する。根拠: `ad200`は本番route/entry pointを一切持たず（repo全体grepでtest file以外に参照ゼロ、`production_reachability_precondition`参照）、これをsourceにするには新規`/admin`route（thin_projection_wrapper）を作る必要があった。一方`dd010`（team_dashboard.admin.projection）は`/admin/team-dashboard`という実在の本番routeを既に持つ。Admin bridgeは名目だけでなく`dd010`へ実際に収束させる: **Admin navigation source = `dd010`**（team-dashboard admin）。
- Admin navigation targets: `ae200`（admin-enum、当初症状の対象）、`5c100`（scheduler-settings）、`092`（credential-management）の3件（`dd010`自身は自分のtargetにならないため除外）。
- `ad200`は本Bundleのadmin axis navigation membershipから完全に除外する（sourceでもtargetでもない）。`ad200`自体のmanifest/seed行は無変更（削除・非推奨化はしない）— 単に本Bundleの登録scope外という判断であり、`ad200`を将来別の変更でhub_navigation:createのsource/targetとして使うことを技術的に禁止するものではない。
- Admin navigation non-targets（本Bundleの登録membership scope、訂正済み — 詳細は`admin-console-workflow-ssot.yaml admin_navigation_non_targets`）: `ae210`〜`ae280`（admin-enumの内部write/read operation manifest、canonical Enum UXではae200自身のnode-local dispatch対象）は、本Bundleが登録する`dd010`起点のfixed navigation membershipには含めない。ただし、これはgeneric `hub_navigation:create`によるauthoring targetabilityを技術的に禁止するものではない — `CreateHubRelationAsync`にmanifest種別チェックは無く、`AdminEnumHubRelationUiProjectionLiveDbTests.DispatchAsync_AdminEnumManagementManifest_HubNavigationCreate_ToCreateGroupWriteManifest_ResolutionChainReflectsIt`がae200→ae210への実authoring+resolution chainを現在も証明している。「独立したnavigation targetではない」という過去の表現をglobal prohibitionとして読むことは誤りであり、既訂正済み。
- Normal axis: 既存`surface_axes.normal.normal_hub_relation_navigation_contract`を維持。`dd020`が唯一のreal Normal surfaceで、他にlink先/元は存在しない。fake targetは作らない。
- **`/admin`新設route計画の撤回**: `ad200`をsourceから外したことにより、`ad200`用に`/admin`（現行static body）を新規thin_projection_wrapperへ置換する計画（`runtime-orchestration-ssot.yaml admin_route_retirement_matrix.routes`の`/admin`エントリ）は不要になり撤回する。`/admin`の現行static bodyは無変更のまま、本Bundleのscope外。`dd010`は既存`/admin/team-dashboard`route（既に構築済み、`ProjectionShell`が`team_dashboard.admin.projection`をmanifestKeyでpin）をそのまま使うため、新しいproduction routeは一切作らない。
- 3 target→`dd010`の復路rowは登録しない（6-row bootstrapは行わない）。ae200等のtargetが自分自身のoutbound sequenceを持たない場合でも、Round 29で復元したbidirectional single-hop読み取りにより`dd010`へのreverse-direction candidateが見つかる（下記(3)参照）。`dd010`への復路を作るかどうかは通常のhub_navigation:create authoring（`/admin/manifests`）に委ねられる、bootstrap必須ではない後回し事項である。

**(2) target_topology_manifest_id migration（目的2）**

`db-schema.yaml hub_relations.target_reference_canonical_contract`で確定（本Roundで変更なし）:
- `hubs.hub_relations.target_topology_manifest_id`（uuid, FK to `hubs.topology_manifests`）が canonical forward target reference。Column-levelではnullable。NOT NULLはCHECK制約（`status <> 'active' OR target_topology_manifest_id IS NOT NULL`）として`status='active'`行のみへ限定適用され、column-level NOT NULLにはしない（`status='deprecated'`行はNULLを保持してよい）。
- Target resolution rule: `target_topology_manifest_id`が参照する行が`status='active'`ならresolve、それ以外はfail-close（null）。Hub配下のManifest集合からの推論ではない。
- `related_hub_id`はCURRENT実装のtarget推論fieldとして残る（`related_hub_id`が指すHubに紐づくactive Manifestがexactly oneという既存推論）が、fixed-navigation target-resolution authorityとしては退役。物理削除・停止はこのPRでは行わない。
- `related_hub_id`のtarget-resolution以外の用途（表示/検索/SQL Attention evidence等）の最終処分は本design_changeでは決定しない。
- SELF_LOOP: `topology_manifest_id == target_topology_manifest_id`は合法（manifest 092の`canonical_default_entry_contract`自己参照が既にこれに依存）。旧Hub-identity SELF_LOOPガードは新fieldへ機械移植しない。新しいguardも作らない。
- **legacy ambiguous row migration disposition**（`db-schema.yaml target_reference_canonical_contract.legacy_ambiguous_row_migration_disposition`で確定）: `target_topology_manifest_id`のNOT NULL要件は`status='active'`行に限定したCHECK制約（`status <> 'active' OR target_topology_manifest_id IS NOT NULL`）とし、column-level NOT NULLにはしない。`related_hub_id`がexactly one active target manifestへ解決する行はそのままbackfill。zero/multiple active targetへ解決する行（既に`runtime_fail_close_negative_cases`でunresolvableとして扱われている行）は、fabricationせず`status='deprecated'`へ遷移させる（既存`hub_navigation:deprecate`のstatus値を再利用、新lifecycle状態は作らない）。**observable behaviorの訂正**: `NpgsqlContentBundleRepository.LoadHubNavigationSequenceAsync`/`frontend/runtime/projectionEntry.ts resolveHubNavigationLinks`/`frontend/tests/projectionShellHubNavigationRenderProof.test.ts`で検証済み — zero/multiple-active-target行はcandidate sequenceから除外されておらず、`target_manifest_id=null`の要素として返り、`resolvable:false`にmapされ、`span[data-hub-navigation-unresolvable]`として実際に可視表示されている（非表示ではない）。deprecate化はこの可視placeholderをsequenceから消す観測可能な表示変化を伴う（navigabilityの後退ではない — deprecate化前から一度もresolveしていない行なので、動作するnavigation targetを失うcallerはいない）。**batch判定への訂正**: (4)の「新しいorphanを作らない」判定は、既存`hub_navigation:deprecate`のper-row runtime guard（raw active行数のみを見る、`HUB_RELATION_LAST_ACTIVE_FOR_MANIFEST`はsurviving active行数が0の時のみ発火 — resolvableかどうかは見ない）をmigration内でrow-by-rowに再利用するのではなく、source `topology_manifest_id`ごとに全対象行を書き込み前に一括classification（clean-backfill / ambiguous-deprecate）し、そのsourceにclean-backfill行が最低1件残るかを判定してから書き込む。row-by-row再利用では、同一sourceに複数のambiguous行がある場合に判定順序依存の穴が生じる（先に処理した行の判定が、まだactiveとカウントされている後続のambiguous行の存在によって誤って通過し得る）。batch判定が0件と判断したsourceの行は、従来どおりmigrationからfail closeして管理者の手動対応へ回す。

**(3) Manifest-scoped sequence membership + navigation Island（目的3、本Roundで訂正）**

`runtime-orchestration-ssot.yaml ui_projection_render_reachability_contract.hub_relation_sequence_membership_and_navigation_island_contract`で確定:
- sequence semantics: `db/topology_tables.sql`が正本（`db/legacy_utils/hub_relations_legacy_to_manifest_scoped.sql`がlegacy global graph shapeからこのmanifest-scoped shapeへ移行した経緯も同根拠）。**Round37確定**: logical sequenceは`[S, target(p1), ..., target(pn)]`——source manifest S（`topology_manifest_id`、全行NOT NULLの実列、位置0を占める直接的なschema factであり配列外の追加要素ではない）に、Sが自分自身で持つ`hubs.hub_relations`行を`sequence_position`昇順に並べたRESOLVED TARGETSが続く。Sをsequenceに含めることはOwner fixtureに反しない——Owner fixture `A=[1,5,7,9,15]`/`B=[4,2,3,8,9,6]`は元々owner概念を持たない抽象配列であり、この統一は具体的なhubs.hub_relations domainのみに関わる（下記参照）。`(topology_manifest_id, sequence_position)`のみがUNIQUE制約対象であり、target側（`target_topology_manifest_id`/`related_hub_id`）の一意性は制約されていないため、同一sequence内に同一manifestが複数positionへ重複出現すること、および`topology_manifest_id == target_topology_manifest_id`という合法な自己参照によりSが自分自身のsequence内のtargetとしても同時出現することを排除しない（occurrence単位でprev/nextを扱う、下記参照）。「順列（permutation）」とは呼ばない — target側に重複が許容されるため全単射ではない。1つのmanifestが他のmanifestのsequenceを所有し他manifestの行を多段（multi-hop）に辿ることはない（global graphではない）。
- **fixed-navigation candidateの訂正（Round29〜32のsemantic driftを訂正、Round33で一旦確定、Round35で上位SSOT根拠を確認、Round36で根拠の出典を訂正、Round37でowner case/member caseの二機構分割を撤回し単一規則へ再統一）**: **Round36訂正**: Round35は「owner case/member caseの二機構分割は`docs/design/db-schema.yaml hub_relations.target_reference_canonical_contract.existing_fixed_navigation_pipeline_reuse_contract`が独立に根拠付けている」と結論したが、この契約自体がPR #612のbase commit（`47e5a9ed9fe38de5b6a07f5a7899b7aa94cec64f`）時点のdb-schema.yamlには存在せず（`git show`で確認済み）、本PR内で新設された文書であり、かつその本文自身がruntime-orchestration-ssot.yamlの`bidirectional_read_projection_gap`を逆参照しているため、PR内文書同士の循環根拠だった。base commitまで遡って再監査した結果: **(a) owner case**はPR以前から真に存在する根拠を持つ——`runtime-orchestration-ssot.yaml`自身の`hub_navigation_resolution.rule`（本PRで無変更）が既に「manifest_dispatcherはcurrent topology自身のmanifest-scoped hubs.hub_relations navigation sequenceをcandidateとして付与する」と定めており、本Bundleの寄与はこの既存の「全sequenceをcandidate化」を、Owner確定fixture（prev/next）に応じて「先頭行のみをnextとする」よう絞り込んだことに限られる。**(b) member case**（reverse-direction read）にはPR以前の先例が一切無い——base commit時点の`framework-core.yaml`/`db-schema.yaml`/`runtime-orchestration-ssot.yaml`を横断検索した結果、prev/next/reverse-direction/bidirectional readへの言及はゼロだった。これは本Bundle自身のRoundがOwnerの明示的fixture・Goal 1要件（`ae200`が自身のoutbound行を持たなくても到達可能であること）に応えて導入した新規design workであり、既存SSOT authorityから導出されたものではない——この事実を隠さず明記する。技術的な帰結（owner/member二機構自体、配列=targetsのみ等）はこの訂正によって変わらない。これまでの撤回履歴 — Round29は「current topologyが所属する上位relation/sequence」の復活を、candidate = そのrowの反対側の端点（opposite endpoint）を無条件に列挙する設計として実装した（誤り、撤回済み）。Round32はこれを訂正しつつも、owner自身がsequenceの「index 0」を占めるという配列拡張モデル（`[owner, target(p1), ..., target(pn)]`）を採用し、これがreverse-direction candidateを表現するための専用DTO問題を要求していた。このindex-0-as-owner解釈自体もOwner fixtureが示す配列外要素の追加にあたるため、本Roundで撤回する。**Round37確定semantic（Round33〜36のowner case/member case二機構分割を撤回し単一規則へ統一）**: logical sequenceはsource manifest S自身を位置0に置く`[S, target(p1), ..., target(pn)]`である（`no_return_edge_bootstrap_requirement`参照）。Sは`topology_manifest_id`という全行NOT NULLの実列から直接得られるschema factであり、配列外の追加要素・pseudo row・hidden indexではなく、位置0という通常のsequence要素として扱う。単一規則: current topologyのUUIDが一致する全occurrenceを見つける（Sそのものとして位置0で一致する場合、いずれかのtarget(pk)として位置kで一致する場合の両方を区別なく扱う）。各occurrence（位置i）について、prev=位置i-1の要素（i=0なら無し）、next=位置i+1の要素（iがsequence末尾なら無し）。「owner専用のnext=先頭要素/prev無し」という特殊規則、「member専用の隣接要素のみ」という特殊規則はいずれも作らない — 位置0のprev不存在は一般規則のi=0の場合に過ぎない。target側の一意性は制約されていないため、同一sequence内に同一manifestが複数positionへ出現することも、`topology_manifest_id == target_topology_manifest_id`という合法な自己参照によりSが自分自身のtargetとしても同時出現することも排除しない——occurrence単位でprev/nextを独立に扱う（マージ・重複排除・破棄しない）。マッチングは直接UUID equalityで行い、regex等の比較手段はidentityが直接比較できない場合の実装詳細に留め、SSOT semanticへ昇格させない。Ownerの抽象fixture（A=[1,5,7,9,15]、B=[4,2,3,8,9,6]、current=9）は元々owner概念を持たない、記載された配列そのものをsequenceとして扱う例であり、この統一の影響を受けない: 9はAの中で7と15に（隣接position 3/5）、Bの中で8と6に（隣接position 4/6）それぞれ隣接する。それぞれのoccurrenceが独立にprev/nextの対を提供する。prev/next両方提示要件のもとでは、standing on 9のcandidateは{7, 15, 8, 6}となる。具体的なhubs.hub_relations domainでのS=位置0の worked example は下記「Admin bridge」参照。新しいaxis/dimension/role flag列は不要 — `(topology_manifest_id, sequence_position)`の既存UNIQUE制約と`topology_manifest_id`のNOT NULL制約のみで導出できる。片側が存在しない場合（sequence先頭のprev、末尾のnext）は、既存の`frontend/islands/ProjectionShell.tsx`の無条件「ホーム」linkが missing-side fallback affordanceとして機能する（Home＝S自身への復帰という意味ではない、下記「Admin bridge」のRound35訂正参照）。
- read projection: 既存`ContentBundleRepository.LoadHubNavigationSequenceAsync(topologyManifestId)`（source-scoped）が、current topologyがS（位置0）として所有するsequenceのtarget部分（target(p1)..target(pn)）を過不足なく返す。current topologyがSとして立っている場合（位置0のoccurrence）はprev=無し、next=この結果の最小`sequence_position`行（position 1）。これに加えて、target方向のreverse read（自己参照行を含む、current topologyをtargetとして名指すrowを全て見つけ、各行ごとにその`sequence_position`が示す位置kのoccurrenceとして、同じsequence内の隣接要素——位置k-1（k=1ならS自身、k>=2ならordinary target）・位置k+1（存在すれば）——からcurrent topologyのprev/nextを独立に導出する）を**追加のadditive query**として導入する — 新しいtable/column/parallel authorityではなく、同一`hubs.hub_relations`永続化に対するもう一つの読み方（新設backend read methodは必要、ただし新しいtable/columnは不要）。この読みの目的はopposite endpointの列挙ではなく、他のsequence要素からのprev/next導出であり、隣接要素がS自身（位置0）である場合もordinary target（位置k>=1）である場合も同一規則で扱い、Sだけを候補から除外する特殊扱いはしない。
- **Admin bridge（実体、dd010へ収束、Round37で単一規則へ再統一）**: `dd010`（team_dashboard.admin.projection）は上記(1)で確定した通り、それ自体がAdmin axisのcanonical navigation sourceである。標準例（Round37確定版）: `dd010`のlogical sequenceは、S=`dd010`自身を位置0とし`sequence_position`昇順のtargetsを続けた`[dd010, ae200, 5c100, 092]`。単一規則を全位置へ同一に適用する: `dd010`（位置0）はprev=無し、next=`ae200`（位置1）。`ae200`（位置1）はprev=`dd010`（位置0）、next=`5c100`（位置2）。`5c100`（位置2）はprev=`ae200`、next=`092`（位置3）。`092`（位置3、sequence最後）はprev=`5c100`、next=無し。**`dd010`はae200のprevとして得られる** — これは位置0が「owner専用の特殊枠」ではなく単一規則下の通常の1位置であることの直接の帰結であり、Round30〜32が意図していた「Admin権限内の4 manifestが相互にfixed-navigationで到達可能」という性質を復元する。**Round33〜36は撤回**: 当時は配列をtargetsのみとしdd010を配列外に置く「owner case/member case」二機構分割を採用し、その帰結として`dd010`をae200/5c100/092のいずれからも得られないとしていたが、この撤回の直接の引き金だったRound32のDTO表現不能問題は、TopologyManifestId一fieldに関してはRound33が既に解消していた（`HubNavigationSequenceItemDto.TopologyManifestId`はSQL行の列ではなく入力パラメータをechoする既存実装）。したがってdd010を配列外へ追い出す必要は元々無く、この後退は本Roundで撤回する——**この到達可能性はdomain-level（論理sequenceのordering）の事実であり、transport（DTOの各field）を実際にどう実装するかとは独立**（`dd010`が位置0を占めるのはRound37のcanonical conventionであり、`topology_manifest_id`自体は元から実在するphysical schema factである点も同様に独立——下記「DTO representability」のRound38訂正参照。TopologyManifestId以外のfield、特にHubRelationId/RelatedHubId/SequencePositionの3つについては、Round38監査でOwner判断待ちのtransport-shape未決事項として指摘されており、「field意味の反転が不要」という結論はこの3fieldには及ばない）。**Round35のHome事実訂正はそのまま維持**: Homeの`href`は`/dashboard`であり、これは`dd020`（team_dashboard.normal.projection、Normal-axis）自身の route であって、`dd010`（team_dashboard.admin.projection、`/admin/team-dashboard`）とは無関係の別axis・別manifestである（`admin-console-workflow-ssot.yaml admin_hub_relation_navigation_contract.normal_navigation_membership`参照）。ただしRound37ではこの訂正の意味が変わる: `dd010`への復帰はもはやHome affordanceに頼る必要が無い——`ae200`は自身のfixed-navigation prevとして`dd010`を直接得られるため。Homeは`dd010`（sequence先頭のprev不存在）・`092`（sequence末尾のnext不存在）という真にmissing-sideな場合のみのgeneric fallbackであり、`dd010`という特定宛先への到達手段だと誤認してはならない。新しいbridge manifest・新しいdispatch axis・新しいsequence-position規約などの新architectureは一切作らない — 既存`dd010`と既存`dd010 -> {ae200,5c100,092}`行をそのまま再利用する。
- **Attention/Recommendationとの分離**: 上記のfixed-navigation prev/next導出は、SQL Attention/Recommendation（`context-route-recommendation.yaml`、ログ・遷移集計・count/recency等の集計圧によるcandidate生成）とは別laneであり、混入しない。`docs/framework-core.yaml runtime_route_attention_boundary`の`attention_score_must_not_override_fixed_route`のとおり、fixed_route（本契約）はcanonical_route_tabが所有し、Recommendationはattention_recommendation_tabが所有する別軸のまま維持する。
- frontend→backend transport wiring: navigation Island（frontend）は`HubNavigationResolver`というbackend C#クラスを直接呼べない — 到達には既存HTTP boundaryが要る。`runtime-orchestration-ssot.yaml`の`navigation_island_transport_boundary`で確定: 既存`GET /hub-navigation/relations`（`backend/Program.cs`、現状は`ResolveFallbackNavigationLinksAsync`によるcanonical-default-entry fallback専用）へ、任意の明示`topologyManifestId` query paramを追加する最小extensionとして再利用する。paramが有る場合はforward方向（`HubNavigationResolver.ResolveAsync`、ownerとして立つ場合は配列の先頭要素のみをnextとして採用）とreverse方向（追加のadditive read method、隣接する他の配列要素からprev/nextを導出）の両方を解決し、current topologyのimmediate prev/next対を返す。無い場合は既存fallback挙動を無変更で維持し既存callerを回帰させない。auth境界は既存JWT/session（admin限定ではない）のまま変更しない。`/dispatch`の`hub_navigation:get_hub_relations`（admin限定・`ListHubRelationsByManifestAsync`ベースでinactive行を含むauthoring用read）は不採用 — semanticも認可境界も一致しない。新しいbackend endpoint/dispatcherは追加しない（既存`GET /hub-navigation/relations`の拡張のみ）。
- **DTO representability（Round32の未解決点、Round33がTopologyManifestId一fieldのみ解消、Round37は誤って全field解消済みと結論、Round38 Gate-0監査で3fieldが未解決のOwner判断待ちと訂正）**: Round32はreverse方向のprev候補（click destinationがowner manifest自身になるケース）が既存DTOのいずれのfieldの意味も偽装・反転せずには表現できないと判断し、新規DTO追加のOwner判断待ちとしていた。
  - **TopologyManifestId（解消済み、Round33確認、Round38でも維持）**: `backend/repository/NpgsqlContentBundleRepository.cs`の`LoadHubNavigationSequenceAsync`実装を直接確認した結果、`HubNavigationSequenceItemDto.TopologyManifestId`は元々SQL行の列ではなく、クエリの入力パラメータ（current topology自身）をそのままechoする既存実装になっていることが判明した——つまりこのfieldは元々「この応答が何について答えているか」を表すものであり、「この行の実owner」ではない。位置0（S自身）が隣接occurrenceであってもこのfieldの扱いは変わらない。
  - **RelatedHubLabel/TargetManifestId（解消済み、Round38で再確認）**: 両fieldの既存roleは元々「このcandidateが指す先のlabel/遷移先を解決する」ことであり、「特定rowの生列値をそのままecho」することではない（RelatedHubLabelは既存forward queryでも`hubs.hub`/`topology.relation_registry`とのjoinで解決される値であり生列直読みではない。TargetManifestIdはそもそも解決済み値であり生列ではない）。位置0の隣接occurrenceでS自身のhubs.hub/hubs.topology_manifests識別列から埋めることは、この既存roleと矛盾しないdata-sourcingの選択であり、偽装ではない。
  - **HubRelationId/RelatedHubId/SequencePosition（Round37は誤って解消済みと結論——Round38で再監査しOwner判断待ちの未解決事項へ訂正）**: `frontend/api/dispatch.ts`の`HubNavigationSequenceItem`既存doc comment（本Bundle以前から無変更）は「hubRelationId（the hubs.hub_relations row's own id）」と明記しており、これはTopologyManifestIdのような「応答の主題」規約ではなく、明示的な「この行自身のid」契約である。既存のforward-only実装は常にHubRelationId/RelatedHubId/SequencePositionを同一の物理行から取得しており（他に実装が存在したことがない）、既存test-proof（`HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector`）もRelatedHubId/SequencePosition/TargetManifestIdを一つの行由来のtupleとして検証している。Round37案（「HubRelationIdは隣接する実際の行自身のidを再利用し、RelatedHubIdはS自身の識別子で埋める」）は、位置0のoccurrenceについて、HubRelationIdが指す実在の行（例: dd010→ae200のrow）の実際の`related_hub_id`列はae200のhubであるにもかかわらず、DTOのRelatedHubIdにはdd010のhubを入れるという、**同一rowを装いながら実際には異なる由来のfieldを混在させる構成**になっており、これは他の箇所でこのBundle自身が禁じている「rowを偽装するfabrication」と同種の問題である。さらにSequencePositionについても、`db/demo_seed.sql`の既存seed行・`db/topology_tables.sql`の`sequence_position`列定義（INTEGER NOT NULL）を確認した結果、値0を持つ行は本repoに一件も存在せず、位置0用のSequencePosition値はどの物理行からも読み出せない**fabricated value**になる。
  - **結論（Round38、Owner判断待ちとして記録、本Roundでは実装しない）**: 位置k>=1（ordinary target）のoccurrenceは既存DTOのままで完全に真実性を保って表現可能（無変更、Round33の結論はこの部分について維持）。位置0（S自身）のoccurrenceについてはHubRelationId/RelatedHubId/SequencePositionの3fieldが未解決であり、新規DTO/新規field無しに真実性を保って表現する方法は無いと判断する。既存の再利用可能資産として`frontend/api/adminApi.ts`の`HubNavigationManifestItem`（topologyManifestId/manifestKey/hubId/topologySystemName/userFacingTopologyLabel、hub_relations行に一切紐づかないmanifest identity専用shape）を参考として挙げる——このshapeが既に存在すること自体が、「manifestの自己identityをhub_relations行shapeのDTOへ混在させる」構成がこのrepoに前例が無いことの直接的な証拠である。少なくとも2つの非決定的な候補方向を提示する（Owner判断、Agent独断では決めない）: (a) `HubNavigationSequenceItemDto`/`HubNavigationSequenceItem`のHubRelationIdをnullable化し、位置0のoccurrenceでは明示的にnullとし、判別用の追加field（boolean/enum）を付与する。SequencePositionもこの場合はsynthetic orderingとして文書化する。(b) `HubNavigationManifestItem`と同様の、hub_relations行に紐づかない小さなsource-identity shapeを別途新設し、prev/next消費者がposition-0 occurrenceについてはそちらを受け取る（discriminated unionまたはresponse envelope側の追加field）。いずれもOwner決定・実装前のconsumer影響・test差分比較が必要であり、本design_change roundでは実装しない。**結論の訂正**: 「新規DTO/新規typeは不要」という前Round(37)の結論は撤回する。新規DTO/新規fieldが必要かどうか自体がOwner判断待ちの未決事項である。
  - **Round39: 抽象関数substrate再利用の監査（結論=近道は存在しない、上記Owner判断待ち事項は不変）**: DTO shapeを決める前に、backend-wide`execute_abstract_function`substrate（`docs/framework-policy.yaml abstract_function_substrate_policy`/`docs/design/abstract-function-primitive-registry-ssot.yaml backend_abstract_function_runtime_substrate`）へ接続することで、position-0のsource Manifest identityとordinary HubRelation rowを単一logical navigation occurrenceへ正規化できないか監査した。`backend/runtime/AbstractFunctionRuntime.cs`の実際の登録primitive一覧・`AbstractFunctionExecutionContext.ResolveBinding`の実装を直接確認した結果: `db_query`/`db_mutation`/`manifest_authorized_read`/`manifest_authorized_write`は登録adapterが一切無く（SSOT語彙のみ、接続先が存在しない）、実装済みの`call_postgres_function`はPostgres FUNCTIONへのscalar-only呼び出しでありjoin済み複数行結果を返せない。実際にDBを読む唯一の登録primitive（`sql_attention`/`recommendation_candidate_source`等）はいずれもbundle固有の専用C# adapter（`SqlAttentionLogsRepository`等を直接注入）であり、汎用`db_query` engineの実例ではない。加えて`manifest_authority`/`physical_table_binding`/`route_context`という3つのbinding_sourceは`db/topology_tables.sql`のCHECK制約には宣言済みだが`ResolveBinding`に対応caseが無く、使用すれば`ABSTRACT_FUNCTION_INPUT_BINDING_SOURCE_UNSUPPORTED`でfail-closeする（宣言済み・未実装というSSOT/DDL vs 実装のgapを具体的に発見・記録した——`abstract-function-primitive-registry-ssot.yaml backend_abstract_function_runtime_substrate.primitive_categories.db_operation`に監査結果を追記済み）。さらに`abstract_function_manifests.runtime_lane`は6値固定のCHECK制約であり、いずれも既存`/dispatch`経路・`ManifestDispatcher`/`RuntimeExecutor`自身のper-manifest dispatch・`SchedulerJobRunner`からのみ呼ばれる——`GET /hub-navigation/relations`のような、認証済みだがadmin限定ではない素の`Program.cs MapGet` route（既存の他の読み取り専用endpointと同型）に対応するlaneは存在しない。しかも既存のforward HubNavigation read自体（`runtime_executor` lane内の`ManifestDispatcher.EnrichWithHubNavigationAsync`から呼ばれる）も`HubNavigationResolver.ResolveAsync`という素のC# methodであり、`execute_abstract_function`を一度も経由していない——HubNavigationのread pathはforward/reverseとも、本repoの歴史上この substrateを使ったことが無い。**結論**: 抽象関数substrateへの接続はOwner判断待ちのtransport-shape決定を回避する近道にはならない——接続するには汎用`db_query` engineの新規構築（前例無し）か、`sql_attention`同様のbundle固有adapter新設（bundle-specific handlerを別の場所へ動かすだけ）のいずれかに加え、新しいruntime_laneの新設（schema変更）または不適切な`admin_runtime`への誤routing（認可境界が合わない）が必要になり、いずれも「既存substrateへの最小接続」ではない。上記「結論（Round38）」のOwner判断待ち2候補はそのまま有効であり、実装するとすれば既存のDTO/repository層のままで良い（`execute_abstract_function`へ載せ替える理由は無い）。
  - **Round40: Round39のownership framingを訂正（production factsは維持、結論は不変）**: Round39の技術的事実（未登録adapter・未実装binding source・runtime_lane固定6値）はそのまま正しいと再確認したが、「production adapterが無い→mechanism自体が存在しない」という読み方は誤りだったと訂正する。`docs/framework-policy.yaml abstract_function_substrate_policy`自身の`migration_order`は、各consuming bundleが自分の migration の最初のstepとして`ssot_fix_and_abstract_function_primitive_generation`（必要なprimitiveを自分で追加すること）を行う設計であり、別の「substrate専用Bundle」が先に汎用engineを用意しておく設計ではない。実際、`file_storage`（af05-af07）・Stripe・`audit_approval`のいずれも、自分が必要とするprimitiveを自分のBundleの中で新設している——`db_query`/`manifest_authorized_read`/`manifest_authority`/`physical_table_binding`/`route_context`についても、`docs/system-roadmap.yaml`・`.agent/tasks/todo.md`・commit historyをrepo-wide検索したが、これを専用に所有するBundleは存在しなかった。これは「誰も所有していない放置されたgap」ではなく、「必要とするBundleが自分で追加する」という既存policy通りの状態である。**ただしこの訂正はRound38/39の結論を変えない**: `db_query→result_context→projection`のdata-flowを具体的にtraceした結果、`projection` primitive自体が単なるresult_contextのdictionary化（passthrough）に過ぎないことを確認済み（Round39）——抽象関数substrateで組み立てても、position-0用の物理`hub_relations`行が存在しないという事実、`sequence_position=0`がどの行にも存在しないという事実はどちらも変わらない。Round38の制約は「どの計算機構で値を組み立てるか」ではなく「最終的なtransport DTOのfield契約（HubRelationId=行自身のid等）」そのものに由来するため、substrate選択（C# repositoryのまま／`execute_abstract_function`化）とtransport-shape選択（Round38の2候補）は独立した決定である——一方を選んでも他方の決定は必要なままである。結論: Round38のOwner判断待ち2候補は本Roundでも不変。`docs/design/abstract-function-primitive-registry-ssot.yaml`へ追記していたPR番号/Round番号/HubNavigation固有の監査履歴は、backend-wide canonical SSOTへBundle固有史を残さないというガバナンス原則に反していたため、本Roundで一般化（「どのBundleも所有していない」「migration_orderにより必要なBundleが追加する設計」という timeless facts のみを残し、PR #612/Round番号/HubNavigationへの直接参照を除去）した。HubRelation固有の監査史はこのSSOT（`bidirectional_read_projection_gap`）側にのみ残す。
- explicit branchのtrust boundary（`runtime-orchestration-ssot.yaml`の`explicit_current_topology_trust_boundary`で確定）: client-supplied `topologyManifestId`はrequest inputであり、Contextに乗っていたことだけではbackend側のauthorization proofにならない。paramが有る場合、まず既存`ManifestRepository.LoadByIdAsync`（`ManifestDispatcher.DispatchAsync`のtarget_ref pathおよび`ResolveFallbackNavigationLinksAsync`のsource manifest checkと同一メソッド）でsource Manifestの存在とactive statusを検証し、次に`active_topology_eligibility_predicate`（同SSOTで確定、下記）でcallerRoleとのeligibility一致を検証する。いずれかの失敗（missing/inactive/unauthorized）は明示fail-close（success化しない）。malformed UUIDはparam absentと同一視せず明示validation errorとする。検証を通過した場合のみ`HubNavigationResolver.ResolveAsync(topologyManifestId)`を呼び、target側にも同一`active_topology_eligibility_predicate`を適用してから返す — explicit branchの安全境界はfallback branchのresponse集合との包含関係ではない（両者はsource manifestが異なり、fallbackの既存per-target filterはactive_topology_eligibility_predicateより狭い既知バグを持つため、fallback基準の上限はpredicate自体の訂正を無効化してしまう）。実際の境界は「同一callerRoleに対しactive_topology_eligibility_predicateを満たさないtargetをexplicit branchが開示しない」ことそのものである。新しいNavigation専用RBAC/session-to-current-manifest ledger/parallel authorization tableは作らない。
- `active_topology_eligibility_predicate`（`runtime-orchestration-ssot.yaml`で確定、本Roundで再定義）: `NavigationTopologyVisibility(caller, topology) == ActiveTopologyEligibility(caller, topology)`というOwner確定invariantをSSOT化。ActiveTopologyEligibilityは「そのcallerが既存production runtimeの実際のexplicit Manifest選択口（`frontend/runtime/projectionEntry.ts resolveProjectionEntryAxes`が`?manifest=<uuid>`/`manifest_key:...:projection_entry`で常にlayer=`screen_list`/action=`Search`を送るdoor）から実際にそのManifestへ到達できるか」を意味する。`backend/runtime/ManifestDispatcher.cs`のtarget_ref分岐を直接検証: `manifest:<uuid>:projection_entry`は`AdminRuntimeTargetRefRe`の厳密shapeに一致しないため`IsGenericStructuralReadTargetRef`（`HasUiProjectionEntry && IsScreenReadAction`）分岐へ入り、実navigation targetは自身の`ui_projection` entryを持つため常にtrueとなってそのまま`ValidateCapabilityRequirement(topology, callerRole, "screen_list", "Search")`という単一gateへ到達する。`ManifestDispatcher.ResolveRequiredRole`自身のdoc commentが明記する通り`dispatcher_mapping.role`はこの経路の判定に一切使われない（"dispatcher_mapping.role is NOT used for inference...tautological check on axes paths"）。よって：**manifestはcallerRoleに対し、`ValidateCapabilityRequirement(topology, callerRole, "screen_list", "Search")`がAUTH_CAPABILITY_DENIEDを返さない場合にActiveTopologyEligibleである** — これのみ。`DispatcherMappingAxisAuthority.FindDeclaredRole`の合成・case (a)/(b)の分岐は撤回する（dispatcher_mapping entry有無で判定方法を変える理由が無くなったため）。
  - **`dd020`の事実訂正（再訂正）**: `dd020`（team_dashboard.normal.projection）は`runtime_destination:"admin_runtime"`、role="normal"のdispatcher_mapping 1件（`team_dashboard/get`）、layer/action-scoped（required_role省略=open）なcapability_requirement 2件（`screen_list/Search`、`team_dashboard/get`）、および自身の`ui_projection`を持つ。上記predicateを適用すると`ValidateCapabilityRequirement(dd020.topology, callerRole, "screen_list", "Search")`は`screen_list/Search`のscoped-open entryへ一致し、**callerRoleを問わず**（Normal・Admin・その他いずれのroleでも）eligibleとなる。dd020自身の`role="normal"`エントリは`team_dashboard/get`という別の(layer,action)に対する宣言であり、`?route=`のaxes選択（別の選択口、`ResolveActiveManifestAsync`のWHERE句）にのみ関与し、この`?manifest=`ベースのpredicateには一切関与しない。前Roundの「Admin callerはdd020経由でineligible（dd010で別途到達）」という結論は撤回する: `ManifestDispatcherLayerActionScopedCapabilityTests.DispatchAsync_DefaultScreenReadDeclared_StructuralReadReturnsRealData`が、dispatcher_mapping.role="admin"を持つmanifestでも、scoped-open な`(screen_list,Search)` capability_requirementがあればrole="user"でもtarget_ref経由でSuccess=trueになることを既存testとして証明しており、dd020も同型である。dd010はdd020のAdmin向け代替ではなく、独立して追加的にeligibleな別Admin-axis manifestとして扱う。
  - fallback resolver（`ResolveFallbackNavigationLinksAsync`）の既存per-target filterは、layer/action省略の`ResolveRequiredRole`に直接依存しており、dd020型のgapを引き続き持つ（撤回前のdispatcher_mapping-role合成よりもさらに狭い — layer/actionを一切見ないため）。同一`active_topology_eligibility_predicate`へ収束させることをforward design要件として記録（fallback endpoint自体のsemantic/canonical-default-entry挙動は変更しない、収束させるのはeligibility判定ロジックのみ）。
  - **既存関数visibility境界（再確認）**: predicateの再定義により、必要な既存関数は`ManifestDispatcher.ValidateCapabilityRequirement(topology, callerRole, "screen_list", "Search")`のみへ縮小した（`DispatcherMappingAxisAuthority.FindDeclaredRole`はこのpredicateの依存から外れる — 他用途では引き続き既存のまま）。`ValidateCapabilityRequirement`は`private static`（`ManifestDispatcher`class内限定）であり、`HubNavigationResolver`側からは現状呼び出し不可能 — 後段implementation_changeで可視性を`internal`へ変更するか、同じ判定ロジックを`ManifestDispatcher`/`HubNavigationResolver`双方から呼べる共有helperへ抽出する必要がある。具体的な手段はAgent判断とするが、Navigation専用の並行authorization実装を新設して`ValidateCapabilityRequirement`と別の判定ロジックを持たせることは禁止する。
- **`minimum_cardinality_completion_invariant`との整合**（`runtime-orchestration-ssot.yaml minimum_cardinality_completion_invariant_relationship`で確定）: `db-schema.yaml hub_relations.minimum_cardinality_completion_invariant`（`docs/framework-core.yaml production_projection_connectivity_invariant`とmirror）は無変更・無弱体化のまま維持する。この invariant が扱う「navigation orphan」はmanifest自身のOWN outbound行数（forward方向）のみを見るDB-level完成度概念であり、navigation Islandがそのmanifestへ/そのmanifestから経路を提示できるか（reverse方向のcandidate discoveryを含む runtime read 契約）とは別軸である。`ae200`/`5c100`/`092`は、この invariant の enforcement boundary（`PromoteAsync`）を経ずraw SQLでactiveとしてseedされた既存行であり、自分自身のOWN outbound行という意味では既に（本Bundleと無関係に）navigation orphan状態にある — `dd010`→3 targetsのrow登録は`dd010`自身のoutbound cardinalityを満たすが、3 targets自身のOWN-outbound orphan状態を解消するものではなく、それは本Bundleのscope外（repo全体のseed-completeness項目）である。`no_return_edge_bootstrap_requirement`はRound37で単一規則へ再統一され、reverse方向のcandidate discoveryは隣接occurrenceがS自身（位置0）である場合もordinary target（位置k>=1）である場合も同一規則で返す — `5c100`は自分自身のOWN outbound行が無いままでも`ae200`をprevとして持てる（同じsequenceの隣接要素のため）のと全く同じ理由で、`ae200`は`dd010`自身をprevとして持つ（`dd010`は`ae200`のOWN outbound行の有無とは無関係にsequence上の隣接要素として得られる）。**Round35訂正はそのまま維持**: `/dashboard`は`dd020`（Normal-axis）自身のrouteであって`dd010`（Admin-axis、`/admin/team-dashboard`）とは無関係であり、Home linkが`dd010`への復帰を意味するという以前の記載は誤りだった。**Round37時点での意味**: この訂正は依然事実として正しいが、`dd010`への復帰はもはやHome affordanceに依存する必要が無い——`ae200`のfixed-navigation prevとして`dd010`自身が直接得られるため、Home linkの役割はsequence先頭/末尾の真にmissing-sideな場合のgeneric fallbackに限定される。「navigation orphan（DB-level完成度）」と「navigation Islandから到達不能（runtime reachability）」は同義ではない、という本項の主旨自体は変わらない。
  - **既存guardのactive-count/resolvable-cardinality混同の切り分け**（`db-schema.yaml target_reference_canonical_contract.deprecate_and_update_guard_resolvability_distinction`で確定）: `NpgsqlContentBundleRepository.DeprecateHubRelationAsync`の既存regression guard（`HUB_RELATION_LAST_ACTIVE_FOR_MANIFEST`）はraw `status='active'`行数のみを見ており、resolvableかどうかは見ない。同一sourceに複数のactive行があり、そのうち1件だけがresolvableな場合、そのresolvable行をdeprecateしてもこのguardは通ってしまい、`minimum_cardinality_completion_invariant`上のorphanを検出しないまま作り得る。`UpdateHubRelationAsync`にはresolvability guardが一切無く、resolvableな行を無guardでunresolvableなHubへ書き換えられる。**第三の経路**: `NpgsqlManifestRepository.DeprecateAsync`（hub_relations行ではなくManifest自体をdeprecateする、別method）は、そのManifestを`target_topology_manifest_id`/`related_hub_id`で参照する他manifestのhub_relations行に対して一切checkを行わない — 1つの被参照Manifestをdeprecateするだけで、無関係な複数のsource manifestのresolvable-cardinalityを同時に0へ落とし得る（`db-schema.yaml`同field・`runtime-orchestration-ssot.yaml production_projection_connectivity_invariant.enforcement_boundary`で確定済み）。repo-wide探索の結果、これら3経路（`DeprecateHubRelationAsync`のraw active-count guard、`UpdateHubRelationAsync`のguard欠如、`DeprecateAsync`のincoming-reference check欠如）を解消する既存Bundle・task・mechanismは`.agent/tasks/todo.md`内に他に存在しなかった。いずれも本design_change / 本Bundleのmigration disposition（batch classification、上記(2)参照）とは独立した既存本番条件であり、本Bundleのmigrationはこれに依存しないため影響を受けない。これら3経路をresolvability-awareにすることは、本design_changeの決定事項とは別の変更であり、本Bundleでは着手しない。**Round34での分離**: 既存の適切なtracking先が無かったため、本Bundle自身の対象ファイル名リストへ記録していたが、この known gap は本Bundleの2目的（Enum jump到達性、`target_topology_manifest_id`移行）のいずれとも独立した別種の変更であり、本Bundleの実装スコープへ混入させるべきではないと判断し、独立`not_started` Bundle `hub-relations-deprecate-update-resolvability-guard-gap`（索引参照）として分離した。本Bundle・次段HubRelation Bundleのいずれもこのgapを実装しない。
- app-shell navigation Island: UI Builder非依存・ProjectionShell非依存。`frontend/routes/_app.tsx`をapp shellとして使う。current topology identityをContextから読み、上記の拡張済み`GET /hub-navigation/relations`（`?topologyManifestId=<current>`）を呼んでcurrent topologyが所属する各sequenceのimmediate prev/nextを取得し（forward read——current topologyがS＝位置0として立つ場合のnext——と、reverse read——current topologyがいずれかのoccurrence（位置0のS自身、または位置k>=1のtarget）として出現する場合のprev/next——の両方から、単一規則の下で区別なく導出、上記参照）、それらを単一へ縮退させず提示する。target選択後の遷移は既存`?manifest=`遷移entryを再利用し、frontend側でattractor/structure-map解決を複製しない。
- Context boundary: Fresh 1.7.3の実際のclient hydration algorithm（`src/runtime/entrypoints/main.ts`の`revive()`/`_walkInner()`、`deno.json`固定バージョンから直接取得して確認）を根拠とする。islandの閉じmarkerが独立した`render()`呼び出し（別Preact root）になるのは、その時点で他のislandのmarkerがmarker stack上に開いていない場合のみ。あるislandが別のislandの subtree内にnestされている場合（outer islandが自分のJSX内で直接renderする場合、またはrouteからouter islandのchildrenとして渡す場合の両方 — 既存`frontend/islands/AdminAuthGate.tsx`が`frontend/islands/ProjectionShell.tsx`を包む現行patternが正にこれ）、そのislandはouter islandと同じvnode treeに畳み込まれ、1回の`render()`で一緒にhydrateされる。したがって文字通りの`createContext`/`Context.Provider`は、outer top-level islandからnestされたislandの`useContext`へ正しく伝播する。canonical designはOwner指定通り: `frontend/routes/_app.tsx`から`{Component}`を包む新しいtop-level app-shell Islandをmountし、そこで`useState`によりcurrent topology identityを所有して`Context.Provider`で公開する。navigation Islandと（既存route nestingを通じた）ProjectionShellはそのnested descendantとして`useContext`で読む。module-scope subscribable storeやRedux/Zustandは使わない。Contextの値は最小限（current topology identity + 明示的なupdate関数）とし、URL selection/backend dispatch resolutionのauthorityを上書きしない。ProjectionShellは`adoptResolvedManifestIdentity`/`adoptedManifestIdRef`確定後にのみupdate関数を呼ぶ。
- ProjectionShell役割縮小: dispatch/loading/error/emission/specs/node-value/local runtime state/SSE lifecycleは維持したまま、`resolveHubNavigationLinks(emission.navigationSequence)`によるnav bar表示は撤退し、navigation Islandとの二重表示を避ける。撤退作業自体は次段implementation_changeの対象。**撤退境界の明示**（実DOM構造を確認済み）: `frontend/islands/ProjectionShell.tsx`の`<nav data-projection-hub-navigation>`内で、`hubNavigationLinks.map(...)`由来の`data-hub-navigation-resolvable`/`data-hub-navigation-unresolvable`要素のみが撤退対象であり、同じ`<nav>`内に兄弟要素として存在する`data-projection-home-link`（`href="/dashboard"`、「ホーム」）は`resolveHubNavigationLinks`の出力ではなく撤退対象に含まれない。両者が同一`<nav>`要素を共有しているため、この撤退を`<nav>`ブロックごと削除する形で実装すると誤ってHome affordanceも削除してしまう — Home anchorのtext/href/無条件render挙動は撤退後も維持すること（`<nav>`ラッパーをHome単独用に残すか、別要素へ移すかはimplementation_change判断でよいが、動作は不変）。
- **selection observation forward要件**（`runtime-orchestration-ssot.yaml selection_observation_forward_requirement`で確定、本Roundで四層に分離）: 表示順序authority（`sequence_position`、uncollapsed）は無変更。別途、fixed sequence linkの選択操作は`context-route-recommendation.yaml lanes.ui_pressure`への観測材料となるべきというOwner意図がある。実装事実をproducer/persistence/SSOT宣言/consumerの四層に分離して検証する（SSOT宣言をproduction実装済みの証拠として扱わない）: (A) `frontend/islands/ProjectionShell.tsx`の現行nav bar linkは素の`<a href>`でonClickが無く、`frontend/runtime/frontendScheduler.ts emitComponentOperationEvent`への接続は現在存在しない（本Bundle以前から未接続、本Bundleが壊したものではない）。(B) producer/persistence chainは既存で再利用可能: `emitComponentOperationEvent` → `backend/endpoint/ComponentEventAppendEndpoint.cs` → `ContextRouteRepository.AppendComponentOperationEventLogAsync` → `component_operation_event_log`（db-schema.yaml上のrole: recommendation pressure signal collection）。(C) `context-route-recommendation.yaml lanes.ui_pressure`は`component_operation_event_log`をsourceとしてcanonical宣言済みだが、これはSSOT authority factであり、実際に何かがこのtableを読んでいる証拠ではない。(D) repo-wide調査の結果、`component_operation_event_log`を読むproduction consumer/aggregation mechanismは現時点で存在しない（`backend/runtime/ContextRouteRecommendationResolver.cs`の実際の`ui_pressure`/`next_operation`計算は`context_event`/`context_transition_stats`のみを使用し、`component_operation_event_log`には一切触れない。同tableに触れる唯一のbackend methodはwrite-onlyの`AppendComponentOperationEventLogAsync`）。本Bundle自身のselection-observation受入境界は(A)+(B)のみとする: 次段implementation_changeでapp-shell navigation Islandを構築する際、そのlink選択handlerが既存`emitComponentOperationEvent`を呼び`component_operation_event_log`へ永続化するところまでを本Bundleの受入条件とし、`ui_pressure`のoutput（`next_operation`/`next_component`/`next_route_action`）が実際に生成されることは本Bundleの受入条件に含めない。新しいcounter table/endpoint/store/event laneは作らない。`framework-core.yaml runtime_route_attention_boundary`の`attention_score_must_not_override_fixed_route`は無影響（観測はどのcandidateが表示されるか・どのhrefへ遷移するかに一切影響しない、純粋な追加材料）。(C)-(D)間のgap（`component_operation_event_log`がSSOT上`ui_pressure` sourceと宣言されているにもかかわらずproduction consumerが存在しないこと）は本Bundleとは独立した、hub-relation navigationと無関係な既存条件であり、本Bundleへ吸収せず独立Bundle`ui-pressure-component-event-log-consumer-gap`（本索引ファイル参照）へ引き継ぐ。

### 対応資料

- `docs/design/db-schema.yaml` `hub_relations.key_columns` / `hub_relations.target_reference_canonical_contract`
- `docs/design/admin-console-workflow-ssot.yaml` `admin_hub_relation_navigation_contract`（`resolution_rule` / `canonical_forward_target_reference` / `bootstrap_seed_vs_runtime_authoring_boundary` / `axis_navigation_membership`）
- `docs/design/runtime-orchestration-ssot.yaml` `ui_projection_render_reachability_contract.hub_navigation_resolution`（`canonical_forward_target_reference`）、`ui_projection_render_reachability_contract.hub_relation_sequence_membership_and_navigation_island_contract`（`navigation_island_transport_boundary`/`explicit_current_topology_trust_boundary`含む）、`admin_route_retirement_matrix.routes`（`/admin`エントリ）
- `docs/design/admin-normal-surface-projection-seed-ssot.yaml` `hub_relation_navigation_binding`
- `db/topology_tables.sql`（`hubs.hub_relations`のsequence semantics正本コメント）

### 次段（implementation_change）が開始する手順

1. `target_topology_manifest_id`のSQL DDL追加とbackfill。NOT NULL要件は`status='active'`行限定のCHECK制約とする（column-level NOT NULLにしない）。既存rowは`legacy_ambiguous_row_migration_disposition`に従いbackfill: exactly-one-active-target行はbackfill、zero/multiple-active-target行は`status='deprecated'`へ遷移（fabricationしない）。deprecate化がsource側を新しいorphanにする行はmigrationをfail closeし手動対応へ回す。source `topology_manifest_id`とtarget `target_topology_manifest_id`が同一UUIDであるrelationはvalidationで拒否しない（合法）。
2. 旧SELF_LOOPガード（`NpgsqlContentBundleRepository.CreateHubRelationAsync`/`UpdateHubRelationAsync`のHub-identity比較）を削除する。移植先・代替guardは作らない。`canonical_default_entry_contract`の自己参照シード行（manifest 092）は、ガード削除後も特別扱い不要でそのまま到達可能である。
3. `LoadHubNavigationSequenceAsync`等のtarget解決を、`related_hub_id`ベースのexactly-one推論から`target_topology_manifest_id`の直接FK existence+status checkへ置換する。`related_hub_id`は物理削除せず、legacy fieldとして残す。この手順の範囲では新しいread projectionは追加しない（既存メソッドのsource column切り替えのみ）— これは目的2（target resolution column移行）の範囲限定であり、目的3が別途手順6/9で追加するreverse方向のadditive read method（prev/next導出用、新しいtable/columnは追加しない）とは独立した、矛盾しないスコープである。
4. Admin Manifests UI（`HubNavigationAdmin.tsx`、既存`/admin/manifests`）のauthoring formを、target Hub選択から既存Manifest一覧を再利用したtarget Manifest直接選択へ変更する。
5. `admin_hub_relation_navigation_contract.axis_navigation_membership`が定義するAdmin source（`dd010`）→3 targets（`ae200`/`5c100`/`092`）のHubRelation行のみ（復路rowは不要）を、`db/seed_empty.sql`へのpre-built rowとして実登録し、`target_topology_manifest_id`をtarget identityとして使用する。`ae210`〜`ae280`・external-port consumer projectionへは登録しない。`ad200`は本Bundleのadmin axis navigation membershipから除外されているため、`ad200`関連のHubRelation行は一切登録しない。
6. `backend/Program.cs`の既存`GET /hub-navigation/relations`を、`navigation_island_transport_boundary`/`explicit_current_topology_trust_boundary`が定義する最小extensionへ拡張する: 任意の明示`topologyManifestId` query paramを追加し、有る場合は(a)`ManifestRepository.LoadByIdAsync`でsource Manifestの存在・active statusを検証、(b)`active_topology_eligibility_predicate`（`ManifestDispatcher.ValidateCapabilityRequirement(topology, callerRole, "screen_list", "Search")`の単一呼び出し — `?manifest=`が実際に送る固定axesと同一）でcallerRoleとのeligibility一致を検証、(c)いずれかの失敗を明示fail-closeとして返す、(d)通過後のみforward方向`HubNavigationResolver.ResolveAsync(topologyManifestId)`（ownerとして立っている場合はその最小`sequence_position`行のみをnextとして採用）とreverse方向の追加read method（`bidirectional_read_projection_gap`が定義する、current topologyをtargetとして名指すrow（owner manifestは問わない — `topology_manifest_id == target_topology_manifest_id`の自己参照行を含む）を見つけ、そのowner内の隣接`sequence_position`からprev/nextを導出する additive query）の両方を呼んでcurrent topologyのprev/next対を導出し、target側にも同一`active_topology_eligibility_predicate`を適用して返す、(e) malformed UUIDはabsentと区別し明示validation errorとする。無い場合は既存`ResolveFallbackNavigationLinksAsync`によるfallback挙動を無変更で維持する。auth境界（任意の認証済みJWT、admin限定ではない）は変更しない。
7. `frontend/routes/_app.tsx`から`{Component}`を包む新しいtop-level app-shell Islandを新設し、`useState`でcurrent topology identityを所有して`Context.Provider`で公開する（file/component名はAgent判断。既存`frontend/islands/AdminAuthGate.tsx`のchildren-wrapping patternを踏襲してよい）。
8. 手順6の拡張済み`GET /hub-navigation/relations`を呼ぶfrontend API client関数を新設する（`queueClientCommand`/`dispatchOperation`の`/dispatch` POST経路は使わない、独立したGET boundary。配置ファイルはAgent判断）。
9. app-shell navigation Island（UI Builder非依存・ProjectionShell非依存、具体的なfile名はAgent判断）を新設し、手順7のContextからcurrent topology identityを`useContext`で読み、手順8のclient関数で`?topologyManifestId=<current>`を呼んでそのmanifestが所属する各sequence membershipのimmediate prev/next（自分がownerの場合の(a)方向、target memberの場合の(b)方向reverse readの双方から導出）を取得して提示し、選択後は既存`?manifest=`遷移を再利用する。
10. `ProjectionShell.tsx`が手順7のContext更新関数を`adoptResolvedManifestIdentity`確定後に呼ぶproducerとして接続し、`resolveHubNavigationLinks(emission.navigationSequence)`によるnav bar表示を撤退してnavigation Islandとの二重表示を避ける。`ad200`は本Bundleのadmin axis navigation membershipから除外済みのため、`ad200`自身の`hub_relation_link_list`card_list retirement作業は本Bundleでは不要（`dd010`自身の`seed_contract`にはnavigationSequence-boundなcard_listが元々存在しないため、同種の二重表示懸念も無い）。
11. （撤回・本Bundleでは不要）以前のRoundは`/admin`の現行static body（`frontend/routes/admin/index.tsx`）を`ad200`にpinしたProjectionShell thin wrapperへ置換する手順としていたが、Admin navigation sourceが`dd010`（既存`/admin/team-dashboard`route）へ訂正されたことにより不要になった。`/admin`の現行static bodyは無変更のまま、本Bundleのscope外。

対象ファイル名/対象関数名（初期スコープの目安、実装順・内部作業境界はAgent判断）:
- `db/topology_tables.sql`（`hubs.hub_relations`テーブル定義）
- `db/seed_empty.sql`（axis navigation registration、`dd010 -> {ae200,5c100,092}`の3 forward rowのみ。`/admin`route自体は無変更・本Bundleのscope外）
- `backend/repository/NpgsqlContentBundleRepository.cs`（`CreateHubRelationAsync`/`UpdateHubRelationAsync`/`LoadHubNavigationSequenceAsync`/`DeprecateHubRelationAsync`。**scope境界の明示（Round35）**: 本Bundleでの`DeprecateHubRelationAsync`への言及は、legacy row migration（上記(2)、`legacy_ambiguous_row_migration_disposition`のstep 2）が既存`status='deprecated'`値を再利用する点、および`UpdateHubRelationAsync`/`CreateHubRelationAsync`が新列`target_topology_manifest_id`を受理・永続化する点のみを指す — `DeprecateHubRelationAsync`自身のraw active-count guard欠陥（resolvable-cardinality非対応）を修正する作業は含まない。そのguard修正は独立Bundle`hub-relations-deprecate-update-resolvability-guard-gap`（索引参照）のscopeであり、本Bundleはこれに依存しない。加えて`bidirectional_read_projection_gap`が定義するreverse方向のadditive read method新設 — `LoadHubNavigationSequenceAsync`と対をなす、current topologyをtargetとして名指すrow（source manifestは問わない。自己参照行を含む）を全て見つけ、各行ごとに、その`sequence_position`が示す位置kのoccurrenceとして、同じsequence内の隣接要素（位置k-1——k=1ならsource S自身、k>=2ならordinary target——・位置k+1が存在すればそれ）からcurrent topologyのprev/nextを独立に導出して返すmethod（Round37: 隣接要素がS自身である場合とordinary targetである場合を区別せず同一規則で扱う——S自身を候補として返すことを含む）。同一sourceの複数行が同じtargetを指す場合はそれぞれのpositionを別occurrenceとして扱いdedupeしない。具体的なmethod名はAgent判断。新しいtable/columnは追加しない。**Round38訂正**: 隣接要素が位置k>=1（ordinary target）である場合のDTOは既存`HubNavigationSequenceItemDto`をそのまま再利用できる（無変更・真実性を保つ、Round33の結論はこの部分について維持）。隣接要素が位置0（S自身）である場合のDTO shapeは、HubRelationId/RelatedHubId/SequencePositionの3fieldについて既存DTOのままでは真実性を保てないというOwner判断待ちの未決事項であり（詳細は`runtime-orchestration-ssot.yaml bidirectional_read_projection_gap`のRound38 Gate-0監査、および上記「DTO representability」bullet参照）、「新規DTO不要」と決め付けない——実装はこのOwner判断が確定してから着手する。
- `backend/schema/ContentBundleContracts.cs`（`HubNavigationHubRelationItemDto`/`HubNavigationSequenceItemDto`/`HubNavigationCreateRequestDto`/`HubNavigationUpdateRequestDto`）
- `frontend/api/adminApi.ts`（`createHubRelation`/`updateHubRelation`）
- `frontend/islands/HubNavigationAdmin.tsx`（target picker）
- `frontend/routes/_app.tsx`（app-shell top-level Islandの配線）
- `backend/Program.cs`（`GET /hub-navigation/relations`ハンドラへの明示`topologyManifestId` query param extension + trust boundary validation呼び出し。既存fallback分岐は無変更）
- `backend/runtime/HubNavigationResolver.cs`（source存在/active/eligibility検証とper-target filterを行う新public method — 既存`ResolveFallbackNavigationLinksAsync`のper-target filter loopを`active_topology_eligibility_predicate`へ収束させたうえで共有抽出して再利用する。`ManifestRepository.LoadByIdAsync`/`ManifestDispatcher.ValidateCapabilityRequirement`を再利用し、新しいparallel authorization logicは作らない。`DispatcherMappingAxisAuthority.FindDeclaredRole`はこのpredicateの依存対象から外れた — 他用途の既存利用は無変更。加えてforward方向`LoadHubNavigationSequenceAsync`呼び出し（ownerとして立つ場合はその最小`sequence_position`行のみをnextとして採用）と、`NpgsqlContentBundleRepository.cs`新設のreverse方向additive read method呼び出しから、current topologyのprev/next対を導出する）
- `backend/runtime/ManifestDispatcher.cs` `ValidateCapabilityRequirement`（現状`private static`で`HubNavigationResolver`から呼び出し不可。`active_topology_eligibility_predicate`は`ValidateCapabilityRequirement(topology, callerRole, "screen_list", "Search")`の単一呼び出しのみに依存するため、`internal`化または共有helperへの抽出のいずれかで可視性を解消する。手段はAgent判断、並行authorization実装は禁止）
- `backend/schema/ContentBundleContracts.cs`（`HubRelationNavigationLinksResponseDto`の`fallbackReason`値追加、または明示branch用のvalidation error code追加。この外側envelope自体は新規DTOを作らない——これは`links`配列の要素型`HubNavigationSequenceItemDto`とは別の話であり、要素型側の位置0 occurrence表現に関する未決事項（上記「DTO representability」参照）とは独立）
- 新設: frontend hub-navigation read client（`frontend/api/`配下、拡張済み`GET /hub-navigation/relations`を呼ぶ独立fetch関数、`/dispatch`経路は使わない、file名はAgent判断）
- 新設: app-shell top-level Island（`frontend/islands/`配下、`useState`+`Context.Provider`所有、file名はAgent判断）
- 新設: app-shell navigation Island（`frontend/islands/`配下、file名はAgent判断。link選択handlerで既存`frontend/runtime/frontendScheduler.ts emitComponentOperationEvent`を呼ぶ — selection observation forward要件、上記参照。新しいcounter mechanismは作らない）
- `frontend/runtime/frontendScheduler.ts` `emitComponentOperationEvent`（既存、selection observation forward要件で再利用— 変更不要、呼び出し側のみ新設）
- `frontend/islands/ProjectionShell.tsx`（current topology publication接続、nav bar表示の撤退）
- `backend/tests/Topolactor.Integration.Tests/*HubRelationUiProjectionLiveDbTests.cs`、`ManifestDraftActivePromotionLifecycleLiveDbTests.cs`、`AdminRuntimeContentBundleTests.cs`（SELF_LOOP assertion）、`backend/tests/Topolactor.Runtime.Tests/InMemoryContentBundleRepository.cs`
- `backend/tests/Topolactor.Runtime.Tests/HubNavigationFallbackLinksTests.cs`（既存fallback回帰防止、`topologyManifestId`明示branchの新規test追加）

### 次段（implementation_change）の受入条件

- [ ] SQL: `hubs.hub_relations`へ`target_topology_manifest_id UUID`列を追加（FK to `hubs.topology_manifests`）。NOT NULL要件は`status='active'`行限定のCHECK制約とする。exactly-one-active-target行をbackfillし、zero/multiple-active-target行は`status='deprecated'`へ遷移（fabricationしない）。deprecate化が新しいorphanを作る行はmigrationをfail closeする。destructive DROP CASCADE無し。
- [ ] backend: `hub_navigation:create`/`hub_navigation:update`が`target_topology_manifest_id`を受理・永続化する。target解決を直接FK existence+status checkへ置換する（`related_hub_id`は物理削除せず残す）。旧SELF_LOOPガードを削除し、代替guardを追加しない。`source == target` Manifest UUIDのrelationがvalidationで拒否されないことを確認する。目的2のこの範囲では新しいbackend read projectionは追加されていない（既存`LoadHubNavigationSequenceAsync`のsource column切り替えのみであることを確認する。目的3が別途追加するreverse方向のadditive read methodはこの受入条件の対象外 — 下記のprev/next受入条件を参照）。
- [ ] Admin Manifests UI: `HubNavigationAdmin.tsx`のauthoring formが、target Hub選択から既存Manifest一覧を再利用したtarget Manifest直接選択へ変わる。
- [ ] axis navigation registration: Admin source（`dd010`）→3 targets（`ae200`/`5c100`/`092`）のHubRelation行のみを`db/seed_empty.sql`へのpre-built rowとして実登録し、`target_topology_manifest_id`をtarget identityとして使用する。復路rowは追加しない。`ae210`〜`ae280`・external-port consumer projection・`ad200`へは登録しないことを確認する。
- [ ] `dd010`のproduction reachability: 既存`/admin/team-dashboard`route（`frontend/routes/admin/team-dashboard/index.tsx`）が既に`team_dashboard.admin.projection`をmanifestKeyでpinしていることを確認する（新しいrouteは作らない）。`/admin`（`frontend/routes/admin/index.tsx`）の現行static bodyは無変更であることを確認する（本Bundleのscope外）。
- [ ] legacy row migration proof: exactly-one-active-target行が正しくbackfillされること、zero/multiple-active-target行が`status='deprecated'`へ遷移すること、この遷移によって不可視になるのはvisible unresolvable placeholder（`span[data-hub-navigation-unresolvable]`）のみで動作していたnavigation targetの後退ではないことを含めてtestで確認する。同一sourceに複数のambiguous行があるfixtureで、batch classification（既存`hub_navigation:deprecate`のper-row guardのrow-by-row再利用ではない）によって新規orphanがfail closeされ手動対応へ回ることを確認する。
- [ ] `active_topology_eligibility_predicate`実装のvisibility proof: `ManifestDispatcher.ValidateCapabilityRequirement`が`HubNavigationResolver`から実際に呼び出せる状態（`internal`化または共有helper抽出のいずれか）になっており、Navigation専用の並行authorization実装が新設されていないことを確認する。
- [ ] （本Bundleでは不要）`ad200`は本Bundleのadmin axis navigation membershipから除外済みのため、`ad200`自身のdual-authority retirement proofは対象外。`dd010`自身の`seed_contract`にはnavigationSequence-boundなcard_listが元々存在しないため、同種の二重表示proofも不要。
- [ ] backend: `GET /hub-navigation/relations`が任意の明示`topologyManifestId` query paramを受理する。有る場合はsource Manifestの存在/active status/`active_topology_eligibility_predicate`（`ManifestDispatcher.ValidateCapabilityRequirement(topology, callerRole, "screen_list", "Search")`単一呼び出し — dispatcher_mapping合成は行わない。navigation専用のfake layer/actionは作らない）を検証したうえで、forward方向`HubNavigationResolver.ResolveAsync(topologyManifestId)`（ownerとして立つ場合は最小`sequence_position`行のみをnextとして採用）とreverse方向の追加additive read method（`bidirectional_read_projection_gap`参照。隣接`sequence_position`からprev/nextを導出する）の両方からcurrent topologyのimmediate prev/next対を導出し、target側にも同一predicateを適用し、sequence membershipごとのprev/nextのみを可視candidateとして返す（sequenceの他memberは返さない）。無い場合は既存`ResolveFallbackNavigationLinksAsync`によるcanonical-default-entry fallback挙動が無変更であることを既存test（`HubNavigationFallbackLinksTests.cs`）で回帰確認する。auth境界（admin限定ではない既存JWT/session）は変更しない。新しいbackend endpoint/dispatcher/Navigation専用RBACは追加しない（reverse方向のadditive read methodは追加するが、新しいtable/columnは追加しない）。
- [ ] backend explicit branch trust-boundary proof: source Manifest存在+active+eligibleのpositive case、malformed UUID negative、source missing negative、source inactive negative、source ineligible negative、valid+0行のsuccess-empty、valid+複数行のordered-return、target filteringが同一callerRoleに対し`active_topology_eligibility_predicate`を満たさないtargetを一件も開示しないこと（fallback branchのresponse集合との比較ではない）の各caseをtestで確認する。
- [ ] Active topology eligibility proof: Normal caller → Admin-only topology（`ae200`/`5c100`/`dd010`/`092`など、`(screen_list,Search)`にscoped-openなcapability_requirementを持たずadmin_runtime推論のみが効くmanifestをsource/targetとするrelation）のNavigation応答からUUID/label/href全てが不在であることのnegative proof。Admin caller → 同じAdmin topologyでrelation candidateが通常表示されるpositive proof。Normal caller AND Admin caller → 双方とも`dd020`をsource/target topologyとした場合にeligibleとして返ること（`dd020`は`(screen_list,Search)`にscoped-openなcapability_requirementを持つため、`runtime_destination=admin_runtime`にもdispatcher_mapping.role="normal"にも関わらずcallerRoleを問わずeligibleになる、という再訂正版dd020 worked exampleの直接検証。Admin callerを「dd010経由でのみ到達」と扱って`dd020`から除外しないこと）。source=`dd020`（Normalがeligibleな実在manifest）/target=Admin-only（`ae200`等）のcross-boundary fixtureで、そのhub_relations DB row自体は存在してもtarget candidateがNormal caller向けresponseへ漏れないnegative proof。
- [ ] frontend: `frontend/routes/_app.tsx`から`{Component}`を包む新しいtop-level app-shell Islandが新設され、`useState`+`Context.Provider`でcurrent topology identityを所有している。`ProjectionShell.tsx`が`adoptResolvedManifestIdentity`確定後にContext更新関数を呼ぶproducerとして接続されている。
- [ ] frontend: 拡張済み`GET /hub-navigation/relations`を呼ぶ独立fetch client関数が新設される（`/dispatch`経路を使わない）。
- [ ] frontend: app-shell navigation IslandがUI Builder layout/component treeに依存せず新設され、Contextからcurrent topology identityを読み、上記client関数で`?topologyManifestId=<current>`を呼び出し、各sequence membershipのimmediate prev/nextのみを受け取り表示する（sequenceの他memberは受け取らない。新しいbackend endpoint/tableは作らない）。
- [ ] frontend DOM proof 1: `092`（credential-management）等のAdmin projectionを表示中、app-shell navigation Islandが存在し、UI Builder layout/component treeに依存していないことを証明する。
- [ ] frontend DOM proof 2: current topology identityがContextで共有されており、`ProjectionShell.tsx`が採用したidentityとnavigation Islandが読むidentityが一致し、navigation Islandのrequestが同一Manifest IDを`topologyManifestId`として送っていることを証明する。
- [ ] frontend DOM proof 3: current topologyが2つの異なるsequence（Owner例のA=[1,5,7,9,15]/B=[4,2,3,8,9,6]と同型のtarget列。各sequenceのownerの識別子はfixtureが指定しないため追加特定しない）それぞれの中でtarget行として独立にindex occurrenceを持つfixture（manifest 9がAの中で7/15に、Bの中で8/6に隣接する）で、両方のindex occurrenceから導出されるprev/next（7・15・8・6の4件）が失われずそれぞれ独立に提示され、いずれのsequenceの他member（1つのsequenceに3件以上ある場合の残り）も列挙されないことを証明する。
- [ ] frontend DOM proof 3b（reverse方向、Admin axis、Round37単一規則）: logical sequence`[dd010, ae200, 5c100, 092]`の全位置へ同一規則を適用したことを証明する。`dd010`（位置0）を表示中はprevが無く、next=`ae200`のみであり（`5c100`/`092`が直接candidate化されないこと）を証明する。`ae200`（位置1、自分自身のoutbound行を持たない）を表示中は**prev=`dd010`**、next=`5c100`であることを証明する——`dd010`が位置0という通常のsequence要素としてprevに現れることの直接確認であり、旧Round33〜36のowner-case/member-case分割下でのnegative-proof（dd010は現れない）とは逆の主張である点に注意。`5c100`（位置2）を表示中はprev=`ae200`、next=`092`であることを証明する。`092`（位置3、sequence最後）を表示中はprev=`5c100`、nextが無いことを証明する。新しいbridge manifest/dispatch axis/architectureを追加していないことを確認する。
- [ ] frontend DOM proof 4: Enum candidate `ae200`をnavigation Islandから選択し、実際に`ae200`のprojectionへ到達できることを証明する。
- [ ] frontend negative proof: current topologyが、自分がownerであるsequenceからのnextも、他manifestのsequenceのtarget memberとしてのprev/nextも一切導出できない（自分をtargetとして名指す他manifestのrowが存在せず、自分自身のoutbound行も存在しない）という真に孤立したfixtureのとき、navigation Islandが空を表示し、canonical default entry（`ResolveFallbackNavigationLinksAsync`側の挙動）や`dd010`等へ暗黙fallbackしていないことを証明する。自分自身のoutbound行が0件でもreverse方向のmembershipからprev/nextが導出できれば空にならないことも別途確認する（DOM proof 3b参照）。
- [ ] frontend DOM proof 5: navigation IslandがProjectionShellと同じfixed-navigationを二重表示していないことを証明する（`ProjectionShell.tsx`の`resolveHubNavigationLinks`によるnav bar表示（`data-hub-navigation-resolvable`/`data-hub-navigation-unresolvable`）が撤退済みであることを含む）。**同時に**、`data-projection-home-link`（`href="/dashboard"`、「ホーム」）が撤退後も引き続きDOM上に存在し、無条件にrenderされていることを証明する negative/positive proof を含める — この撤退がHome affordance自体を巻き添えで削除していないことの直接確認。
- [ ] selection observation proof（本Bundleの受入境界は(A)+(B)のみ、`ui_pressure` output生成は含まない）: navigation Islandでfixed sequence linkを選択した際、既存`emitComponentOperationEvent`（`frontendScheduler.ts`）が呼ばれ、`ComponentEventAppendEndpoint`経由で`component_operation_event_log`へ実際に記録されることを証明する。新しいcounter table/endpoint/store/event laneが追加されていないこと、`sequence_position`順の表示・遷移先hrefが観測ロジックにより一切変わらないことを確認する。`component_operation_event_log`から`ui_pressure`のoutput（`next_operation`/`next_component`/`next_route_action`）が実際に生成されることの証明は本Bundleの受入条件に含めない — 独立Bundle`ui-pressure-component-event-log-consumer-gap`（索引参照）へ引き継ぐ。
- [ ] 本Bundleの完了判定はCI greenのみを根拠にしない。SSOT契約・実装・testの意味的整合を監査役が個別に確認したうえで判定する。

---

## Bundle `hub-relations-deprecate-update-resolvability-guard-gap`

**Status:** `not_started`
**Primary SSOT:** `docs/design/db-schema.yaml` `db_schema.tables.hub_relations.target_reference_canonical_contract.deprecate_and_update_guard_resolvability_distinction`（正本）/ `docs/design/runtime-orchestration-ssot.yaml` `ui_projection_render_reachability_contract.minimum_cardinality_completion_invariant_relationship` / `production_projection_connectivity_invariant.enforcement_boundary`
**Position:** `hub-relation-target-manifest-canonical-migration` Round34監査で発見・分離された、独立scopeの変更。SQL DDL変更は不要（既存guard/checkのロジック修正のみ）。**Round36訂正**: 本Bundle全体を`implementation_change`として一括ラベルしていたが、内容の一部（下記#3）は未だOwner判断が確定していないdesign上の未決事項であり、「backend実装は未着手」という記述はこの矛盾を覆い隠していた。正しい区分: #1（`DeprecateHubRelationAsync`のraw active-count guardをresolvable-count判定へ変更/追加）と#2（`UpdateHubRelationAsync`へのresolvability guard新設）はfail-close方針が既存パターン（resolvable-cardinality違反は拒否）と整合しており、具体的な実装手段の選択のみがAgent判断に委ねられた`implementation_change`として着手可能。#3（`NpgsqlManifestRepository.DeprecateAsync`が被参照Manifestをdeprecateする際の挙動——fail-close/警告/許容のいずれにするか）は、既存の管理者ワークフロー（正当なManifest整理作業）を壊しうる製品判断であり、Owner未決定のまま実装へ進めない`design_investigation`項目として本Bundleから明示的に分離する。#3のOwner決定が確定するまで、本Bundle全体を"implementation_change ready"と扱わないこと。

### 問題点

`hubs.hub_relations`のresolvable-cardinality（`minimum_cardinality_completion_invariant`が定義する「navigation orphanでない」状態）を崩す書き込み経路が3つ存在し、いずれも既存guardに検出されない:

1. **`NpgsqlContentBundleRepository.DeprecateHubRelationAsync`のraw active-count guard**: 既存regression guard（`HUB_RELATION_LAST_ACTIVE_FOR_MANIFEST`）はraw `status='active'`行数のみを見ており、resolvableかどうかは見ない。同一sourceに複数のactive行があり、そのうち1件だけがresolvableな場合、そのresolvable行をdeprecateしてもこのguardは通ってしまい、orphanを検出しないまま作り得る。
2. **`UpdateHubRelationAsync`のresolvability guard欠如**: resolvabilityを一切checkしないため、resolvableな行を無guardでunresolvableなHubへ書き換えられる。
3. **`NpgsqlManifestRepository.DeprecateAsync`のincoming-reference check欠如**: Manifestを直接deprecateする際、そのManifestを`target_topology_manifest_id`/`related_hub_id`で参照する他manifestのhub_relations行に対して一切checkを行わない。1つの被参照Manifestをdeprecateするだけで、無関係な複数のsource manifestのresolvable-cardinalityを同時に0へ落とし得る。

いずれも`hub-relation-target-manifest-canonical-migration`の2目的（Enum jump到達性、`target_topology_manifest_id`移行）とは独立した既存本番条件であり、そのBundleのmigration dispositionはこれらに依存しない。

### 目的

上記3経路をresolvability-awareにし、`minimum_cardinality_completion_invariant`が定義するorphan状態への意図しない遷移を防ぐ。

### 改善方針

**#1・#2（implementation_change着手可能）:**
- `DeprecateHubRelationAsync`のguardを、raw active-count判定からresolvable-count判定へ置き換える、または追加する。
- `UpdateHubRelationAsync`に、書き換え後のtarget/related_hub_idがresolvableかどうかを検証するguardを新設する。

**#3（design_investigation、Owner判断待ち——実装へ進まない）:**
- `NpgsqlManifestRepository.DeprecateAsync`に、deprecate対象Manifestを参照する他manifestのhub_relations行を検索するcheckを追加すること自体は自明だが、resolvable-cardinalityが0になるsourceが見つかった場合の挙動（(a) fail-close＝deprecate自体を拒否する、(b) 警告のみでdeprecateを許容する、(c) 無視して許容する）はOwnerの製品判断が必要——正当な管理者が別の被参照Manifestを整理する作業を(a)が意図せずブロックしうる一方、(b)/(c)はorphan発生を許容する。この判断が確定するまで#3は実装へ進まない。

3経路とも、新しいlifecycle状態や新しいparallel authorization/guard機構は作らず、既存のguardパターンを拡張する（この方針自体はOwner判断を要しない）。

### 対応資料

- `docs/design/db-schema.yaml` `hub_relations.target_reference_canonical_contract.deprecate_and_update_guard_resolvability_distinction`
- `docs/design/runtime-orchestration-ssot.yaml` `minimum_cardinality_completion_invariant_relationship` / `production_projection_connectivity_invariant.enforcement_boundary`

### 対象ファイル名/対象関数名

- `backend/repository/NpgsqlContentBundleRepository.cs`（`DeprecateHubRelationAsync`/`UpdateHubRelationAsync`）
- `backend/repository/NpgsqlManifestRepository.cs`（`DeprecateAsync`）

### 次段（implementation_change）の受入条件

- [ ] `DeprecateHubRelationAsync`が、resolvable行のdeprecateによってsourceのresolvable-cardinalityが0になるケースをguardする（既存`HUB_RELATION_LAST_ACTIVE_FOR_MANIFEST`のraw active-count判定をresolvable-count判定に置換または追加する）ことをtestで証明する。
- [ ] `UpdateHubRelationAsync`が、書き換え後の行がunresolvableになる更新を検知しguardすることをtestで証明する。
- [ ] **（design_investigation完了後のみ着手）** `NpgsqlManifestRepository.DeprecateAsync`が、deprecate対象Manifestを参照する他manifestのincoming hub_relations行を検索し、Owner確定済みの扱い（fail-close/警告/許容のいずれか、上記改善方針#3参照）を実施することをtestで証明する。Owner決定が未確定のままこの項目を実装しないこと。
- [ ] 3経路とも新しいlifecycle状態/parallel authorization機構を追加していないことを確認する。
- [ ] 本Bundleの完了判定はCI greenのみを根拠にしない。SSOT契約・実装・testの意味的整合を監査役が個別に確認したうえで判定する。

---

## Bundle `ui-pressure-component-event-log-consumer-gap`

**Status:** `not_started`
**Primary SSOT:** `docs/design/context-route-recommendation.yaml` `hub_local_recommendation_pressure_lanes.lanes.ui_pressure`（正本）/ `docs/design/context-route-recommendation.md` / `docs/design/runtime-orchestration-ssot.yaml` `selection_observation_forward_requirement`
**Position:** design_investigation / design_change。`hub-relation-target-manifest-canonical-migration` Round 27監査からの新規発見であり、Recommendation subsystem側の独立scope。同Bundleへは吸収しない。

### 問題点

`docs/design/context-route-recommendation.yaml hub_local_recommendation_pressure_lanes.lanes.ui_pressure`は`sources: [context_event, component_operation_event_log]`、`outputs: [next_operation, next_component, next_route_action]`をcanonical宣言しているが、repo-wide調査の結果、`component_operation_event_log`を実際に読むproduction consumer/aggregation mechanismが存在しない:

- `backend/repository/ContextRouteRepository.cs`/`backend/repository/NpgsqlContextRouteRepository.cs`には`AppendComponentOperationEventLogAsync`という write-only メソッドのみが存在し、対になる読み取り/集計メソッド（`GetTransitionStatsAsync`/`GetWindowedTransitionStatsAsync`が`context_transition_stats`に対して持つような）が存在しない。
- `backend/runtime/ContextRouteRecommendationResolver.cs BuildScoreRankAsync`が生成する`NextOperations`（`ui_pressure`の実際の出力）は`ResolveNextOperations(eligibility.Neighbors, eligibility.TransitionStats, policy)`という、`context_event`/`context_transition_stats`のみに由来するneighbor/transition-stats経由の計算であり、`component_operation_event_log`は一切参照しない。
- `backend/schema/ContextRouteContracts.cs RecommendNavigationProjectionSpec.FromRecommendation`が実際に生成する`ui_pressure` laneの`CandidateKind`は`next_operation`と`next_context_token`の2種のみであり、SSOTが`outputs`として宣言する`next_component`/`next_route_action`はどこでも生成されていない。
- `frontend/api/dispatch.ts`の型union、`docs/design/context-route-recommendation.yaml`/`.md`には`next_component`/`next_route_action`という語彙が存在するが、`frontend/tests/recommendNavigationIsland.test.ts`（`"RecommendationPanel labels nextTokens as context tokens, not component or route-action candidates"`）は逆に`next_component / next_route_action candidates`という文字列が`RecommendationPanel.tsx`に**存在しないこと**を明示的に検証しているテストであり、これら2 output kindが未実装であることの直接的な既存証跡になっている。
- `frontend/tests/recommendationPressureLaneGuard.test.ts`は`context-route-recommendation.yaml`をパースして`ui_pressure.sources`/`outputs`の宣言shapeのみを検証するテストであり、production consumerの存在を証明するものではない。

SSOT上のsource宣言とproduction実装の間にこのgapがあることは、`hub-relation-target-manifest-canonical-migration`のselection observation forward要件（navigation Islandからの`emitComponentOperationEvent`呼び出し）が実装された後も、その観測データが実際に`ui_pressure`のrecommendation出力へ反映されないことを意味する。

### 目的

`component_operation_event_log`と`ui_pressure`の`next_component`/`next_route_action` outputの間に必要なsource→aggregate→output mappingの設計が、既存の`context_event`/`context_transition_stats`パターンの延長で十分か、別の設計が必要かをOwner判断のために調査・整理する。本Bundle自体はこの調査結果を確定させることを目的とし、調査未了のままimplementation_changeへ進めない。

### 改善方針

- 既存mechanism（`ContextRouteRecommendationResolver`の`BuildCandidateSourceAsync`/`BuildEligibilityAsync`/`BuildScoreRankAsync`パイプライン、`NpgsqlContextRouteRepository`の`context_transition_stats`集計パターン）を repo-wide に再調査し、`component_operation_event_log`を折り込める既存拡張点があるかを先に確認する。既存mechanismのrepo-wide探索を経ずに新しいcounter table/aggregate table/endpoint/Store/recommendation lane/parallel authorityを設計しない。
- `next_operation`が`context_event`/`context_transition_stats`のみで既に生成されている一方、`next_component`/`next_route_action`が具体的に何を入力として何を出力すべきかがSSOT上でも未確定である可能性を検証する（`component_operation_event_log`のペイロード形状 — `ComponentOperationEventLogRecord` — が`next_component`/`next_route_action`を導出するに足る情報を持つか、`ComponentEventAppendEndpoint.cs`が受理するペイロード契約を含めて確認する）。
- `context_transition_stats`のような既存aggregate tableへ`component_operation_event_log`を根拠なく機械的に写像しない。`context_event`（append-only operation event log、各eventがoperation発生時点のtoken_idsスナップショットを記録する）/`context_transition_stats`（`prev_operation`→`next_operation`のoperation transition aggregate、`P(next_operation | prev_operation)`）と、`component_operation_event_log`（component/operation単位のUI操作ログ）のsemanticsが異なるため、単純な統合が正しいかはOwner判断が必要な設計論点として明示する。
- 既存consumerがrepo-wide探索で発見された場合（本調査時点では未発見）は、新規architectureを設計せずそれをreuseする。
- 調査の結果、設計authorityが不足していると判明した場合はimplementation_changeへ進めず、Owner判断が必要な具体的選択肢（例: 既存`context_transition_stats`集計を拡張してcomponent/operation次元を追加するか、別テーブル・別集計を設けるか）を比較根拠とともに報告する場に留める。

### 対応資料

- `docs/design/context-route-recommendation.yaml`（`hub_local_recommendation_pressure_lanes.lanes.ui_pressure`）
- `docs/design/context-route-recommendation.md`
- `docs/design/db-schema.yaml`（`component_operation_event_log`テーブル定義・role）
- `docs/design/runtime-orchestration-ssot.yaml`（`selection_observation_forward_requirement`、四層(A)/(B)/(C)/(D)分離の記述）

### 対象ファイル名

- `backend/runtime/ContextRouteRecommendationResolver.cs`（`BuildCandidateSourceAsync`/`BuildEligibilityAsync`/`BuildScoreRankAsync` — 既存`ui_pressure`計算パイプライン、`component_operation_event_log`を組み込む場合の拡張候補）
- `backend/repository/ContextRouteRepository.cs`（`AppendComponentOperationEventLogAsync` — 現状write-onlyな抽象基底、対になる読み取り/集計メソッドが存在しない）
- `backend/repository/NpgsqlContextRouteRepository.cs`（`AppendComponentOperationEventLogAsync`実装、`GetTransitionStatsAsync`/`GetWindowedTransitionStatsAsync` — `context_transition_stats`集計の既存参照実装パターン）
- `backend/schema/ContextRouteContracts.cs`（`RecommendNavigationProjectionSpec.FromRecommendation` — `next_operation`/`next_context_token`のみを生成し`next_component`/`next_route_action`を生成しない現行実装、`ComponentOperationEventLogRecord`のペイロード形状）
- `backend/schema/RecommendationPressureLanes.cs`（`UiPressure`定数）
- `backend/endpoint/ComponentEventAppendEndpoint.cs`（`component_operation_event_log`への書き込み経路、受理ペイロード契約）
- `frontend/api/dispatch.ts`（`next_component`/`next_route_action`型union宣言箇所）
- `frontend/components/RecommendationPanel.tsx`
- `frontend/tests/recommendNavigationIsland.test.ts`（`next_component`/`next_route_action`未実装を裏付ける既存test）
- `frontend/tests/recommendationPressureLaneGuard.test.ts`（SSOT shape guardのみ、consumer proofではないことに留意）
- `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs`
- `frontend/tests/recommendNavigationIsland.test.ts`（`RecommendNavigationProjectionSpecTests`相当のfrontend側proof）

### 対象関数名

- `ContextRouteRecommendationResolver.BuildScoreRankAsync`（`NextOperations`生成箇所、`component_operation_event_log`由来の入力が無い）
- `ContextRouteRecommendationResolver`内`ResolveNextOperations`（private、`context_event`/`context_transition_stats`のみ使用）
- `ContextRouteRepository.AppendComponentOperationEventLogAsync`（write-only、対称の読み取り/集計メソッドは repo-wide 探索の結果 現時点で存在しない — 新設が必要な場合の対象）
- `NpgsqlContextRouteRepository.GetTransitionStatsAsync`/`GetWindowedTransitionStatsAsync`（`context_transition_stats`集計の既存参照実装、拡張または並列設計の比較対象）
- `RecommendNavigationProjectionSpec.FromRecommendation`（`next_component`/`next_route_action`のCandidateKindが未生成であることの確定箇所）

### 受入条件（design_investigation / design_change の範囲）

- [ ] 既存mechanism（`ContextRouteRecommendationResolver`パイプライン、`NpgsqlContextRouteRepository`集計パターン）のrepo-wide再探索により、`component_operation_event_log`を読むconsumerが本当に存在しないことを再確認する（存在する場合はその具体的source/function/proofを記録し、本Bundleの問題点自体を訂正する）。
- [ ] `next_component`/`next_route_action`の入力（`component_operation_event_log`のどのcolumn/ペイロードから何を導出するか）と出力（`RecommendProjectionSection`のどのCandidateKind/表示semanticとして提示するか）のsource→aggregate→output mappingの設計選択肢を、既存`context_transition_stats`パターンとの異同を明示したうえでOwnerへ提示する。
- [ ] 上記mappingがOwnerに確認・確定されるまで、新しいcounter table/aggregate table/endpoint/Store/recommendation lane/parallel authorityを設計・実装しない。
- [ ] 本Bundleの成果物はdesign_investigation/design_change文書（SSOT訂正・比較根拠の記録）に留め、implementation_change（SQL DDL/backend/frontend実装）には進まない。
