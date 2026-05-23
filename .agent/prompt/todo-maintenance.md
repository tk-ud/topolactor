# todo_maintenance prompt router

## purpose
Inspect/cleanup/reconcile `.agent/tasks/todo.md`.

## trigger_condition
Worktype is `todo_maintenance`.

## required_reads
- .agent/tasks/todo.md
- .agent/protocols/todo-carry-over.md

## optional_reads
- .agent/docs/ssot-map.yaml when TODO items map to specific runtime/db/frontend/governance surfaces and related SSOT confirmation is needed
- .agent/docs/required-paths.yaml only when TODO items involve `.agent` required paths/structure/check-expectation vocabulary
- related reports in `.agent/reports/`

## protocol_triggers
- .agent/protocols/todo-carry-over.md

## output_shape
todo classification, carry-over decisions, remaining unresolved items

## out_of_scope
- storing CI waiting/missing tool/remote confirmation bookkeeping as TODO
- treating `.agent/docs` as full-read bundle
