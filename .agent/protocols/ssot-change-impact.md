# SSOT Change Impact Protocol

## Trigger

Use this protocol only when one or more of the following applies:

- SSOT files (for example `docs/design/*`) are changed.
- SSOT contract surfaces are changed (route / response / error / emission / projection / jump / persistence).
- SSOT updates change externally observable contracts.

## Required

When triggered, perform all of the following impact checks:

1. Confirm reference surfaces for the changed SSOT.
2. Grep for stale expectations related to the changed contract.
3. Verify update impact across:
   - tests,
   - `.agent/tests` check/CI scripts,
   - frontend contract tests,
   - prompt routers,
   - docs resume/index surfaces,
   - `.agent/tasks/todo.md`.
4. Fix stale expectations that can be corrected in the same PR.
5. If a required follow-up cannot be completed in the same PR, add an explicit TODO entry to `.agent/tasks/todo.md`.

## Prohibited

- Updating only SSOT body text while leaving stale expectations in referenced surfaces.
- Copying detailed per-SSOT specifications into this protocol.
- Growing checklists with incident-specific one-off items instead of using this shared impact protocol.

## Output Expectation

- Keep this protocol lightweight and impact-focused.
- Use protocol + TODO carry-over, not protocol body expansion, to manage unresolved follow-up.
