# topolactor

Topolactor is a **data-driven topology runtime** and **AI-Driven Development OS** with **SQL Attention** and **CI Attention** for governed, contract-first evolution.

## Highlights

- **Data-Driven OS:** Runtime behavior is resolved from persisted topology/registry definitions rather than ad-hoc surface-by-surface wiring.
- **AI-Driven Development OS:** Change flow is governance-routed through explicit contracts, prompts, protocols, and checks.
- **SQL Attention:** SQL-side attention evidence is treated as an observation surface for runtime-relevant pressure and continuity signals.
- **CI Attention:** CI Attention treats CI as an operational checker, not just a pass/fail gate.
- **SSOT / CI governance posture:** System events, implementation diffs, runtime states, and SSOT contracts are projected into structured statuses such as pass, gap, blocking, drift, and not-covered. Those statuses drive follow-up actions such as merge, carry-over, repair, stop, or contract expansion.

## Tech Stack

PostgreSQL / C# / Deno Fresh / Preact

## Agent Governance Context Cost

Topolactor uses a route-based governance read path before implementation work, so context cost is intentionally estimated and managed.

- Rough estimate method: repository-local character counts, then token approximation by `chars/4` (primary) and `chars/3` (upper-bound).
- Route cost is workload-dependent, but baseline governance reads are typically in the low-thousands of tokens, and implementation/design routes increase from there depending on triggered protocol and SSOT reads.
- Full governance bundle loading should be avoided; route-targeted loading is the intended operating posture.

## Where to Go Next

- Public roadmap and status SSOT: `docs/system-roadmap.yaml`
- Agent development/governance overview: `docs/agent-development-os.md`
- Core/runtime and policy SSOT entry points: `docs/framework-core.yaml`, `docs/framework-policy.yaml`
- Design SSOT surfaces: `docs/design/`
- Agent execution contract and route surfaces: `AGENTS.md`, `.agent/`
