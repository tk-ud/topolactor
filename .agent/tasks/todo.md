# Agent Task List — Remaining TODO

## Completed

- [x] Structure check script (`.agent/tests/check-structure.sh`)
- [x] GitHub Actions wrapper (`.github/workflows/structure-check.yml`)
- [x] Agent entrypoint (`AGENTS.md`)
- [x] Agent docs (`.agent/docs/structure-map.yaml`, `.agent/docs/required-paths.yaml`)
- [x] Agent rules (`.agent/rules/rule.md`)
- [x] Agent skills (`.agent/skills/structure-check.md`)
- [x] Agent reports surface (`.agent/reports/README.md`)

## Remaining

### CI / Validation

- [ ] DB schema execution CI — run topology SQL against a real or ephemeral Postgres instance.
- [ ] Backend build / unit tests — dummy topology resolution without production data.
- [ ] Frontend Fresh/Deno type check — verify TypeScript types and Fresh island structure.

### Integration

- [ ] Dummy emission integration test — `default:entity:search → operation_vector → attractor → emission`.
- [ ] PR audit report template under `.agent/reports/`.

### Features

- [ ] Registrar admin UI specification.
- [ ] Promotion manifest editor specification.
- [ ] State policy resolution tests.
- [ ] Diff log append test (non-fatal path).

### Optional Skills

- [ ] `.agent/skills/pr-audit.md`
- [ ] `.agent/skills/issue-writing.md`
- [ ] `.agent/skills/runtime-boundary-check.md`

### Optional Tests

- [ ] `.agent/tests/check-content.sh` — extended content term checks.
- [ ] `.agent/tests/check-agent-surface.sh` — verify agent surface files are non-empty.
