# topolactor

Topolactor is a **registry-backed full-stack framework** for building data-driven apps, no-code tools, and lightweight interactive games.

Development started: 2026-05-17 (first repository commit: 79d71f1)

## What is topolactor?

topolactor treats application structure as promoted database identity rather than scattered frontend code.

Users can compose screens, layouts, operations, and state flows, but runtime behavior must pass through canonical dispatch, validation, and CI-audited contracts.

The core idea:

> UI is a projection.  
> The database stores promoted identities.  
> Runtime operations must pass through a canonical dispatch route.  
> CI checks whether implementation, SSOT, roadmap, and tests still agree.

## What you can build

topolactor is not limited to business applications. It is useful for any app where state, events, UI layout, and runtime behavior are data-driven.

Examples:

- internal business tools
- admin consoles
- workflow apps
- no-code dashboards
- character/state-based apps
- lightweight mobile games
- idle / simulation / card / narrative games

## Why it is different

Most no-code tools prioritize free composition.

topolactor prioritizes **composable systems that remain auditable**.

A component is not “real” just because it exists in frontend code. It becomes real when it is promoted into the topology registry and receives database identities such as `componentId`, `packageId`, `layoutId`, `wiringId`, and `tensorId`.

This keeps user-authored apps flexible while preventing hidden routes, silent fallback, and untracked structure changes.

## Core model

- **Registry-backed UI:** Components, packages, layouts, wiring, and topology tensors are promoted into database identities before they are treated as runtime projection targets.
- **Single dispatch shape:** Application operations enter through a canonical dispatch route instead of many unrelated endpoints.
- **Projection-only frontend:** The frontend projects approved topology state. It does not own topology meaning or persistence authority.
- **SSOT-driven development:** Design documents, roadmap state, implementation files, and tests are expected to stay semantically aligned.
- **CI Attention:** CI is treated as an operational inspection surface, not just a pass/fail gate.
- **SQL Attention:** Database/runtime observation surfaces track pressure, hub-current state, and attractor evidence without mutating registry or topology definitions directly.
- **AI-Driven Development OS:** Agent work is routed through repository-local contracts, prompts, protocols, and checks so changes remain auditable.

## Tech Stack

PostgreSQL / C# / Deno Fresh / Preact

## Project status

This project is in active development.

The repository already contains the core dispatch pipeline, runtime scheduling/dispatch surfaces, admin UI topology registration surfaces, primitive UI/catalog boundaries, abstract runtime function boundaries, SQL Attention research surfaces, and CI governance surfaces.

Production readiness varies by subsystem. Do not treat this README as the implementation status source of truth. Verify status against:

- `docs/system-roadmap.yaml`
- `.agent/tasks/todo.md`
- relevant SSOT files under `docs/design/`
- implementation files and tests

## Agent Governance Context Cost

Topolactor uses a route-based governance read path before implementation work, so context cost is intentionally estimated and managed.

- Rough estimate method: repository-local character counts, then token approximation by `chars/4` (primary) and `chars/3` (upper-bound).
- Context cost is route-dependent and must be estimated from the selected read path using repository-local character counts. Small targeted governance reads may be low; semantic audit routes that trigger SSOT, implementation files, and test reads must be estimated from the actual file set.
- Full governance bundle loading should be avoided; route-targeted loading is the intended operating posture.

## Where to Go Next

- Public roadmap / status reference: `docs/system-roadmap.yaml`
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
