# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `helper-manual` | helper reference artifact / admin helper projection | not_started | 1 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `admin-topology-clone-draft-lifecycle` | admin topology clone / draft lifecycle | not_started | 1 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (email / audit_approval) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。UI Builder form preset は docs/design/ui-builder-preset-ecosystem-ssot.yaml / db/physical_search_crud_aggregate_preset_seed.sql の CRUD preset seed の写像/派生であり、新規 UI runtime / 専用 component 実装ではない。

---

## Bundle `admin-topology-clone-draft-lifecycle`

**Status:** not_started
**Roadmap bundle:** `product.admin_topology_authoring`
**Primary SSOT:** `docs/design/admin-console-workflow-ssot.yaml`
**Supporting SSOT:**
- `docs/registrar-admin-ui-specification.md`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/ci-contract-ssot.yaml`

目的:
/admin/contents Step 1 に、新規作成 / active 正本から正本置き換え用 clone 下書き作成 / active 正本から別トポロジ登録用 clone 下書き作成を実装し、正本置き換え merge は backend authority で source evidence・validation・diff/log・conflict check を満たす場合だけ成立させる。/admin/manifests の active-to-draft / reopen draft 操作は runtime dispatch policy が確定するまで実装可能扱いにしない。

残問題:
- Step 1 entry mode selector と source active read-only selection が未実装。
- human-authored replacement clone draft と clone-as-new topology draft の draft_origin / clone_mode / source evidence が未保存・未表示。draft / active は toggle state であり、active兼draft・draft兼active・同一rowの重ね合わせ状態は禁止する必要がある。
- clone は enum/status 追加ではなく JSONB metadata / attribute / operation context として扱い、clone on の source_active_manifest_id UUID 必須、clone off の null/absent、source UUID 整合 fail-close が未実装。
- replacement clone merge の backend validation、stale source active fail-close、active identity conflict check、diff/log evidence gate が未実装。merge は existing active row UPDATE + working draft row DELETE であり、draft row active 化や draft row 自体の audit evidence 永続保持は禁止。
- source_active_manifest_id や lineage evidence だけで replacement authority を得ない guard が未実装。
- SQL Attention candidate と human clone draft の origin/candidate_source 境界が未検証で、candidate が自動 merge authority を得ない regression guard が無い。SQL Attention candidate は draft row を量産せず、candidate/evidence surface に保持し、明示採用時のみ draft 化する必要がある。
- /admin/manifests active-to-draft / reopen-active-as-draft は runtime dispatch impact policy が design gap のため未実装に留める必要がある。safe default は active を dispatchable のまま維持して draft copy を INSERT する方針。

改善方針:
implementation_change で Primary SSOT に従い、frontend は entry intent / source evidence 表示 / blocker 表示だけを担当し、merge target・conflict 判定・active mutation は backend AdminRuntime / ManifestRepository transaction に限定する。新規作成と clone-as-new topology は従来の registration/promote path を維持し、replacement clone のみ backend replacement merge guard を通す。draft / active は toggle state とし、clone は enum/status 追加ではなく JSONB metadata / attribute / operation context として扱う。replacement merge は source UUID 整合を fail-close で確認し、existing active row UPDATE + working draft row DELETE で完了する。SQL Attention candidate は candidate/evidence surface に保持し、draft row を量産せず、明示採用時だけ draft 化し、manual_clone_replacement と混同しない。active-to-draft 操作は safe default として active を維持した draft copy INSERT を採用し、direct active→draft は runtime dispatch policy が SSOT 確定するまで UI 実装しない。

対応資料:
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/registrar-admin-ui-specification.md`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/ci-contract-ssot.yaml`

対象ファイル名候補:
- `frontend/islands/ContentsAdmin.tsx`
- `frontend/islands/ContentsScreenDesignPanel.tsx`
- `frontend/islands/UiBuilderAdmin.tsx`
- `frontend/api/adminApi.ts`
- `frontend/content/adminUxTerms.ts`
- `frontend/tests/adminUxGuard.test.ts`
- `backend/runtime/AdminRuntime.cs`
- `backend/repository/ManifestRepository.cs`
- `backend/repository/NpgsqlManifestRepository.cs`
- `backend/tests/Topolactor.Runtime.Tests/InMemoryManifestAdminRepository.cs`
- backend tests for clone draft / replacement merge / stale source conflict
- DB migration only if draft_origin / clone_mode / source evidence schema is required by implementation design

対象関数名候補:
- `manifest:create_new_topology_draft`
- `manifest:create_clone_replacement_draft_from_active`
- `manifest:create_clone_new_topology_draft_from_active`
- `manifest:validate_clone_replacement_draft`
- `manifest:merge_clone_replacement_draft_to_active`
- `manifest:reopen_active_as_draft` or equivalent, only after runtime dispatch policy is fixed
- `CreateCloneReplacementDraftFromActiveAsync`
- `CreateCloneNewTopologyDraftFromActiveAsync`
- `MergeCloneReplacementDraftToActiveAsync`
- `LoadCloneSourceEvidenceAsync`
- `CountActiveIdentityConflictsAsync`

残受入条件:
- [ ] /admin/contents Step 1 entry modes are SSOT-defined and visibly separated.
- [ ] Replacement clone and clone-as-new topology cannot be confused.
- [ ] Replacement merge requires source evidence, validation, diff/log evidence, and backend conflict check.
- [ ] Stale source active fails close.
- [ ] Frontend has no merge authority.
- [ ] UI Builder layout/package authoring remains draft-scoped until canonical boundary.
- [ ] `layout_patch:apply` is not treated as production manifest replacement merge.
- [ ] SQL Attention candidate drafts cannot auto-merge to active.
- [ ] /admin/manifests active-to-draft policy has explicit runtime dispatch semantics or remains disabled as unresolved design gap.

---

## Bundle `helper-manual`

**Status:** not_started
**Roadmap/status SSOT:** `product.helper_manual_policy`
**Primary SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

目的:
helper/manual をユーザー向け文章方針だけでなく、人間 / Agent / MCP / External AI / Local LLM / admin UI が共通参照する helper reference artifact として実装可能にする。現行MCPの import-candidate / draft_operation / commit_candidate lane に topology authoring draft を載せる参照点を作り、admin では同じ内容を viewer として表示する。

残問題:
- `helper_reference_artifact` の schema / seed がまだ無い。
- MCPで topology authoring draft を構築する際の `structured_output_payload` / `assigned_business_object_candidate` / `assignment_target_scope` / `preview_diff` / `unresolved_fields` の具体例が未実装。
- admin 共通ヘッダから開く helper viewer が未実装。
- helper viewer が admin submit / apply / promote / approval を実行しない projection-only surface であることを実装上確認する guard が無い。
- AI/MCP由来 candidate evidence と人間の admin 手作業 draft の origin を混同しない表示・保存・監査境界が未検証。

改善方針:
implementation_change で、SSOTに従って helper schema / seed artifact を追加し、admin common header から Drawer helper viewer を開けるようにする。viewer は検索・カテゴリ選択・tree viewer・detail modal mount までに限定し、runtime/admin/MCP authority を持たせない。MCP新規tool surfaceは作らず、既存 import-candidate lane の payload reference として実装する。

対応資料:
- `docs/design/user-facing-helper-manual-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`
- `docs/design/cli-mcp-port-implementation-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
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

対象関数名候補:
- future `loadHelperManualSeed`
- future `filterHelperManualItems`
- future `buildHelperManualTree`
- future `renderHelperManualDetail`
- future `openAdminHelperDrawer`
- future `mountDetailHelperModal`

残受入条件:
- [ ] helper schema / seed artifact が追加され、SSOTの required fields を満たしている。
- [ ] helper seed に admin authoring flow / MCP topology authoring draft / UI Builder / CI Attention / approval boundary のカテゴリがある。
- [ ] internal vocabulary と user-facing vocabulary の対応が helper artifact に定義されている。
- [ ] MCP topology authoring draft の payload example が、既存 import-candidate lane の field に対応している。
- [ ] admin common header から helper Drawer を開け、検索・カテゴリ選択・tree viewer・detail modal が使える。
- [ ] helper viewer は admin submit / apply / promote / approval / MCP operation を実行しない。
- [ ] AI/MCP由来 candidate evidence と human manual admin draft の origin を混同しない表示・監査境界が確認できる。
- [ ] 新規 MCP tool surface / admin submit direct execution / active topology mutation は追加していない。

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する

---
