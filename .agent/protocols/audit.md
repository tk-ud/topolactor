# audit protocol

## workflow_guard
Use in JUDGMENT for worktype `audit`.

## trigger_condition
Semantic PR/diff audit, merge judgment, or summary-truth verification requested.

## required_alignment_surfaces
- top-level semantic baseline SSOT (audit mandatory):
  - docs/framework-core.yaml
  - docs/framework-policy.yaml
  - docs/design/runtime-orchestration-ssot.yaml
  - docs/design/pipeline-continuity-ssot.yaml
- PR diff or patch
- changed file list
- .agent/tasks/todo.md
- docs/system-roadmap.yaml
- roadmap target milestone/unlocks and implementation_registry entries
- diff-target implementation files
- main-vs-target diff reality (or target PR head state)
- README/public docs only when needed for externally claimed behavior verification

## judgment_scope
Implementation meaning consistency against stated intent and roadmap/todo status.


## Gate 0: Architecture substrate and reusable abstraction conformance

Before implementation completeness judgment, the auditor must classify every changed surface by architecture substrate:

- `hardcoded runtime substrate`
- `seed-defined entity / projection / action / UI surface`
- `data-defined runtime/admin mapping`
- `runtime/admin data`
- `secret or external authority boundary`

### hardcode allowed / seed-data-defined required boundary

Use `docs/design/runtime-orchestration-ssot.yaml` boundaries as the judgment source. Hardcode is allowed only for:

- runtime port
- runtime handler
- runtime skeleton
- scheduler / dispatcher skeleton
- endpoint shape
- abstract function shape, when explicitly registered in SSOT

Seed/data-defined representation is required for:

- UI schema
- form/table projection
- action buttons
- action wiring
- dispatch payload mapping
- admin surface registration
- entity operation binding
- projection constructor mapping
- function parameters
- runtime mapping where SSOT treats mapping as data-defined

### reusable abstraction first rule

Before accepting a new route, island, frontend API wrapper, action handler, helper, repository method, audit writer, validation flow, or status transition flow, the auditor must check whether existing reusable substrate can express the behavior.

Reusable substrate must be preferred. If existing substrate is insufficient, the implementation must add a reusable abstraction suitable for future bundles rather than a narrow one-off implementation, unless an explicit SSOT exception exists.

For each new route / island / frontend API / action handler / helper addition, the auditor must ask:

- Can this be expressed through existing seed/entity/projection/action substrate?
- Can this use the existing dispatch -> entity -> runtime circuit?
- Can this use existing repository / audit / validation / status transition abstractions?
- If a new abstraction is necessary, is it reusable by future bundles rather than narrow one-off code?

Implementation-first shape must not pass by adding matching SSOT text after the fact. The auditor must classify each SSOT update as either `design-conformant` or `deviation-ratification`, and `deviation-ratification` cannot be used as an approval basis unless the SSOT explicitly creates an exception and explains why the substrate route is impossible.

Gate 0 is blocking: a completeness judgment is invalid when this classification is omitted or when a dedicated implementation bypass is accepted without the checks above.

### completion_gate_judgment (triggered when projection / admin / external integration surfaces touched)

When a PR or diff touches any of the following surfaces, Gate 0 must additionally include explicit completion gate judgment. All applicable gate axes must be `pass` before `implemented` judgment is valid. An axis that is `partial` or `fail` blocks `implemented` judgment for that gate.

**Trigger: `data_driven_projection_completion_gate`** (SSOT: `docs/design/runtime-orchestration-ssot.yaml`)
- Triggered surfaces: DB-driven UI projection / frontend emission / component rendering / dispatch resolution / ManifestDispatcher / api_command_lane / response-SSE lane / projection response binding / backend mutation (abstract function, topology_function_binder, repository write) / seed or data-defined UI/action/mapping
- Axes: `ui_db_projection`, `dispatch_resolution`, `response_or_sse_queue_projection`, `abstract_function_or_db_driven_operation_boundary`, `seed_or_data_defined_surface`
- Each axis is `pass` only when the SSOT condition is fully satisfied, not merely when a surface exists

