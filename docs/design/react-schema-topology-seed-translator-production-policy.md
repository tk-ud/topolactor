# React Schema Topology Seed Translator — Production Policy

Status: non-SSOT implementation guidance
Authority: this file has no contract authority; the exchange contract is
`docs/design/react-schema-topology-seed-translator-ssot.yaml`

## Purpose

This file holds the build-method, CLI shape, and rollout notes for the next-Bundle
Python3 translator tool, so that content never leaks into the SSOT YAML. The SSOT
owns *what* the input/output shapes and exchange mapping are; this file owns *how*
a future Bundle intends to build a tool around them.

## Current implementation status

- `design_change` Bundle: produced the SSOT contract only (no code). Done.
- `implementation_change` Bundle (credential-management-0092): implemented
  `generate-react-schema`, proven against the
  `auth.external.credential_management.projection` fixture
  (`manifest.manifest_id = 00000000-0000-0000-0000-000000000092`). Done.
- Follow-up `implementation_change` Bundle: implemented `generate-topology-seed`,
  converting that same fixture's own `generate-react-schema` output into a
  `topology_ui_seed_contract` candidate (identity/source/authority preserved,
  loss/gap reporting populated). Still draft/intake only -- no active seed
  write, no `RuntimeComponentSpec` generation. Done.
  `round-trip-check` is still deliberately `not_implemented_out_of_scope`
  (fail-closed, not silently accepted).
- No docker compose service, nginx route, or C# call boundary exists yet.
  Everything under "Deferred" below is still forward-looking guidance.

## Work boundary

### OK

- Python3 tool that runs from a repo checkout without starting backend, frontend,
  nginx, or a database. (Implemented: `.agent/tools/react-schema-topology-seed-translator`.)
- Tool reads `docs/design/react-schema-topology-seed-translator-ssot.yaml` and the
  caller-supplied input envelope JSON, never `db/*.sql`.
- Tool implements `input_format_contract` -> `input_text_markup_grammar_contract` ->
  `text_decomposition_contract` -> `react_schema_contract` -> `output_format_contract`
  from the SSOT for `generate-react-schema`.
- Tool implements `exchange_mapping.schema_to_seed_record_mapping` ->
  `topology_ui_seed_contract` -> `output_format_contract` for
  `generate-topology-seed`. Its `inputText` is a JSON string of a
  `topolactor.react_schema.v1` candidate (see `input_format_contract.mode_vocabulary`),
  not markup text. The supplied schema is re-validated against
  `wiring_lane_contract`/`ui_catalog_boundary_contract`/structural rules before
  conversion -- it is never trusted blindly, even if it came from this
  translator's own `generate-react-schema` output.
- Tool classifies every action/step `eventBinding` into exactly one
  `wiring_lane_contract` lane, and resolves every componentKind/style ref through
  `ui_catalog_boundary_contract` before treating a node as valid.
- Tool validates every generated node/record against `validation_rules`.
- If the caller's input envelope carries a pre-resolved `seedEvidence` object
  (produced by a test/proof/evidence-verification step, not by the translator),
  the tool passes it through to the output unchanged after a shape check.
- `projection_render_exchange_contract` is still a forward-looking boundary for
  a future `RuntimeComponentSpec` candidate stage; not implemented yet.
- C# may call the tool later through a bounded subprocess or a local compose
  service, treating tool output as a candidate, never as semantic authority.

### NG (always)

- Expanding `SeedRuntime` / `SeedAdmin` into this translator.
- Making a generated schema or seed active topology.
- Writing to the database from the translator.
- Treating translator output as runtime policy authority.
- Allowing arbitrary React execution in a generated schema.
- Bypassing UIBuilder preview / validate / apply.
- Routing around backend authority for apply/approval.
- **Reading `db/*.sql` from inside the translator body.** Seed evidence
  (e.g. resolving which `manifest.manifest_id` backs a declared seed surface,
  and ruling out unrelated same-UUID rows in other tables such as
  `topology.structure_maps`) is a test/proof/evidence-verification concern.
  It belongs in the paired `check-*.sh`/`check_*.py` for a given fixture
  (see `.agent/scripts/check_react_schema_topology_seed_translator.py`
  `resolve_seed_evidence_from_seed_file`), never inside
  `.agent/scripts/react_schema_topology_seed_translator.py` itself. The
  translator only ever passes through an already-resolved `seedEvidence`
  object from the input envelope.
