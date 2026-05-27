# topolactor

Topolactor is a **data-driven topology runtime** and **AI-Driven Development OS** with **SQL Attention** and **CI Attention** for governed, contract-first evolution.

Development started: 2026-05-17 (first repository commit: 79d71f1)

## What is topolactor?

topolactor is a full-stack framework for building data-driven no-code applications.

It is designed around a simple idea:

> UI is a projection.  
> The database stores promoted identities.  
> Runtime operations must pass through a canonical dispatch route.

This makes it possible to build applications where users can compose screens, data flows, and operations without directly editing code or bypassing the system structure.

topolactor is not limited to business applications.  
It can also be used for lightweight mobile games and interactive apps where state, events, UI layout, and runtime behavior are data-driven.

Examples include:

- internal business tools
- admin consoles
- workflow apps
- no-code dashboards
- character/state-based apps
- simple mobile games
- idle / simulation / card / narrative games

The core goal is not just “build UI without code,” but:

> let users compose applications while keeping structure, identity, permissions, and runtime behavior auditable.

## Highlights

- **Data-Driven OS:** Runtime behavior is resolved from persisted topology, registry, manifest, and package definitions rather than ad-hoc surface-by-surface wiring.
- **Package-resolved runtime:** Topology resolution selects package routes, schemas, components, and projections, reducing repeated frontend branching and raw exploration work.
- **Performance posture:** Topolactor is designed to keep runtime paths predictable as the system grows. By resolving UI and operations through topology/package definitions, it avoids accumulating ad-hoc branches that typically make large systems slower and harder to maintain.
- **AI-Driven Development OS:** Change flow is governance-routed through explicit contracts, prompts, protocols, and checks.
- **SQL Attention:** DB + runtime hub-attractor observation pipeline. Physical pressure is observed via logs.current (L2 norm watch function); hub-current attractor fields via logs.hub_current; attractor evidence rows are appended to logs.attention. Scheduler, runtime, and repository enforce no registry or topology mutation.
- **CI Attention:** CI Attention treats CI as an operational checker, not just a pass/fail gate.
- **SSOT / CI governance posture:** Runtime system CI diagnostics emit structured findings with `SystemCiStatus`: Pass, Gap, or Blocking. Agent governance checks use broader CI contract vocabulary defined in `docs/design/ci-contract-ssot.yaml`. Diagnostic results are structured log output; automated persistence and follow-up routing are not yet implemented.

## Tech Stack

PostgreSQL / C# / Deno Fresh / Preact

## Agent Governance Context Cost

Topolactor uses a route-based governance read path before implementation work, so context cost is intentionally estimated and managed.

- Rough estimate method: repository-local character counts, then token approximation by `chars/4` (primary) and `chars/3` (upper-bound).
- Context cost is route-dependent and must be estimated from the selected read path using repository-local character counts. Small targeted governance reads may be low; semantic audit routes that trigger SSOT, implementation files, and test reads must be estimated from the actual file set.
- Full governance bundle loading should be avoided; route-targeted loading is the intended operating posture.

## Implementation Status

This project is in active development. Core dispatch pipeline (manifest-driven routing, runtime executor, timeline scheduler) is implemented. Output lanes and backend SSE emission are partial, while frontend SSE receiver/dispatcher remain skeleton surfaces with projection-runtime wiring still tracked as TODO. Projection constructor runtime interface and `RuntimeTopologyComponentProps` envelope are closed for the current primitive factory path, but manifest-response constructor mapping remains partial. UI/UX primitive catalog and abstract function registry SSOTs are defined; runtime/catalog/factory boundary preparation is in place, while executable primitive promotion is still a not-started/interface-ready follow-up. SQL Attention scheduler/exploration runtime/evidence persistence subpaths are implemented (production_ready: false); parent milestone M7 is partial due to remaining live verification, hub_current attractor-vector hardening, and topology projection gaps. Admin UI surfaces (M5) remain partial. See `docs/system-roadmap.yaml` and `.agent/tasks/todo.md` for component-level status and canonical remaining TODO.

## Where to Go Next

- Public roadmap / status reference (implementation status must be verified against SSOT, implementation files, and tests): `docs/system-roadmap.yaml`
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