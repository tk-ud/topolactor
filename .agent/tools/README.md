# .agent/tools

## Purpose

`.agent/tools` is the Agent-facing repository tool surface. Existing observation tools provide stable commands for inspecting repository directory surfaces, SSOT routing indexes, and proof-surface maps without treating `.agent/scripts` as a convenience-command namespace.

## Mutation boundary

Existing observation tools in this directory are read-only over repository/product surfaces. They must not edit Topolactor application/runtime/product source, TODO/roadmap/SSOT/proof manifests, or expose file-write options such as `--output`. If a mutation-oriented option is passed to an observation tool, the tool rejects it instead of forwarding it.

Tool-side or governance-side writes are different. A tool may write tool-owned or governance-owned artifacts only when an applicable SSOT and task scope explicitly require it, for example Agent UI evidence files or temporary Agent UI working files. These writes must stay inside the declared tool/governance boundary and must not become ordinary mutation of Topolactor product/runtime/source implementation surfaces.

## Judgment boundary

Tool output is observation data only. It is not:

- SSOT authority.
- proof completion evidence by itself.
- completion judgment.
- semantic audit judgment.
- implemented / partial / not_started status evidence by itself.

Todo and roadmap statuses may appear only as observed text. They must not be treated as proof or implementation reality.

## Relationship to `.agent/scripts`

`.agent/scripts` owns CI/gate/helper implementation bodies and reusable structured-processing code. `.agent/tools` may expose thin entrypoints over those bodies, but must not duplicate structural processing logic already owned by `.agent/scripts`.

## Recommended usage order

`.agent/tools` is not an autonomous exploration entrypoint — read prompt/protocol/todo required-reads first (see `## Prohibited uses` below). When a task needs repository observation beyond those required reads, the intended chain is:

1. **`directory-map`** — see the repo/docs/`.agent` directory structure and find a promising YAML/doc file.
2. **`yaml-section-query`** — list that file's top-level sections (cheap), then read only the specific section path you need instead of the whole file.
3. **`proof-surface-map`** — from a `proof_id`, `bundle_id`, or an SSOT path found via step 2, look up the related proof/runner/test/bundle candidates.
4. **`topology-seed-discussion`** — only when the task is specifically seed-structure discussion; it is SSOT-driven and independent of steps 1-3.

Each step is optional and skippable; this is a suggested order for combining the tools, not a mandatory workflow gate.

## Available tools

### `directory-map`

Observes a repository directory surface and writes JSON to stdout.

- Inputs: `--root <dir>` and optional `--depth <n>`.
- Output: JSON array using the stable element shape from `.agent/scripts/emit-directory-tree-json.py`.
- Implementation boundary: reuses `.agent/scripts/emit-directory-tree-json.py`; does not expose or forward `--output`.
- Boundary: `--root` is normalized to a repo-relative path and validated to resolve inside the repository (including through symlinks) before being forwarded to the emitter; absolute paths, `../` escapes, and symlink escapes fail-close with a non-zero exit instead of walking outside the repo.

Example:

```sh
.agent/tools/directory-map --root docs --depth 1
```

### `yaml-section-query`

Reads a repository `*.yaml`/`*.yml` file's hierarchical structure without dumping the whole file. This is the intended entry point for the "found a YAML/doc with `directory-map`, now read only the relevant section" flow: list top-level sections first, then request only the section subtree you need.

