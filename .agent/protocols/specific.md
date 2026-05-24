# specific protocol

## workflow_guard
Use for worktype `specific` during DEFINE_SCOPE/JUDGMENT.

## trigger_condition
Local file/function/design-point task.

## allowed_scope
- single-file local fix
- single-function bug fix
- explicitly-audited, clearly bounded one-point adjustment

## disallowed_scope
- PR diff audit
- merge judgment
- roadmap/todo/repo consistency audit
- summary-truth verification
- multi-surface semantic alignment checks

## judgment_scope
Scope containment and correctness of local change.

## todo_roadmap_touch_boundary
- specific may touch TODO/roadmap only when the local target directly affects canonical progress state for that same local scope.
- If TODO/Roadmap finalization is required, read `.agent/protocols/todo-carry-over.md` and apply its carry-over/closure judgments only for the local target.
- If work expands to multi-surface progress-consistency audit or repository-wide status judgment, switch route to `audit` or `todo_maintenance`.

## blocking_conditions
- Unjustified scope expansion.
- Full docs/protocol bundle read treated as required baseline.
- Using `specific` for disallowed scope.

## pass_conditions
- Minimal target-surface read is preserved.
- Any scope expansion has explicit reason.
- roadmap/todo touched status checks include minimum `.agent/tasks/todo.md` and `docs/system-roadmap.yaml` reads.
