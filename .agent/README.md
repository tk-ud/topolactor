# .agent Directory Map

## Purpose

`.agent/` is the execution surface for repository agent governance that is subordinate to:

- `docs/governance/agent-governance-routing-ssot.yaml`
- `docs/governance/agent-governance-routing-ssot.md`

## Route Position

Canonical route:

`AGENTS.md -> .agent/rules/rule.md -> (Claude web/remote only: READ_ENTRY triggers .agent/protocols/claude.md) -> worktype decision -> .agent/README.md -> .agent/prompt/<worktype>.md -> .agent/protocols/<worktype or triggered protocol>.md -> checklist/tests`

## Directory Responsibilities

- `rules/`: always-on prohibitions, minimal workflow invariant, worktype branch.
- `routes/`: executable map between worktype and required prompt/protocol/check references.
- `prompt/`: worktype routers (purpose/trigger/reads/protocol triggers/output shape/out-of-scope).
- `protocols/`: judgment gates (blocking/pass semantics only).
- `checklists/`: viewpoint signature templates.
- `tests/`: executable route/vocabulary/structure checks.
- `docs/`: agent-side required path and ssot mapping indexes.
- `reports/`: persistent inspection reports.
- `tasks/`: unresolved work queue.
- `tmp/`: temporary artifacts only.
- `skills/`: task procedures.
- `tools/`: Agent-facing tool entrypoints. Existing observation tools are read-only over repository/product surfaces. Tool-side or governance-side writes require explicit SSOT/task scope and must not touch Topolactor product/runtime/source implementation surfaces during ordinary tool use.
- `scripts/`: CI / gate / helper implementation bodies. Scripts may provide reusable structured-processing bodies for tools, but `.agent/scripts` is not the Agent-facing convenience-command surface.

## Boundary Notes

- `.agent/README.md` is a directory map only.
- Token estimates, protocol body details, and completion judgment logic are out of scope here.
- `docs/system-roadmap.yaml` と `.agent/tasks/todo.md` は常時変更対象の動的参照点。実装実態の正本ではない。実装状態の確認は実コードおよびテストから行い、ロードマップやTODOの記述のみを根拠に実装完了・未着手と判断しない。
