# todo_maintenance prompt router

## purpose
Inspect/cleanup/reconcile `.agent/tasks/todo.md`.

## trigger_condition
Worktype is `todo_maintenance`.

## tool_first_entry
When `.agent/tools/agent-ui-initial-contract` is usable, follow `worktypes` → `start` → `resolve-ssot` → `sections` → `end` per [`docs/governance/agent-ui-protocol-ssot.yaml`](../../docs/governance/agent-ui-protocol-ssot.yaml). `start` inlines the routed prompt as `prompt_content` and the routed required/triggered protocol full text as `protocol_trigger_hints[].content`; SSOT sections are read in `resolve-ssot` / `sections`, not in `start` alone. Protocol file paths listed below are fallback-route/manual verification references only when the tool is unusable, or when checking tool-output absence/routing inconsistency; under tool-first they are already represented by `protocol_trigger_hints[].content` and are not an extra mandatory manual read. After implementation or audit work, close with the full `agent-ui-local-test` chain through `summary`. This file remains the fallback router when the tool is not usable.
## required_reads
- .agent/tasks/todo.md
- .agent/protocols/todo-carry-over.md
- foundation SSOT read gate judgment (when applicable):
  1. docs/framework-core.yaml
  2. docs/design/runtime-orchestration-ssot.yaml
  3. docs/design/pipeline-continuity-ssot.yaml

## optional_reads
- docs/system-roadmap.yaml when roadmap status/known_gap/public_summary is changed
- .agent/docs/ssot-map.yaml when TODO items map to specific runtime/db/frontend/governance surfaces and related SSOT confirmation is needed
- .agent/docs/required-paths.yaml only when TODO items involve `.agent` required paths/structure/check-expectation vocabulary
- related reports in `.agent/reports/`

## protocol_triggers
- .agent/protocols/todo-carry-over.md

## roadmap_change_guard
- roadmap変更時は `.agent/protocols/todo-carry-over.md` の Roadmap update judgment gate を適用する。
- `docs/system-roadmap.yaml` を変更した場合は `bash .agent/tests/check-system-roadmap.sh` を required check として扱う。

## output_shape
todo classification, carry-over decisions, foundation_ssot_read_judgment, remaining unresolved items

## foundation_ssot_read_judgment
- framework_core_read: yes/no/not_required
- runtime_orchestration_read: yes/no/not_required
- pipeline_continuity_read: yes/no/not_required
- reason_if_not_required:
- target_ssot_read_after_foundation:

## out_of_scope
- storing CI waiting/missing tool/remote confirmation bookkeeping as TODO
- treating `.agent/docs` as full-read bundle