- Putting `db/*.sql` paths inside `sourceYamlRefs` on any React schema node.
  `sourceYamlRefs` is SSOT/YAML-facing evidence only; seed-file evidence
  belongs exclusively under `seedEvidence`.

## Implemented file layout

```text
.agent/tools/react-schema-topology-seed-translator
.agent/scripts/react_schema_topology_seed_translator.py
.agent/tests/check-react-schema-topology-seed-translator.sh
.agent/scripts/check_react_schema_topology_seed_translator.py
.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.input.json
.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.topology-seed.input.json
tools/generate/schema/translated.json                (committed evidence artifact: generate-react-schema)
tools/generate/schema/translated-topology-seed.json  (committed evidence artifact: generate-topology-seed)
docs/design/react-schema-topology-seed-translator-ssot.yaml
```

## Implemented CLI shape

```text
react-schema-topology-seed-translator generate-react-schema  --input <envelope.json> [--output <path>] [--scenario-uuid <uuid>]
react-schema-topology-seed-translator generate-topology-seed --input <envelope.json> [--output <path>] [--scenario-uuid <uuid>]
react-schema-topology-seed-translator round-trip-check       --input <envelope.json>   # fails closed: not_implemented_out_of_scope
```

`--target-surface` is not a separate CLI flag: `targetSurface` lives in the
input envelope JSON (`input_format_contract.required_fields.targetSurface`),
alongside `mode`, `inputText`, `sourceYamlRefs`, and the optional
`seedEvidence` passthrough object. Each subcommand maps to one
`input_format_contract.mode` value.

## Output location

`--output` writes the full `output_format_contract`-shaped document (plus the
`scenario` and `seedEvidence` extension fields) to a repo-relative path;
paths outside the repository root are rejected. `tools/generate/schema/translated.json`
is the committed evidence snapshot for the credential-management-0092
fixture. Do not commit further generated outputs unless a task explicitly
asks for an evidence snapshot.

## Fixture order

```text
1. auth.external.credential_management.projection   (categories + four forms + admin_approve lifecycle) -- done
2. hub_search.readonly.v1                           (smallest layout_tree, few gaps) -- not started
3. physical_search_crud_aggregate.v1                (form + workflow coverage) -- not started
```

## Compose / C# / nginx direction (deferred)

None of these exist yet and none are in scope until an owner explicitly asks for them:

- docker compose: only after the offline CLI is fully proven; read-only `docs/design` mount,
  generated-output mount, no DB credentials required, not part of any healthcheck.
- C# call boundary: subprocess with explicit input/output paths, or a local
  compose HTTP call; output is always a candidate, never semantic authority.
- nginx: local/demo tool route only (e.g. `/tools/react-schema-translator/`),
  never public runtime authority.

## Completion check

OK:

```text
- tool runs without starting backend/frontend/nginx/a database
- tool reads only SSOT YAML plus the caller-supplied input envelope; never db/*.sql
- every generated node/record carries sourceYamlRefs, and sourceYamlRefs never contains a db/*.sql path
- pre-supplied seedEvidence passes through unchanged; the translator never resolves it itself
- unresolved mappings are explicit knownGapRefs, never silently dropped
- validation_rules failures surface as validationErrors, not thrown exceptions that hide state
```

NG:

```text
- generated schema/seed lacks source evidence
- generated schema/seed silently invents a component key or catalog identity
- tool requires backend/frontend/nginx/DB to run generate-react-schema
- output is treated as active topology or callable execution authority
- translator body opens db/*.sql for any reason
```
