# Agent Governance Routing SSOT

Status: draft SSOT  
Canonical YAML: `docs/governance/agent-governance-routing-ssot.yaml`

## Purpose

This document is the human-readable companion to the machine-readable governance routing SSOT.

The goal is to make `.agent` governance routing refer to a single repository-level source instead of spreading route definitions across README, rules, prompt routers, protocols, checklists, and shell checks.

## Canonical Route

```text
AGENTS.md
→ .agent/rules/rule.md
→ worktype decision
→ .agent/README.md
→ .agent/prompt/<worktype>.md
→ .agent/protocols/<worktype or triggered protocol>.md
→ checklist / tests
```

## Responsibility Split

### `AGENTS.md`

Repository entry contract.

Owns:

- agent role summary
- entry handoff
- minimum start instruction

Does not own:

- full routing body
- protocol body
- token estimate table

### `.agent/rules/rule.md`

Always-on prohibitions and worktype branch.

Owns:

- always-on prohibitions
- minimal workflow invariant
- worktype decision
- branch to prompt

Does not own:

- directory role explanation
- protocol body details
- token estimates
- large read-route explanation
- duplicated prompt/protocol procedures

### `.agent/README.md`

`.agent` directory map.

Owns:

- `.agent` directory purpose
- subdirectory responsibilities
- permanent vs temporary surface distinction

Does not own:

- token estimates
- protocol load comparison
- worktype procedure body
- completion judgment rules

### `.agent/prompt/*`

Worktype router surface.

Owns:

- worktype purpose
- trigger condition
- required reads
- optional reads
- protocol triggers
- output shape
- out of scope

Does not own:

- final PASS/FAIL judgment
- protocol body duplication
- checklist template answers

### `.agent/protocols/*`

Judgment gate surface.

Owns:

- triggered gate conditions
- completion blockers
- PASS / FAIL / NOT_REQUIRED / OUT_OF_SCOPE semantics
- protocol-specific judgment rules

Does not own:

- worktype selection
- directory map
- executable CI implementation

### `.agent/checklists/*`

Viewpoint signature surface.

Owns:

- blank checklist templates
- self-test fixtures
- answer format expectations

Does not own:

- canonical judgment source
- permanent PR-specific filled answers

### `.agent/tests/*`

Executable governance checks.

Owns:

- structure checks
- routing existence checks
- vocabulary/reference integrity checks
- local CI wrapper checks

Does not own:

- semantic PR judgment
- product design authority

### `.agent/tasks/todo.md`

Unresolved work queue.

Owns:

- unresolved implementation work
- unresolved design work
- unresolved SSOT alignment work
- unresolved test-authoring work

Does not own:

- CI waiting records
- local tool absence records
- remote CI pass confirmation records
- completed PR work logs

## Worktypes

### `audit`

PR or diff semantic audit.

Focus:

- implementation meaning consistency
- diff-to-intent consistency
- residual work extraction
- follow-up prompt output when fixes are needed

Structural checks, build checks, type checks, and required path existence are delegated to CI/tests.

### `specific`

Targeted file/function/design-point inspection or correction.

Focus:

- minimal target-surface read
- local scope fixation
- triggered protocol use only when touched surfaces require it

### `implementation_change`

Implementation change against existing SSOT.

### `design_change`

SSOT, docs/design, or externally observable contract change.

### `todo_maintenance`

`.agent/tasks/todo.md` inspection, cleanup, or reconciliation.

### `existing_pr_update`

Follow-up work on an already-open PR.

## Grep Key Policy

- Route ids: English only
- Markers: English only
- Grep keys: English only
- Explanatory prose: Japanese allowed
- Case style: snake_case

Stable worktype ids:

```text
audit
specific
implementation_change
design_change
todo_maintenance
existing_pr_update
```

## Token Estimate Policy

Repository-side governance overhead estimates belong in root `README.md` or another repository-level overview.

`.agent/README.md` must remain a directory purpose map only.

## Prohibited Duplication

Do not duplicate:

- token estimates inside `.agent/README.md`
- protocol body details inside `.agent/rules/rule.md`
- directory map inside `.agent/rules/rule.md`
- judgment rules inside prompt files
- worktype selection inside protocol files
- completed PR work logs inside `.agent/tasks/todo.md`

## Migration Target State

- Add governance SSOT under `docs/governance/`
- Move `.agent/README.md` token estimates to root `README.md`
- Reduce `rule.md` to prohibitions and worktype branching
- Add `audit` worktype prompt/protocol
- Add `specific` worktype prompt/protocol
- Unify grep keys to English snake_case
- Update required paths and check scripts to reference new worktypes