**Trigger: `admin_authoring_completion_gate`** (SSOT: `docs/design/admin-console-workflow-ssot.yaml`)
- Triggered surfaces: admin authoring pipeline (/admin/contents steps 1–3) / UI Builder wiring (PackageWiringEditor) / dispatch kind admin configuration (screen_operation_kinds) / UI Events runtimeInteractions trigger+targetNodeId / external integration portTargetRef wiring
- Axes: `all_dispatch_kinds_configurable`, `contents_dispatch_ui_wiring_configurable`, `ui_events_trigger_and_target_configurable`, `external_integration_uses_credential_substrate`, `external_integration_trigger_and_target_wiring_configurable`
- Note: `trigger UI` and `target UI` are independent concepts; satisfying one does not satisfy the other

**Trigger: `external_integration_completion_gate`** (SSOT: `docs/design/external-port-substrate-ssot.yaml`)
- Triggered surfaces: external port bundle consumer / portTargetRef action wiring / credential substrate / port record context / policy steps execution
- Extends `data_driven_projection_completion_gate`: all axes of both gates must be `pass`
- Additional axis: `credential_resolution_base`

Completion gate judgment must be explicit per axis: `pass` / `partial` / `fail` + evidence. Omitting gate judgment when the surface is triggered is a blocking condition identical to omitting Gate 0 substrate classification. `partial` gate axis is allowed only when PR scope is explicitly scoped-progress (non-closing); closing PRs must satisfy all gate axes.

## evidence_based_classification

For each implementation/status claim, auditor must classify by evidence only.

### Evidence sufficient path

1. Read related SSOT (`docs/design/*` for the touched surface).
2. Read related implementation files.
3. Read related tests.
4. Emit the status supported by the evidence:
   - `implemented`: SSOT completion_condition met; code path exists and is not skeleton/placeholder; tests cover the behavior.
   - `partial`: some completion_conditions met; code path exists but known gaps remain.
   - `skeleton`: boundary or adapter exists; runtime behavior is pass-through or placeholder.
   - `not_started`: no implementation surface exists yet.
5. Do not suppress `implemented` merely to avoid overclaim. If SSOT + code + tests support `implemented`, emit `implemented`.

### Evidence insufficient path

If SSOT, implementation files, or tests for the claimed surface have NOT been read:
- Emit `Repo implementation checked: no`.
- Emit `Merge judgment: invalid audit / blocking`.
- List the missing evidence (which SSOT / files / tests were not read).
- Do not guess `partial` or `implemented` without reading the evidence.

### roadmap_todo_drift

If implementation reality (read from SSOT + code + tests) proves `implemented` but roadmap/TODO claims `partial`, `skeleton`, or `not_started`:
- Report `roadmap_todo_drift: yes`.
- Stale underclaim is a false status claim, not a safe conservative default.
- Auditor must request or perform roadmap/TODO update to match the evidence.
- Holding the stale roadmap/TODO status without update is a blocking condition.

### Anti-overclaim / anti-underclaim symmetry

Both directions are invalid audit behavior:
- **Overclaim**: claiming `implemented` without reading SSOT + code + tests.
- **Underclaim**: claiming `partial` or `skeleton` despite `implemented` evidence from SSOT + code + tests.
- Holding a conservative status because "implemented might be exaggerated" without reading code and tests is invalid and is treated as `Repo implementation checked: no`.

## foundation_ssot_read_gate
For worktype `audit`, read top-level semantic baseline SSOT first (mandatory), in this order:

1. `docs/framework-core.yaml`
2. `docs/framework-policy.yaml`
3. `docs/design/runtime-orchestration-ssot.yaml`
4. `docs/design/pipeline-continuity-ssot.yaml`
5. target-specific SSOT / DB / implementation files (via `.agent/docs/ssot-map.yaml` as discovery aid)

`全部読むな` は維持するが、これは `.agent/docs` 全読みに対する制約であり、audit baseline 4SSOT の省略を許可しない。

## approve_judgment_axis
- Approve requires semantic consistency between PR diff, TODO, roadmap, and relevant SSOT completion_condition classification.
- audit 判定基準は常に implemented 到達基準に揃える。partial 状態そのものは禁止しないが、implemented 未達のまま無条件 Approve は禁止する。
- implemented 未達時は、implemented 到達可能な TODO 単位への細分化、または canonical TODO への carry-over 指示（remaining scope / next TODO）を Approve 前に必須とする。
  - ここでの「TODO 単位への細分化」は implementation atom 分割を意味せず、roadmap entry（docs/system-roadmap.yaml）を正本とした completion bundle 単位への再編を意味する。
- implemented 未達 + TODO細分化なし + carry-over 指示なし + Approve は禁止（Request Changes）。
  - TODO細分化は roadmap completion bundle 化を指し、implementation atom の小TODO分割を指さない。
