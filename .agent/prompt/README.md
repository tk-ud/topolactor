# Prompt Router Surface

## Purpose

`.agent/prompt/` is the work-type router surface.

It exists to help the agent decide:

- which SSOT documents must be read,
- which protocols/checklists are triggered,
- which completion blockers must be enforced.

## What This Surface Is Not

- not a protocol,
- not a checklist,
- not a task instruction archive,
- not an always-read bundle.

Read only the router file that matches the current work type.

## Responsibility Split

- `AGENTS.md` = entry contract
- `.agent/rules/rule.md` = global invariant / trigger map
- `.agent/README.md` = `.agent` map
- `.agent/prompt/*.md` = work-type router
- `.agent/protocols/*.md` = judgment gates
- `.agent/skills/*.md` = execution procedures
- `docs/*.yaml` = SSOT
- `.agent/tasks/todo.md` = task queue

## Usage

1. Identify work type from task materials and target surfaces.
2. Open the matching `.agent/prompt/<work-type>.md` only when applicable.
3. Follow that router to load minimum required SSOT and triggered protocols.
4. Execute gate judgment in protocols; router guidance does not replace protocols.
