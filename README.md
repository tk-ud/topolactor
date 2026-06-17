# topolactor

topolactor is a production-ready core for data-driven topology runtime.

Topolactor is a **registry-backed full-stack framework** for building data-driven apps, no-code tools, and lightweight interactive games.

Development started: 2026-05-17 (first repository commit: 79d71f1)  

Production-ready core achieved: 2026-05-29 (M6/M7 core runtime boundaries CI green)

AI development cost to production-ready core: $40/month fixed subscription run-rate  
- ChatGPT Plus / Codex: $20/month
- Claude Pro / Claude Code: $20/month
- API usage billing: $0
- Pay-as-you-go agent billing: not used

Production-ready core was achieved using only fixed monthly AI subscriptions, with no usage-based API spend.

## What is topolactor?

topolactor treats application structure as promoted database identity rather than scattered frontend code.

Users can freely draft screens, layouts, operations, and state flows.
When a draft is promoted into canonical runtime state, topolactor assigns database identities and routes it through explicit dispatch and audit boundaries.

The core idea:

> UI is a projection.  
> The database stores promoted identities.  
> Runtime operations must pass through a canonical dispatch route.  
> CI checks whether implementation, SSOT, roadmap, and tests still agree.


## Authoring UX

Build freely in the admin UI builder.  
SQL Attention suggests candidates.  
CI Attention shows guidance.  
Manifests make promoted drafts runnable.

The intended workflow starts in the admin UI builder.

1. Draft screens, layouts, components, operations, and state flows freely.
2. Use SQL Attention to surface dynamic candidates from hub/log/relation/attractor evidence.
3. Use CI Attention guidance to view missing inputs, valid candidates, structural violations, and boundary issues.
4. Promote useful draft structures into database-backed identities.
5. Connect promoted structures to runtime behavior through manifests and canonical dispatch.

Drafting stays flexible. Strictness appears at the promotion boundary, where structures become auditable runtime identities.

## Auditability and risk model

Because promoted runtime behavior flows through manifest-backed dispatch instead of scattered hardcoded frontend logic, topolactor helps prevent accidental data exposure caused by miswiring, hidden routes, or untracked structure changes.

It does not replace security review, permission design, or load testing; it narrows a common class of human-error risks by making runtime wiring explicit and auditable.

## Flexible external integrations

topolactor keeps integration surfaces explicit and data-driven instead of scattering service-specific wiring through application code.

External services are connected through connector or port surfaces. Those surfaces stay on the intake, response, trigger, notification, approval, or export side, while promoted topology, manifests, registry state, schema, and runtime logs remain on the canonical runtime side.

This keeps integration flexible while preserving pipeline boundaries such as validation, preview/apply, scheduling, dispatch, and audit/event recording.

## Service switching by DB instance

topolactor can be re-pointed at a different DB instance to run as a different service context without forking the application code.

The environment selects the target DB instance. Service-specific topology, manifests, connector settings, runtime data, and audit history live behind that DB instance.

Changing the DB instance changes the service context: the data, external connections, operational state, and promoted runtime identities can differ while the shared application code, canonical dispatch route, and projection model stay the same.

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

topolactor keeps that freedom at the draft layer, then adds an explicit promotion boundary so useful structures can become auditable runtime identities without turning every edit into a system lock.

A component is not “real” just because it exists in frontend code. It becomes real when it is promoted into the topology registry and receives database identities such as `componentId`, `packageId`, `layoutId`, `wiringId`, and `tensorId`.

This keeps user-authored apps flexible while preventing hidden routes, silent fallback, and untracked structure changes.

## Core model

- **Registry-backed UI:** Components, packages, layouts, wiring, and topology tensors are promoted into database identities before they are treated as runtime projection targets.
- **Single dispatch shape:** Application operations enter through a canonical dispatch route instead of many unrelated endpoints.
- **Projection-only frontend:** The frontend projects approved topology state. It does not own topology meaning or persistence authority.
- **SSOT-driven development:** Design documents, roadmap state, implementation files, and tests are expected to stay semantically aligned.
- **CI Attention:** Guides missing inputs, valid candidates, structural violations, and break boundaries so runtime/backend validation does not expand into hidden branching.
- **SQL Attention:** Uses hub/log/relation/attractor evidence to grow hub construction, hub connection, and projection candidates.

  ```text
  attention_strength = neighbor_score * ||current_basis||_2
  ```

  Phase Attention keeps the current norm as `w`, while candidate position and movement are expressed on the `i/j/k` axes:

  ```text
  q_phase(candidate) = w + x*i + y*j + z*k
  ```

  `w` is the current L2 norm / attention pressure, `x/y/z` are hub-side population bases, and `i/j/k` carry axis movement amounts for the phase candidate.
- **Scope note (CI Attention):** CI Attention is input guidance and boundary guidance; canonical dispatch / explicit failure remain runtime/backend responsibilities.
- **Scope note (SQL Attention):** Topology recommendation current is a child projection consumer, not SQL Attention itself, and does not auto-mutate fixed routes or registry/topology definitions.
- **AI-Driven Development OS:** Agent work is routed through repository-local contracts, prompts, protocols, and checks so changes remain auditable.

## Tech Stack

PostgreSQL / C# / Deno Fresh / Preact

## Project status

The M6/M7 core runtime is production-ready. Development continues on optional external connectors, broader UX acceptance, and future subsystem expansion.

The repository already contains the core dispatch pipeline, runtime scheduling/dispatch surfaces, admin UI topology registration surfaces, primitive UI/catalog boundaries, abstract runtime function boundaries, SQL Attention runtime/projection surfaces, and CI governance surfaces.

Production readiness varies by subsystem. Do not treat this README as the implementation status source of truth. Verify status against:

- `docs/system-roadmap.yaml`
- `.agent/tasks/todo.md`
- relevant SSOT files under `docs/design/`
- implementation files and tests

## Future SSOT-directed initiatives

Upcoming work is gated by SSOT before implementation. Near-term design lanes:

- **CLI/MCP reader port:** define a safe read and export surface for AI, CLI, and shell readers through approved project boundaries.
- **Implementation SSOT:** define Context API, Data Reader, export job, manifest, checksum, and audit log contracts before code changes.
- **Runtime bundle SSOTs:** split optional side-effect surfaces into separate bundle contracts before runtime wiring.
- **External surface policy:** keep third-party tools as intake, draft, notification, approval, or trigger surfaces rather than runtime authority.
- **User-facing helper/manual:** document authoring flows and boundary concepts for builders without making help text a runtime authority.

These items are roadmap-facing summaries only. Canonical design authority remains in `docs/system-roadmap.yaml`, `.agent/tasks/todo.md`, and the relevant SSOT files under `docs/design/`.

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
- UX positioning article: `articles/dynamic-support-no-code-positioning.md`
- Agent execution contract and route surfaces: `AGENTS.md`, `.agent/`

## License

Licensed under the Apache License 2.0.

Original concept and architecture by Takumi Udagawa.

Commercial use, modification, redistribution, and cloud deployment are allowed under the license.

If you build on topolactor, attribution and citation are appreciated.