- 親 Issue / TODO が大きすぎる場合、対象を implemented 到達可能な小TODOへ分割し、Approve 根拠は今回PR対象の細分化TODO単位 completion_condition 充足に限定する。
  - 小TODO分割とは implementation atom ではなく、roadmap `completion_condition` / `known_gap_ref` を閉じる completion bundle への再編を意味する。
- 「未達が残っているが partial として整合」は Approve 理由にしない。
- 「未達が残っているが、残TODOが roadmap completion bundle として canonical に明示されている」場合のみ carry-over 整合として扱う。
- partial Approve は、PR purpose / Issue目的 / user依頼が明示的に partial / scoped progress / non-closing progress の場合に限定して許可する。
- 上記 partial purpose に該当する場合のみ、未達SSOT条件が TODO / roadmap / `known_gap_ref` / `remaining_todo` に明示維持されていることを Approve 条件として扱える。
- PR本文・Issue目的・user依頼・TODO項目のいずれかが implemented / close / completion / TODO `[x]` を目指す場合、`completion_condition` 未達、remaining `known_gap_ref`、concrete `remaining_todo` が1つでもあれば Request Changes とする。
- TODO/roadmap に未達が明示されている事実は partial 分類の正しさの証拠であり、implemented-target PR の Approve 根拠にはならない。
- Issue は入口・作業チケットであり、closed / aggregated / not_planned であっても implemented 判定根拠にしない。implemented 判定の正本は SSOT（`docs/design/*` 意味契約）・実装ファイル・テストとする。ロードマップの `completion_condition` / `known_gap_ref` は判定参照として使うが、ロードマップの status 記述のみを implemented 根拠にしない。ロードマップとTODOは動的な進捗参照面であり、実装実態の権威ソースではない。
- 既存の「TODO細分化」「小TODOへ分割」という語は、implementation atom 分割ではなく roadmap completion bundle への再編を意味する。
- relevant SSOT completion_condition が未達のまま implemented / complete / closed を示す、または示唆する PR は Approve 禁止。
- representative route、ACK-only intake、skeleton wiring、partial wiring は、SSOT completion_condition が許容しない限り implemented 根拠にしない。
- Remote CI / tests passing は証拠の一部であり、単体では semantic completion 根拠にしない。

## required_output_contract
- Diff reviewed: yes/no
- Changed files
- Todo checked: yes/no
- Roadmap checked: yes/no
- Implementation registry checked: yes/no
- Repo implementation checked: yes/no (yes は実際に読んだ実装ファイル・テストのリストを必須とする; ファイル・テスト読取なしの yes は無効 → Merge judgment: invalid audit / blocking)
- Architecture substrate judgment:
  - runtime port hardcode: pass/partial/fail + evidence
  - UI surface: pass/partial/fail + evidence
  - action wiring: pass/partial/fail + evidence
  - dispatch/entity circuit: pass/partial/fail + evidence
  - reusable abstraction usage: pass/partial/fail + evidence
  - new route/island/frontend API necessity: pass/partial/fail + evidence
  - SSOT update classification: pass/partial/fail + `design-conformant` or `deviation-ratification`
  - data_driven_projection_gate_judgment (when projection/dispatch/SSE/mutation surfaces touched):
    - ui_db_projection: pass/partial/fail + evidence
    - dispatch_resolution: pass/partial/fail + evidence
    - response_or_sse_queue_projection: pass/partial/fail + evidence
    - abstract_function_or_db_driven_operation_boundary: pass/partial/fail + evidence
    - seed_or_data_defined_surface: pass/partial/fail + evidence
  - admin_authoring_completion_gate_judgment (when admin/Contents/UIEvents/wiring surfaces touched):
    - all_dispatch_kinds_configurable: pass/partial/fail + evidence
    - contents_dispatch_ui_wiring_configurable: pass/partial/fail + evidence
    - ui_events_trigger_and_target_configurable: pass/partial/fail + evidence
    - external_integration_uses_credential_substrate: pass/partial/fail + evidence
    - external_integration_trigger_and_target_wiring_configurable: pass/partial/fail + evidence
  - external_integration_completion_gate_judgment (when external port bundle surfaces touched):
    - UI_db_projection: pass/partial/fail + evidence
    - dispatch_resolution: pass/partial/fail + evidence
    - credential_resolution_base: pass/partial/fail + evidence
    - response_queue: pass/partial/fail + evidence
    - abstract_function_boundary: pass/partial/fail + evidence
