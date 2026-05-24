# audit prompt router

## purpose
PR/diff semantic audit and merge-readiness judgment with implementation-meaning alignment.

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

## output_shape
- Diff reviewed: yes/no
- Changed files
- Todo checked: yes/no
- Roadmap checked: yes/no
- Implementation registry checked: yes/no
- Repo implementation checked: yes/no
- Semantic findings
- Required follow-up
- Merge judgment

## out_of_scope
- full-bundle reading by default
- treating `.agent/docs` as always-read bundle
- replacing semantic audit with structure check
- summary-only judgment
- metadata-only (PR metadata/mergeability only) judgment
