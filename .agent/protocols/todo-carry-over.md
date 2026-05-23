# TODO Carry-over Protocol (Agenda: todo-carry-over)

## Trigger condition

Read this protocol only when remaining TODO classification, close-candidate judgment,
or roadmap carry-over decisions are needed.

## `.agent/tasks/todo.md` responsibility

- `.agent/tasks/todo.md` preserves unresolved implementation, design, SSOT, or test-authoring work that must survive beyond the current PR/conversation.
- Do not use `.agent/tasks/todo.md` for CI waiting, remote CI pass confirmation, local tool absence, or verification-only bookkeeping.
- If a failed/unexecuted check reveals concrete follow-up implementation/design/SSOT work,
  carry that concrete work as TODO; do not carry the check execution activity itself.

## Remaining TODO classification

### 残すべき TODO 候補

Keep only work items that are still unresolved and require concrete follow-up.
Each candidate should include objective completion conditions and affected surfaces.

### 削除 / close 候補

Close candidates are items where implementation, verification evidence, and scope intent
are complete with no concrete remaining work.

### roadmap 更新候補

When residual work or completed closure affects roadmap status,
propose/update corresponding roadmap entries with known gap references.

## Auditor finalizer boundary

- Completion/follow-up summary `残タスク引き継ぎ指示` is Auditor TODO input,
  not canonical TODO/roadmap closure.
- Auditor is the default finalizer for canonical TODO/roadmap reflection.

## Implementation agent direct-close conditions

Implementation agent may directly close canonical TODO/roadmap items only when all are explicit:

1. completion protocol closure conditions are satisfied,
2. no partial/skeleton/known_gap remains,
3. required evidence is present and classification is unambiguous,
4. no auditor-only uncertainty remains.

If any condition is unmet or unknown, keep it as Auditor TODO input.
