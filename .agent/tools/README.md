# .agent/tools

## Purpose

`.agent/tools` is the Agent-facing read-only repository observation surface. It provides stable commands for inspecting repository directory surfaces, SSOT routing indexes, and proof-surface maps without treating `.agent/scripts` as a convenience-command namespace.

## Read-only / no mutation boundary

Tools in this directory must not mutate repository files, edit TODO/roadmap/SSOT/proof manifests, create persistent temporary artifacts, or expose file-write options such as `--output`. If a mutation-oriented option is passed to an initial tool, the tool rejects it instead of forwarding it.

## Judgment boundary

Tool output is observation data only. It is not:

- SSOT authority.
- proof completion evidence by itself.
- completion judgment.
- semantic audit judgment.
- implemented / partial / not_started status evidence by itself.

Todo and roadmap statuses may appear only as observed text. They must not be treated as proof or implementation reality.

## Relationship to `.agent/scripts`

`.agent/scripts` owns CI/gate/helper implementation bodies and reusable structured-processing code. `.agent/tools` may expose thin read-only entrypoints over those bodies, but must not duplicate structural processing logic already owned by `.agent/scripts`.

## Available tools

### `directory-map`

Observes a repository directory surface and writes JSON to stdout.

- Inputs: `--root <dir>` and optional `--depth <n>`.
- Output: JSON array using the stable element shape from `.agent/scripts/emit-directory-tree-json.py`.
- Implementation boundary: reuses `.agent/scripts/emit-directory-tree-json.py`; does not expose or forward `--output`.

Example:

```sh
.agent/tools/directory-map --root docs --depth 1
```

### `ssot-map-query`

Observes `.agent/docs/ssot-map.yaml` for Agent-facing read-route discovery.

- Inputs: optional `--query <text>`, `--surface <text>`, and `--path <text>` filters.
- Output: JSON object containing matched entries, source file, query metadata, and authority-boundary metadata.
- Boundary: does not decide SSOT read completion, implementation state, proof state, or todo/roadmap reality.

Example:

```sh
.agent/tools/ssot-map-query --query agent
```

### `proof-surface-map`

Observes proof graph surfaces from `docs/design/test-proof-manifest-ssot.yaml` and reverse lookup data from `.agent/docs/test-bundles.yaml`.

- Inputs: `--all`, `--proof-id <proof_id>`, or `--bundle-id <bundle_id>`.
- Output: JSON object containing observed `proof_id`, runner surfaces, SSOT refs, test files, and `does_not_prove` fields.
- Boundary: does not execute runners, judge proof completion, or claim implemented status.

Example:

```sh
.agent/tools/proof-surface-map --all
```

## Prohibited uses

Do not use `.agent/tools` to:

- write repository files or persistent artifacts;
- bypass `.agent/scripts` / `.agent/tests` gate implementations;
- claim proof passed, completion, implemented, partial, or not_started status;
- treat todo or roadmap text as proof;
- replace required SSOT reads, protocol judgment, semantic audit, or structure checks.

## Planned follow-up

The follow-up child bundle `agent-tools-proof-and-structure-gate` is expected to connect this surface to the proof / structure gate surfaces. That later work owns required-paths, test-bundles, proof-manifest, and dedicated checker wiring; those connections are intentionally not implemented by this initial read-only observation bundle.
