# Agent Development OS

This document is an external overview and agenda for Topolactor's Agent Development OS. It explains where the layer sits, what it governs, and where readers should go next. It is not the rule source for agent execution.

## Positioning

Topolactor has two cooperating layers:

- **Application Runtime OS** — the runtime-defined application scaffold that resolves topology, dispatch, projection, and policy from stored definitions.
- **Agent Development OS** — the governance layer that constrains how AI agents change this repository.

The Agent Development OS is not application runtime logic. It does not execute user operations, define database schema, or project frontend UI. Its job is to keep repository changes inside explicit judgment boundaries while the runtime remains data-defined.

## What it fixes for agents

AI agents can edit many repository surfaces quickly, but speed creates risk when runtime, persistence, projection, and policy boundaries are implicit. The Agent Development OS makes those boundaries explicit by fixing:

- the judgment boundary for what an agent may change;
- the runtime, persistence, projection, and policy boundaries an agent must not break;
- the verification order agents must follow before claiming completion;
- the completion conditions that separate "implemented" from "verified";
- the return path when a gate fails and work must go back to the fix phase.

In short: the Agent Development OS turns agent work from free-form patch generation into governed repository operation.

## Scope

The Agent Development OS covers questions such as:

- Which repository surfaces are in scope for a requested change?
- Which runtime, persistence, projection, or policy boundaries are protected for that change?
- Which verification gates must pass before the task can be reported complete?
- Which failures are blocking rather than advisory?
- Where should the agent resume when verification fails?

## Non-scope

The Agent Development OS is deliberately not:

- the specification body of the Application Runtime OS;
- the source of truth for DB schema;
- the implementation detail source for backend or frontend code;
- a prompt collection for unconstrained agent writing.

Runtime behavior belongs to the runtime surfaces. Persistence shape belongs to schema and migration surfaces. Backend and frontend behavior belongs to their implementation and tests. The Agent Development OS governs how agents change those surfaces; it does not replace them.

## Reading order

External readers should use the following order:

1. **`README.md`** — project overview and the two-layer positioning.
2. **`docs/agent-development-os.md`** — external overview and agenda for the governance layer.
3. **`AGENTS.md`** — repository-local agent contract entrypoint.
4. **`.agent/rules/`** — durable judgment rules for agent decisions.
5. **`.agent/protocols/`** — verification protocols and completion procedures.
6. **`.agent/scripts/`, `.agent/checklists/`, `.agent/tests/`** — executable support, local gates, and structural checks.

This document should help readers understand why the agent governance layer exists before they read the executable contract and checks.

## Why `.agent` stays repository-local

The `.agent` tree is intentionally kept in this repository instead of being moved into an external source of truth.

- It is repository-specific operating policy and must be verified against the same branch diff as implementation changes.
- If an external document could override concrete `.agent` protocols, agent judgment would split across multiple sources of truth.
- The portable part is the design principle: governed agent operation with explicit boundaries and gates. The concrete checks must remain close to the repository they protect.

For that reason, this overview describes the agenda, while `AGENTS.md` and `.agent` remain authoritative for actual agent execution.