- problem
- purpose
- improvement_policy
- reference_materials
- target_files
- target_functions
- todo
- remaining_todo
- Semantic findings
- Required follow-up
- Merge judgment
- todo_granularity_judgment
- top_level_ssot_checked

## forbidden_shortcuts
- Summaryだけで判断しない
- PR metadata / mergeability だけで判断しない
- ファイル存在だけで partial / implemented 判定しない
- todo未実装scopeを見ずに roadmap status を判断しない
- implementation_registry key 名だけで実装意味を判断しない
- ロードマップの status 記述または implementation_registry エントリ名・ファイル存在のみによる実装意味判断
- completion_condition 未達のまま implemented 判定しない
- representative route / skeleton / ACK-only / partial wiring を implemented 根拠にしない
- SSOT + コード + テストを読まずに実装実態が implemented かもしれないという保守的理由で partial/skeleton を保持しない（underclaim）
- "overclaim を避けるため" という理由のみで、証拠が supported する implemented を抑制しない
- 新規 dedicated route を、SSOT route registry へ追加するだけで合格扱いしない
- 新規 dedicated island を、UIが存在するという理由だけで合格扱いしない
- 新規 frontend API wrapper を、backend runtimeに到達できるという理由だけで合格扱いしない
- dispatch -> entity -> runtime の既存回路を迂回する実装を、SSOT未登録だけの問題として扱わない
- 既存抽象・共通関数・共通基板で表現できる処理を、専用処理として認めない
- 実装を正本としてSSOTを後追いさせない


## todo_roadmap_finalization_gate
- PR Approve requires TODO/Roadmap Finalization Judgment.
- When TODO/Roadmap Finalization Judgment is executed, auditor must read `.agent/protocols/todo-carry-over.md` and apply its carry-over/closure gate before approval judgment.
- If implementation meaning satisfies or changes any TODO / roadmap `implementation_registry` entry, auditor must either:
  1. update canonical TODO/roadmap in the same audit/follow-up maintenance task, or
  2. if canonical TODO/roadmap cannot be updated in the same task, emit a single explicit follow-up prompt for `todo_maintenance` as a blocked-state output obligation (not as an approval-unblock condition).
- Approve is blocked when roadmap/TODO status remains materially stale.
- `roadmap_todo_drift`: when SSOT + code + tests prove a component is `implemented` but roadmap/TODO records it as `partial`/`skeleton`/`not_started`, the stale entry is a false status claim and must be updated. Holding stale underclaim without update is a blocking condition identical to holding stale overclaim.
- When same-task canonical update is not possible, auditor must hold approval until stale status is resolved or explicitly reclassified as out-of-scope, after emitting the required follow-up prompt.
- Audit is semantic consistency judgment for implementation meaning and canonical progress state; it is not self-approval for implementation completion.
- Remote CI unavailable to implementation agent is not a TODO item; it is Auditor evidence input for final closure.


## security_authority_visibility_promotion_boundary_gate

Security / authority / visibility / promotion boundary surfaces are **not eligible for unconditional partial Approve** by placement in `known_gap_ref` alone.

These boundary surfaces require a closed **completion bundle** before Approve:
- draft authority — who can author drafts and when
- promotion authority — who can execute canonical promotion (package_generator:promote, layout_patch:apply)
- manifest activation authority — who can activate manifests
- CI Attention refresh authority — who can trigger CI Attention fragment refresh
- audit / evidence visibility authority — who can view audit logs and evidence (auditability ≠ unrestricted visibility)
- frontend display-only boundary — frontend holds no promotion judgment or authority
- backend / runtime promotion guard authority — runtime is the final and exclusive promotion authority

### partial_approve_exclusion_for_authority_boundary

The following cannot unlock Approve by `known_gap_ref` placement alone:
- promotion gate authority ownership when the backend-vs-frontend boundary is ambiguous or undeclared
- visibility boundary between display-only surfaces and authority surfaces
- draft / promote / apply authority separation when any of the three is missing
- manifest activation authority and timing (who, when, under which conditions)
- audit evidence access scope (transparency as auditability has a defined boundary; unrestricted visibility is not auditability)
- frontend authority / promotion judgment prohibition when this is not explicitly declared in the roadmap

**Approve is blocked** when any of these boundary surfaces remain undefined or remain in `known_gap_ref` without an explicit completion bundle referenced in the roadmap (`design.authority_visibility_promotion_policy_ssot` or equivalent implemented-and-closed entry).

