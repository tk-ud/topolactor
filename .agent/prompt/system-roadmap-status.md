# Work-Type Router: system-roadmap-status

## Trigger

Use this router when any of the following applies:

- `docs/system-roadmap.yaml` is changed,
- `implementation_registry` is changed,
- milestone status is changed,
- implemented/production_ready is claimed,
- completion summary includes implementation status claims,
- TODO `[x]` update is tied to implementation status,
- status judgment includes `skeleton` / `partial` / `not_started` / `implemented` / `production_ready`.

## Required Read

- `docs/system-roadmap.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `.agent/protocols/completion.md`
- `.agent/protocols/completion-summary.md`
- When SSOT contract text is changed, also apply `.agent/protocols/ssot-change-impact.md`.
- Keep SSOT impact details in the protocol; do not duplicate procedure text in this router.

## Required Gates

- Roadmap Status Gate
- Completion Governance
- TODO Surface Gate
- Required Check Scope Declaration

## Completion Blockers

Block completion when any applies:

- `status: implemented` without evidence,
- `status: implemented` without satisfying `completion_condition`,
- `production_ready: true` while `known_gap_ref` remains,
- production code still contains skeleton/stub/dummy/pass-through markers while status is `implemented` or `production_ready`,
- TODO `[x]` claims conflict with implementation state in `docs/system-roadmap.yaml`,
- `.agent/tasks/todo.md` is treated as implementation status SSOT.

## Notes

- Use this file as a router only.
- Gate decisions must still be executed via the relevant protocols.