- Inputs:
  - `--file <repo-relative-path>` (required): must resolve inside the repository and end in `.yaml`/`.yml`; symlink/traversal escapes are rejected.
  - `--list-sections`: force a section listing even when a path/section selector is also given (lists the selected node's children instead of its full value).
  - `--path-json '["mapping",0,"work_type"]'`: canonical path selector as a JSON array. Object keys are matched as literal strings (dots, slashes, and digit-looking keys are never misread), list indices are matched as integers, decided by the current node's type at each step — not by the token's own type.
  - `--path '/mapping/0/work_type'`: canonical path selector as a JSON-Pointer-style string (`~0`/`~1` escaping for `~`/`/` inside a key). Equivalent to the `--path-json` example above.
  - `--section <key-name>`: convenience alias, not canonical. Searches the whole file for dict keys with an exact match.
  - `--depth <n>`: for section listing, how many levels below the selected path to list; for a resolved section value, how many levels to expand before summarizing the remainder.
  - `--format json` (default and only supported value).
- The canonical path selector is `path` (an array of string/int segments), never a bare section name; `--path`/`--path-json` are two equivalent encodings of the same canonical path selector, and every output includes both the `path` array and a human-readable `path_text` (JSON-Pointer string) built from it. `--section` is a convenience-only alias over an exact key-name search — it is never treated as canonical and never guesses among multiple hits.
- Default output (no `--path`/`--path-json`/`--section`, or `--list-sections` given explicitly): `mode: "list_sections"`, a minimal projection of the immediate children (or `--depth`-bounded descendants) of the selected path — `path`, `path_text`, `key`, `kind` (`object`/`list`/`scalar`), `depth`, `child_count`, and a short `preview` (truncated scalar, or `{keys, key_count}` / `{item_count}` for object/list). It never dumps the full YAML by default.
- Explicit section read (`--path`/`--path-json`, or a `--section` that resolves to exactly one key): `mode: "section"`, with `selected_section.value` holding the resolved subtree. Large subtrees are automatically depth/size-bounded (`truncated: true`, `truncated_summary`, `__omitted_child_count`) even without `--depth`; passing `--depth` narrows this further.
- Ambiguous `--section`: if the key name matches more than one node in the file, the tool returns `mode: "ambiguous"` with a `candidates` list (`path`, `path_text`, `key`, `kind`, `depth`, `preview` per candidate) and **no** resolved value — it never silently guesses one match.
- Errors are explicit and never silently fall back: `not_found`, `invalid_path`, `path_outside_repo`, `unsupported_file_type`, `parse_error` (returned as `mode: "error"` JSON on stdout, non-zero exit).
- Boundary: a section being found is not a "should read" or "implementation complete" judgment; output is observation data only.

Examples:

```sh
.agent/tools/yaml-section-query --file .agent/docs/ssot-map.yaml
.agent/tools/yaml-section-query --file .agent/docs/ssot-map.yaml --list-sections --depth 2
.agent/tools/yaml-section-query --file .agent/docs/ssot-map.yaml --path-json '["mapping",0]'
.agent/tools/yaml-section-query --file .agent/docs/ssot-map.yaml --path '/mapping/0/work_type'
.agent/tools/yaml-section-query --file .agent/docs/ssot-map.yaml --section protocols
```

> `ssot-map-query` was retired: use `yaml-section-query --file .agent/docs/ssot-map.yaml ...` instead. It was a thin alias with no structured processing of its own, so removing it does not lose any capability — `check-agent-tools-surface.sh` gates that it stays removed (no entrypoint file, no dispatch target, no README documentation).

### `proof-surface-map`

Observes proof graph surfaces from `docs/design/test-proof-manifest-ssot.yaml` and reverse lookup data from `.agent/docs/test-bundles.yaml`.

- Inputs: `--all`, `--proof-id <proof_id>`, `--bundle-id <bundle_id>`, or `--ssot <repo-relative-path>`.
  - `--ssot <repo-relative-path>`: looks up proof entries related to a target SSOT/implementation path (e.g. one found via `yaml-section-query`), matching against `ssot_refs` (exact), `missing_ssot_blocking` (exact), `evidence_inputs` (exact), and `required_when.changed_files` (glob, e.g. `backend/**/*.cs`). Each returned entry includes `ssot_match_fields` listing which of those fields matched, so the match reason stays auditable.
- Output: JSON object containing observed `proof_id`, runner surfaces, SSOT refs, test files, and `does_not_prove` fields.
- Boundary: does not execute runners, judge proof completion, or claim implemented status. An empty `--ssot` match means no proof edge currently references that path — not that the SSOT is unproven or invalid.

Examples:

```sh
.agent/tools/proof-surface-map --all
.agent/tools/proof-surface-map --ssot docs/design/db-schema.yaml
```


### `topology-seed-discussion`

Maps seed discussion into a two-stage, read-only question flow. Stage 1 selects a `question_space` such as `sql_attention_observation`, `topology_manifest_authoring`, `admin_contents_authoring`, `admin_ui_builder_authoring`, `admin_manifests_navigation`, `runtime_manifest_dispatch`, `seed_runtime_import`, or `db_topology_wiring`. Stage 2 is generated from SSOT YAML (`docs/design/admin-console-workflow-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/sql-attention-logs-ssot.yaml`, and `docs/design/db-schema.yaml`) rather than UI-derived hand-written question keys. Each YAML object/list/scalar is normalized to a stable `source_ref` + `path` element with `key`, `question`, `answer_type`, `json_fragment`, `category`, and implementation/DB `validation_ref` metadata.

- `inspect`: emits the Stage 1 `question_space_selector` plus a binary answers template.
- `inspect --space <question_space>`: emits the Stage 2 indexed bit schema for that space.
- `expand --answers <stage1_answers.json>`: reads Stage 1 answers and returns Stage 2 schemas for selected spaces.
- `build-template --space <question_space> --bits '[1,0,1,...]'` or `build-template --answers <stage2_answers.json>`: deep-merges only the selected SSOT element JSON fragments and prints separate `discussion_metadata` and `seed_candidate_payload` objects plus a structurally valid tmp template for an AI/human to fill. `seed_candidate_payload` is shaped for SeedRuntime (`version` + `runtimes`), includes a non-empty runtime declaration when bits are selected, and remains a candidate only. The tool does not write `tmp.json`; callers may redirect stdout to `/tmp/...json`.
  - `--bits` shorter than the space's schema length is accepted as a prefix: unspecified trailing bits are treated as `0` (input-load reduction for spaces with hundreds/thousands of bits). This prefix zero-fill is never silent — every `build-template` response includes a top-level `bits_length` object (also mirrored inside `discussion_metadata.bits_length`) with `mode` (`"exact"` or `"prefix_zero_fill"`), `expected` (schema bit count), `provided` (bits array length), `implicit_zero_fill` (count of trailing bits assumed `0`), and `bits_length_mismatch` (boolean). The same shape is emitted with `mode: "exact"` and `bits_length_mismatch: false` when `--bits` matches the schema length exactly, so callers can rely on the field always being present rather than only on mismatch.
  - `--bits` longer than the schema, containing an out-of-range index, or containing a non-`0`/`1` value still fails closed (non-zero exit, no `bits_length`/template emitted); prefix shortening is the only length deviation this tool accepts.
- `build --answers <tmp.json>`: reads an AI-filled tmp JSON file, fail-closes invalid `seed_candidate_payload`, and prints separated discussion output plus SeedRuntime-shaped seed candidate JSON. When the input tmp JSON carries a `discussion_metadata.bits_length` (as produced by `build-template`), it is passed through unchanged and mirrored at the top-level `bits_length` field; hand-authored tmp JSON without it yields `bits_length: null`.
- Boundary: output is a discussion draft only; it is not SSOT authority, seed adoption, proof completion, or implemented status evidence. The tool does not write seed SQL, manifests, SSOT, TODO, or roadmap files and does not connect to a DB, external API, or AI API. Generated local artifacts matching `topology-seed-discussion*.tmp.json`, `topology-seed-discussion*.seed.json`, or `.agent/tmp/topology-seed-discussion*.json` are ignored; use `.agent/scripts/cleanup-topology-seed-discussion-artifacts.sh` to remove them without deleting tracked fixtures.

Examples:

```sh
.agent/tools/topology-seed-discussion inspect
.agent/tools/topology-seed-discussion inspect --space admin_ui_builder_authoring
.agent/tools/topology-seed-discussion expand --answers /tmp/stage1.json
.agent/tools/topology-seed-discussion build-template --space admin_ui_builder_authoring --bits '[1,1,0,1]' > /tmp/topology-seed-discussion.tmp.json
.agent/tools/topology-seed-discussion build --answers /tmp/topology-seed-discussion.tmp.json
```

## Agent UI protocol tools

`agent-ui-initial-contract` and `agent-ui-local-test` implement the Agent UI protocol's `initial_contract` and `local_test` steps from `docs/governance/agent-ui-protocol-ssot.yaml`. They are governance/tool-side tools, not ordinary observation tools: the SSOT and this task scope explicitly permit them to write `senario-tmp.md` and append to `docs/governance/logs/tool.log`, but they still do not mutate Topolactor application/runtime/product source, and they do not expose an `--output` mutation escape hatch.

### `agent-ui-initial-contract`

- `worktypes`: lists canonical worktype ids with their routed prompt path and required checks (from `.agent/routes/worktype-required-protocols.yaml`).
- `start --task-name <name> --worktype <worktype>`: resolves the worktype route and emits tool-generated `uuid`/`datetime` plus a short prompt excerpt and protocol trigger hints. The AI must reuse these tool-generated values verbatim in later steps rather than hand-authoring them.
- `resolve-ssot --target <name>`: resolves a target SSOT name to a repo-relative path and lists its top-level sections (delegates to `yaml-section-query` for the actual read).
- `sections --file <path> --select '["a","b"]'`: outputs only the selected section subtrees (delegates to `yaml-section-query --section` per selected key).
- `end --task-name ... --worktype ... --uuid ... --datetime ... --target-file ... --senario-summary ... [--ng-boundary ...]`: requires a senario contract, writes `senario-tmp.md` from the reference template, and appends the compact usage record to `docs/governance/logs/tool.log`.

### `agent-ui-local-test`

- `run-worktype-tests --worktype <worktype>`: runs the `required_checks` routed for a worktype and summarizes pass/fail with a bounded tail, not raw logs.
- `read-senario-tmp`: outputs `senario-tmp.md`, or an explicit Error if it cannot be checked.
- `checklist [--files <a,b>]`: lists existing checklist interview item headings (default: `policy-judgment.md`, `boundary-identity.md`); it does not evaluate free-form checklist answers itself.
- `checks`: runs `.agent/tests/check-structure.sh` and summarizes pass/fail.
- `summary --task-name ... --worktype ... --uuid ... --datetime ...`: runs worktype tests + structure check + senario-tmp presence and emits a compact `pass_or_fail` completion summary. `pass_or_fail` reflects required-check pass only; it is not an implemented/partial/not_started judgment.

Examples:

```sh
.agent/tools/agent-ui-initial-contract worktypes
.agent/tools/agent-ui-initial-contract start --task-name "fix-x" --worktype existing_pr_update
.agent/tools/agent-ui-local-test checks
.agent/tools/agent-ui-local-test summary --task-name "fix-x" --worktype existing_pr_update --uuid <uuid> --datetime <datetime>
```

## Prohibited uses

Do not use ordinary observation `.agent/tools` to:

- write Topolactor application/runtime/product source files;
- write seed SQL, manifests, SSOTs, TODO, or roadmap files without explicit tool/governance SSOT scope;
- connect to a DB, external API, or AI API;
- bypass `.agent/scripts` / `.agent/tests` gate implementations;
- claim proof passed, completion, implemented, partial, or not_started status;
- treat todo or roadmap text as proof;
- replace required SSOT reads, protocol judgment, semantic audit, or structure checks.

## Proof / structure gate connectivity

The existing read-only observation tools' executable permission, thin entrypoint boundary, mutation-argument rejection, and no-authority-claim baseline output are gated by `.agent/tests/check-agent-tools-surface.sh` (structured verification delegated to `.agent/scripts/check_agent_tools_surface.py`), which is called as a delegated subcheck from `.agent/tests/check-structure.sh`. `.agent/tools`, `.agent/scripts/agent_tools/`, and the dedicated checker are enumerated in `.agent/docs/required-paths.yaml`. The connection is recorded in `.agent/docs/test-bundles.yaml` under the `agent-tools-proof-and-structure-gate` bundle against the `ssot_structure_policy_contract` proof edge in `docs/design/test-proof-manifest-ssot.yaml` (whose `required_when.changed_files` already covers `.agent/**`). This dedicated check verifies the existing observation tools' own structural / read-only / no-authority contract only; it does not become SSOT authority, proof completion, or completion judgment for anything `.agent/tools` observes.

## Advanced surface maps: deferred scope

The child bundle `agent-tools-advanced-surface-maps` implemented `yaml-section-query` (this PR) instead of the originally-listed candidates `change-impact-map`, `dependency-surface-map`, and `orphan-surface-map`. Those three remain **not implemented** and are not scheduled by this bundle: they tended toward a thin grep/rg wrapper and require separate schema and judgment-boundary design (impact/dependency/orphan analysis is semantic-judgment-adjacent in a way a generic section reader is not) before any future implementation.