### completion_bundle_unit_for_authority_visibility_promotion

Authority / Visibility / Promotion Policy must be closed as a **unit**. The following splits are not valid completion bundles:
- draft authority only (missing promotion + manifest + visibility)
- promotion gate implementation only (missing draft + manifest + audit/evidence visibility + frontend boundary declaration)
- frontend display boundary declaration only (missing backend guard authority + draft + promotion)

Each subset is incomplete without the others; partial coverage does not constitute a valid completion bundle.

### audit_responsibility_for_authority_boundary

Auditor must treat the following as **blocking** (not non-blocker carry-over):
- security / authority / visibility / promotion boundaries in `known_gap_ref` without a roadmap completion bundle reference
- partial promotion gate or visibility implementation presented as "progress" without a roadmap completion bundle
- CI Attention fragment being interpreted as frontend authority or system lock authority
- draft editing blocked or gated by fragment state or authority gate
- promotion judgment not exclusively owned by backend runtime boundary guard
- audit evidence access without explicit auditability boundary (visibility ≠ unrestricted access)

## repository_inspection_gate

When the audit scope includes repository-wide inspection (repo実態確認 / roadmap-todo整合確認 / design gap discovery), apply the following gate before emitting TODO or follow-up items.

### inspection_prerequisite

- Read SSOT docs and implementation files to determine current state. Do NOT rely on roadmap or todo alone.
- Determine whether the problem diagnosis is firm before converting to a TODO.
- If the root cause is unclear or multiple hypotheses exist, emit a follow-up prompt or investigation item rather than a TODO.

### bundle_unit_todo_gate

Before adding a TODO to `.agent/tasks/todo.md`, all of the following must be present:

```
- 問題点: (concrete, not speculative)
- 目的: (what the fix achieves)
- 改善方針: (approach, not implementation atom)
- 対応資料: (SSOT / docs references)
- 対象ファイル名: (specific file paths)
- 対象関数名: (specific function or runtime boundary names)
```

TODOs must be completion bundle units, not implementation atoms. A TODO that covers a single alias addition, adapter connection, single test case, or single emit call is an implementation atom and must NOT be added as a canonical TODO. Bundle the full surface or carry over as a follow-up prompt.

### projection_admin_external_gate_inspection

When inspecting repo reality for projection / admin / external integration surfaces:

1. Check `data_driven_projection_completion_gate` axes against actual implementation files. If any axis is `partial` or `fail`, record the finding as a bundle-unit TODO only if the root cause is confirmed.
2. Check `admin_authoring_completion_gate` axes for admin surfaces. Ambiguous findings (e.g., "dispatch may be missing") must be investigated before adding a TODO.
3. Check `external_integration_completion_gate` axes for external port consumers. Confirmed gaps become bundle-unit TODOs referencing the applicable consumer bundle SSOT.

Do NOT add a TODO until the SSOT says what "done" means and the implementation gap is confirmed. Speculative TODOs based on roadmap status alone are prohibited (roadmap and todo are not the authoritative source of implementation state).

## non_blocker_carry_over_rule
- SSOT completion_condition と実装意味整合が成立している PR は、軽微な cleanup / coverage expansion / future integration test が残っていても Approve 可能。
- Approve可能な非ブロッカー残件は、PR summary/comment だけで閉じず `.agent/tasks/todo.md` の `Non-blocking cleanup / hardening carry-over` ブロックへ carry-over する。
- 非ブロッカーTODOには分類タグを付ける（例: `coverage`, `hardening`, `cleanup`, `integration-test`, `surface-expansion`）。
- 次の項目は非ブロッカー扱い禁止（Approve禁止条件）: SSOT completion_condition 未達、implemented 誤判定、required_identity 欠落、roadmap/TODO の material stale。

## blocking_conditions
- Missing required audit output fields.
- Replacing semantic audit with structure-only result.
- Required alignment surfaces not checked.

## pass_conditions
- Required output contract produced.
- Required output contract includes semantic audit fields:
  - todo_granularity_judgment
  - problem
  - purpose
  - improvement_policy
  - reference_materials
  - target_files
  - target_functions
  - todo
  - remaining_todo
  - Architecture substrate judgment
- Required alignment surfaces explicitly cross-checked.
- Semantic findings grounded in diff + implementation reality.
- `implemented` 判定時、roadmap/TODO/SSOT completion_condition 充足を明示できる。
- SSOT未達が残る場合、`known_gap_ref` と `remaining_todo` に未達条件が明示される。
