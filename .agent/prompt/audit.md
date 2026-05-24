# audit prompt router

## purpose
PR/diff semantic audit and merge-readiness judgment with implementation-meaning alignment.
Approve判断は「完全実装済みか」ではなく、PR scope の実装意味と SSOT状態分類（implemented/partial/remaining gaps）が整合しているかで行う。

## trigger_condition
Worktype is `audit`, including any of:
- PR監査
- 差分監査
- merge可否判断
- roadmap/todo/repo実態の整合確認
- Summary の真偽確認
- 「差分見て」「リポジトリ進捗見て」「マージしていいか」系依頼

## required_reads
- PR diff or patch
- changed file list
- .agent/tasks/todo.md
- docs/system-roadmap.yaml
- roadmap target milestone/unlocks and related implementation_registry entry
- diff-target implementation files
- .agent/protocols/audit.md

## optional_reads
- README / docs/articles / design SSOT when diff claim validation requires public/docs context
- .agent/docs/ssot-map.yaml when PR diff touches runtime/db/frontend/governance surfaces and SSOT selection is needed
- .agent/docs/required-paths.yaml only when auditing `.agent` structure, required paths, required content terms, or check-structure expectations

## protocol_triggers
- always: .agent/protocols/audit.md
- conditional: .agent/protocols/todo-carry-over.md when TODO/Roadmap Finalization Judgment updates, closes, reclassifies, or carries over canonical TODO/roadmap state
- conditional: policy/scenario/runtime protocols only when touched

## completion_judgment_axis
- `implemented` 判定は roadmap/TODO/SSOT に定義された completion_condition を満たす場合のみ許可する。
- audit 判定基準は常に implemented 到達基準に揃える。partial 状態そのものは禁止しない。
- implemented 未達時は、implemented 到達可能な TODO 単位への細分化、または canonical TODO への carry-over 指示（remaining scope / next TODO）を必須とする。
- implemented 未達 + TODO細分化なし + carry-over 指示なし + Approve は禁止（Request Changes）。
- 親 Issue / TODO が大きすぎる場合は、implemented 到達可能な小TODOへ分割し、Approve 根拠は今回PR対象の細分化TODO単位 completion_condition 充足に限定する。
- 「未達が残っているが partial として整合」は Approve 理由にしない。
- 「未達が残っているが、残TODOが canonical に明示細分化されている」場合のみ carry-over 整合として扱う。
- representative route のみ成立、skeleton 実装、ACK-only、partial wiring は `implemented` 禁止で `partial` 判定に固定する。
- Frontend Component Event Runtime などで queue/start/representative emit が存在しても、append-only DB境界未達や全surface配線未達が残る場合は `implemented` 禁止。
- SSOT未達条件が1つでも残る場合、`known_gap_ref` と `remaining_todo` に未達項目を明示的に残す。
- partial Approve は、PR purpose / Issue目的 / user依頼が明示的に partial / scoped progress / non-closing progress の場合に限定する。
- PR本文・Issue目的・user依頼・TODO項目のいずれかが implemented / close / completion / TODO `[x]` を目指す場合、`completion_condition` 未達、remaining `known_gap_ref`、concrete `remaining_todo` が1つでもあれば Request Changes とする。
- TODO/roadmap に未達が明示されている事実は partial 分類の正しさの証拠であり、implemented-target PR の Approve 根拠にはならない。
- 実装意味が進んだ場合でも status 更新前に、TODO/roadmap 記載と実装実態を照合し、completion_condition 充足を再確認する。
- summary-only / 動作確認-only / representative route-only を completion 根拠として扱わない。
- Approve可能な non-blocker findings は PR本文だけで閉じず、`.agent/tasks/todo.md` の `Non-blocking cleanup / hardening carry-over` へ必ず carry-over する。

## output_shape
- Diff reviewed: yes/no
- Changed files
- Todo checked: yes/no
- Roadmap checked: yes/no
- Implementation registry checked: yes/no
- Repo implementation checked: yes/no
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

## out_of_scope
- full-bundle reading by default
- treating `.agent/docs` as always-read bundle
- replacing semantic audit with structure check
- summary-only judgment
- metadata-only (PR metadata/mergeability only) judgment
