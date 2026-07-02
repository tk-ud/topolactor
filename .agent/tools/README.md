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


### `topology-seed-discussion`

Maps seed discussion into a two-stage, read-only question flow. Stage 1 selects a `question_space` such as `sql_attention_observation`, `topology_manifest_authoring`, `admin_contents_authoring`, `admin_ui_builder_authoring`, `admin_manifests_navigation`, `runtime_manifest_dispatch`, `seed_runtime_import`, or `db_topology_wiring`. Stage 2 is generated from SSOT YAML (`docs/design/admin-console-workflow-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/sql-attention-logs-ssot.yaml`, and `docs/design/db-schema.yaml`) rather than UI-derived hand-written question keys. Each YAML object/list/scalar is normalized to a stable `source_ref` + `path` element with `key`, `question`, `answer_type`, `json_fragment`, `category`, and implementation/DB `validation_ref` metadata.

- `inspect`: emits the Stage 1 `question_space_selector` plus a binary answers template.
- `inspect --space <question_space>`: emits the Stage 2 indexed bit schema for that space.
- `expand --answers <stage1_answers.json>`: reads Stage 1 answers and returns Stage 2 schemas for selected spaces.
- `build-template --space <question_space> --bits '[1,0,1,...]'` or `build-template --answers <stage2_answers.json>`: deep-merges only the selected SSOT element JSON fragments and prints a structurally valid tmp seed candidate template for an AI/human to fill. The tool does not write `tmp.json`; callers may redirect stdout to `/tmp/...json`.
- `build --answers <tmp.json>`: reads an AI-filled tmp JSON file and prints candidate discussion JSON.
- Boundary: output is a discussion draft only; it is not SSOT authority, seed adoption, proof completion, or implemented status evidence. The tool does not write seed SQL, manifests, SSOT, TODO, or roadmap files and does not connect to a DB, external API, or AI API.

Examples:

```sh
.agent/tools/topology-seed-discussion inspect
.agent/tools/topology-seed-discussion inspect --space admin_ui_builder_authoring
.agent/tools/topology-seed-discussion expand --answers /tmp/stage1.json
.agent/tools/topology-seed-discussion build-template --space admin_ui_builder_authoring --bits '[1,1,0,1]' > /tmp/topology-seed-discussion.tmp.json
.agent/tools/topology-seed-discussion build --answers /tmp/topology-seed-discussion.tmp.json
```

## Prohibited uses

Do not use `.agent/tools` to:

- write repository files or persistent artifacts;
- write seed SQL, manifests, SSOTs, TODO, or roadmap files;
- connect to a DB, external API, or AI API;
- bypass `.agent/scripts` / `.agent/tests` gate implementations;
- claim proof passed, completion, implemented, partial, or not_started status;
- treat todo or roadmap text as proof;
- replace required SSOT reads, protocol judgment, semantic audit, or structure checks.

## Planned follow-up

The follow-up child bundle `agent-tools-proof-and-structure-gate` is expected to connect this surface to the proof / structure gate surfaces. That later work owns required-paths, test-bundles, proof-manifest, and dedicated checker wiring; those connections are intentionally not implemented by this initial read-only observation bundle.
