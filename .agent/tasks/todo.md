# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `in_progress` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `helper-manual` | helper reference artifact / admin helper projection | not_started | 1 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `scheduler-job-manifest-admin-ui` | scheduler job manifest admin authoring / projection UI | partial | 1 | `product.scheduler_job_manifest_substrate` | `docs/design/scheduler-job-manifest-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 2 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |

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

## Bundle `scheduler-job-manifest-admin-ui`

**Status:** partial
**Roadmap/status SSOT:** `product.scheduler_job_manifest_substrate`
**Primary SSOT:** `docs/design/scheduler-job-manifest-ssot.yaml`
**Related SSOT:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`
**Carry-over source:** `.agent/tasks/roadmap-manual-acceptance-ui-todo.md` section 3, 2026-07-04 audit `implementation_changes_request`

目的:
scheduler job manifest を `admin.contents` の data-defined authoring / projection surface として扱い、cron / interval / manual-only / active / disable / status projection を人間が誤認せず作成・確認できる admin UI 導線を実装する。frontend は scheduler authority を持たず、backend `AdminRuntime` の `scheduler_jobs` create / edit / disable / list_settings authority に intent を渡すだけにする。

残問題:
- SSOT は `admin.contents` による scheduler job manifest の create / edit / disable と settings/status projection を要求しているが、frontend/API 側に `scheduler_jobs` authoring / projection helper や画面 surface が見つからない。
- backend `AdminRuntime.SchedulerSettings.cs` には `create` / `edit` / `disable` / `list_settings` があり、secret fail-close / table-column authority guard / read-only projection tests もあるが、admin UI から操作・確認できる導線が未実装。
- `cron` / `interval_seconds` / `manual_only`、`active` / disabled、`manualRunAllowed`、credential/external port reference、失敗/保留/未実行/無効化の表示が無いため、手動受入以前に UI 実装が必要。
- manual run 相当を実装する場合、通常 cron 実行と混同しない表現と backend authority boundary が必要。未定義なら manual run 実行は追加せず、`manualRunAllowed` の表示に留める。

改善方針:
implementation_change で、既存 admin dispatch lane を使って `scheduler_jobs` helpers と admin contents subpanel を追加する。専用 runtime / frontend direct DB write / scheduler-domain switch は追加しない。UI は scheduler job manifest の data-defined form と read-only settings/status projection に限定し、secret material を表示しない。create/edit/disable は backend `AdminRuntime` authority へ渡し、active job の in-place edit は disable-first 境界を表示する。

対応資料:
- `docs/design/scheduler-job-manifest-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`
- `.agent/tasks/roadmap-manual-acceptance-ui-todo.md`

対象ファイル名候補:
- `frontend/api/adminApi.ts`
- `frontend/islands/ContentsScreenDesignPanel.tsx`
- future `frontend/islands/SchedulerJobManifestPanel.tsx` or equivalent contents subpanel
- `backend/runtime/AdminRuntime.SchedulerSettings.cs` (existing authority; reference / contract alignment)
- `backend/tests/Topolactor.Runtime.Tests/AdminRuntimeSchedulerAuthoringTests.cs`
- future frontend/admin UX tests for scheduler job authoring/projection

対象関数名候補:
- future `listSchedulerJobSettings`
- future `createSchedulerJobManifestDraft`
- future `editSchedulerJobManifestDraft`
- future `disableSchedulerJobManifest`
- future `renderSchedulerJobManifestPanel`
- future `renderSchedulerJobSettingsProjection`
- future `renderSchedulerJobRunStatusSummary`
- existing `DataListSchedulerJobsSettingsAsync`
- existing `DataCreateSchedulerJobAsync`
- existing `DataEditSchedulerJobAsync`
- existing `DataDisableSchedulerJobAsync`

残受入条件:
- [ ] `frontend/api/adminApi.ts` に `scheduler_jobs` list_settings / create / edit / disable helper があり、既存 admin dispatch lane を通る。
- [ ] `/admin/contents` または admin contents subpanel から scheduler job manifest を作成・編集・disable できる。
- [ ] UI は `cron` / `interval_seconds` / `manual_only`、`active`、`manualRunAllowed`、credentialRequirementRef / externalPortRef を secret無しで表示する。
- [ ] active job の in-place edit が禁止または disable-first として明示される。
- [ ] settings/status projection が read-only で、payload由来 table/column authority や credential plaintext を表示しない。
- [ ] 失敗 / 保留 / 未実行 / 無効化 / 成功が同じ表示に潰れない。
- [ ] reload / 再訪問後に設定済み cron / interval / manual-only の状態を追える。
- [ ] disable / edit / manual run 表示が通常 cron 実行と混同されない。
- [ ] frontend guard test が、scheduler UI helper が `/api/dispatch` / admin dispatch lane を通り direct DB write しないことを確認している。
- [ ] backend `AdminRuntimeSchedulerAuthoringTests` の authority / secret fail-close / projection guard を崩していない。

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する
- [ ] `product.admin_topology_authoring` の `/admin/contents` Step 1 3 entry mode → clone draft → edit → backend replacement merge を、統合UX手動受入 / hand-debug evidence として確認する
