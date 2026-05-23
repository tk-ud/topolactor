# todo_maintenance prompt router

## purpose
Inspect/cleanup/reconcile `.agent/tasks/todo.md`.

## trigger_condition
Worktype is `todo_maintenance`.

## required_reads
- .agent/tasks/todo.md
- .agent/protocols/todo-carry-over.md

## optional_reads
- related reports in `.agent/reports/`

## protocol_triggers
- .agent/protocols/todo-carry-over.md

## output_shape
todo classification, carry-over decisions, remaining unresolved items

## out_of_scope
- storing CI waiting/missing tool bookkeeping as TODO
