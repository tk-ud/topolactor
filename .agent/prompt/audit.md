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
- conditional: policy/scenario/runtime protocols only when touched

## completion_judgment_axis
- `implemented` 判定は roadmap/TODO/SSOT に定義された completion_condition を満たす場合のみ許可する。
- representative route のみ成立、skeleton 実装、ACK-only、partial wiring は `implemented` 禁止で `partial` 判定に固定する。
- Frontend Component Event Runtime などで queue/start/representative emit が存在しても、append-only DB境界未達や全surface配線未達が残る場合は `implemented` 禁止。
- SSOT未達条件が1つでも残る場合、`known_gap_ref` と `remaining_todo` に未達項目を明示的に残す。
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
