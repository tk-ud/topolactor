# topolactor

Topolactor is a **data-driven topology runtime** and **AI-Driven Development OS** with **SQL Attention** and **CI Attention** for governed, contract-first evolution.

Development started: 2026-05-17 (first repository commit: 79d71f1)

## Highlights

- **Data-Driven OS:** Runtime behavior is resolved from persisted topology, registry, manifest, and package definitions rather than ad-hoc surface-by-surface wiring.
- **Package-resolved runtime:** Topology resolution selects package routes, schemas, components, and projections, reducing repeated frontend branching and raw exploration work.
- **Performance posture:** Topolactor simplifies runtime paths through topology/package resolution and does not make benchmark-speed claims in this README.
- **AI-Driven Development OS:** Change flow is governance-routed through explicit contracts, prompts, protocols, and checks.
- **SQL Attention:** SQL Attention is a DB + runtime hub-attractor observation pipeline spanning `logs.current`, `logs.hub_current`, append-only `logs.attention`, and scheduler/runtime/repository boundaries. It observes physical pressure and hub-current attractor evidence, and must not mutate registry/topology state.
- **CI Attention:** CI Attention treats CI as an operational checker, not just a pass/fail gate.
- **SSOT / CI governance posture:** Current runtime diagnostic surface (`SystemCiStatus`) supports `Pass`, `Gap`, and `Blocking`. Broader CI contract vocabulary can include `drift` and `not-covered`, but those are not runtime statuses unless implemented in `SystemCiStatus`. Current scheduler behavior emits structured diagnostics/logging; persisted or automated follow-up actions must not be claimed unless implemented.

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
- UX positioning article: `docs/articles/dynamic-support-no-code-positioning.md`
- Agent execution contract and route surfaces: `AGENTS.md`, `.agent/`

## License

Licensed under the Apache License 2.0.

Original concept and architecture by Takumi Udagawa.
Commercial use, modification, redistribution, and cloud deployment are allowed under the license.
If you build on topolactor, attribution and citation are appreciated.
