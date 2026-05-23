# Prompt Router Surface

Prompt files are worktype routers only. They must contain:

- purpose
- trigger_condition
- required_reads
- optional_reads
- protocol_triggers
- output_shape
- out_of_scope

Prompt files must not contain final PASS/FAIL judgment logic.

## Common `.agent/docs` route

- **ssot-map route**: read `.agent/docs/ssot-map.yaml` only when touched surfaces need SSOT/supporting-doc/protocol selection (runtime, backend, frontend, DB/SQL Attention, pipeline, governance surfaces).
- **required-paths route**: read `.agent/docs/required-paths.yaml` only when changing/auditing `.agent` structure, required paths, required content terms, or structure-check expected vocabulary.
- **read condition**: both files are conditional indexes; open them only when the task trigger matches.
- **not full-read bundle**: `.agent/docs` is not an always-read or full-read bundle.
- **check-structure authority**: executable authority remains `bash .agent/tests/check-structure.sh`; `required-paths.yaml` is a human-readable reference.
