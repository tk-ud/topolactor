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


## todo_roadmap_finalization_gate
- PR Approve requires TODO/Roadmap Finalization Judgment.
- When TODO/Roadmap Finalization Judgment is executed, auditor must read `.agent/protocols/todo-carry-over.md` and apply its carry-over/closure gate before approval judgment.
- If implementation meaning satisfies or changes any TODO / roadmap `implementation_registry` entry, auditor must either:
  1. update canonical TODO/roadmap in the same audit/follow-up maintenance task, or
  2. if canonical TODO/roadmap cannot be updated in the same task, emit a single explicit follow-up prompt for `todo_maintenance` as a blocked-state output obligation (not as an approval-unblock condition).
- Approve is blocked when roadmap/TODO status remains materially stale.
- When same-task canonical update is not possible, auditor must hold approval until stale status is resolved or explicitly reclassified as out-of-scope, after emitting the required follow-up prompt.
- Audit is semantic consistency judgment for implementation meaning and canonical progress state; it is not self-approval for implementation completion.
- Remote CI unavailable to implementation agent is not a TODO item; it is Auditor evidence input for final closure.


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
- Required alignment surfaces explicitly cross-checked.
- Semantic findings grounded in diff + implementation reality.
- `implemented` 判定時、roadmap/TODO/SSOT completion_condition 充足を明示できる。
- SSOT未達が残る場合、`known_gap_ref` と `remaining_todo` に未達条件が明示される。
