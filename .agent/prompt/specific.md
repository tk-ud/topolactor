# specific prompt router

## purpose
Single-file/function/design-point local inspection or fix.

## trigger_condition
Worktype is `specific` and scope is strictly local:
- 単一ファイルの局所修正
- 単一関数のバグ修正
- 監査済みで修正箇所が明確な1点変更

## required_reads
- target files only
- .agent/protocols/specific.md

## optional_reads
- .agent/tasks/todo.md and docs/system-roadmap.yaml only when target touches roadmap/todo/status judgment
- .agent/docs/ssot-map.yaml when target file/function maps to SSOT-mapped surfaces
- .agent/docs/required-paths.yaml only when touching `.agent` structure, required paths, or structure-check expectation vocabulary

## protocol_triggers
- .agent/protocols/specific.md
- conditional: .agent/protocols/todo-carry-over.md when the local target touches TODO/roadmap/status judgment or changes canonical progress state
- runtime/policy/scenario protocols only if trigger applies

## output_shape
local scope statement, touched targets, decisions, checks

## out_of_scope
- PR差分監査
- merge判断
- roadmap/todo/repo整合確認
- 複数surfaceの意味整合確認
- Summary の検証
- 「差分見ろ」「進捗見て」「マージしていいか」系
- reading all docs/protocols by default
- scope expansion without explicit reason
