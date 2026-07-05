# React Schema Topology Seed Translator — Production Policy

Status: non-SSOT implementation guidance
Authority: this file has no contract authority; the exchange contract is
`docs/design/react-schema-topology-seed-translator-ssot.yaml`

## Purpose

This file holds the build-method, CLI shape, and rollout notes for the next-Bundle
Python3 translator tool, so that content never leaks into the SSOT YAML. The SSOT
owns *what* the input/output shapes and exchange mapping are; this file owns *how*
a future Bundle intends to build a tool around them.

## Scope for this Bundle

This Bundle (`design_change`) produced the SSOT contract only. No Python code,
docker compose service, nginx route, or C# call boundary exists yet. Everything
below is forward-looking guidance for the Bundle that implements the tool.

## Work boundary

### OK (future implementation Bundle)

- Python3 tool that runs from a repo checkout without starting backend, frontend,
  nginx, or a database.
- Tool reads `docs/design/react-schema-topology-seed-translator-ssot.yaml` and the
  other SSOT files it references, never `db/*.sql`.
- Tool implements `input_format_contract` -> `text_decomposition_contract` ->
  `react_schema_contract` / `topology_ui_seed_contract` -> `output_format_contract`
  from the SSOT.
- Tool validates every generated node/record against `validation_rules`.
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
- Reading `db/*.sql` as translator input.

## Candidate file layout (future Bundle)

```text
.agent/tools/react-schema-topology-seed-translator
.agent/scripts/react_schema_topology_seed_translator.py
docs/design/react-schema-topology-seed-translator-ssot.yaml   (already exists; this Bundle's deliverable)
```

Names may change to fit repository conventions at implementation time.

## Candidate CLI shape

```text
react-schema-topology-seed-translator generate-react-schema   --input <inputText/file> --target-surface <key>
react-schema-topology-seed-translator generate-topology-seed  --input <reactSchemaCandidate.json>
react-schema-topology-seed-translator round-trip-check        --input <topologyUiSeedCandidate.json>
```

Each subcommand maps to one `input_format_contract.mode` value.

## Candidate output location

```text
.agent/generated/react-schema-topology-seed/react-schema-candidate.json
.agent/generated/react-schema-topology-seed/topology-ui-seed-candidate.json
.agent/generated/react-schema-topology-seed/translator-output.json
```

Generated outputs are evidence artifacts. Do not commit them unless a task
explicitly asks for an evidence snapshot.

## Suggested fixture order for first implementation

```text
1. hub_search.readonly.v1                              (smallest layout_tree, few gaps)
2. physical_search_crud_aggregate.v1                    (form + workflow coverage)
3. auth.external.credential_management.projection        (category + admin_approve coverage)
```

## Compose / C# / nginx direction (deferred)

None of these exist yet and none are in scope for the SSOT-completion Bundle:

- docker compose: only after the offline CLI works; read-only `docs/design` mount,
  generated-output mount, no DB credentials required, not part of any healthcheck.
- C# call boundary: subprocess with explicit input/output paths, or a local
  compose HTTP call; output is always a candidate, never semantic authority.
- nginx: local/demo tool route only (e.g. `/tools/react-schema-translator/`),
  never public runtime authority.

## Completion check for the first implementation attempt

OK:

```text
- tool runs without starting backend/frontend/nginx/a database
- tool reads only SSOT YAML (this file's sibling contract plus its refs)
- every generated node/record carries sourceYamlRefs
- unresolved mappings are explicit knownGapRefs, never silently dropped
- validation_rules failures surface as validationErrors, not thrown exceptions that hide state
```

NG:

```text
- generated schema/seed lacks source evidence
- generated schema/seed silently invents a component key or catalog identity
- tool requires backend/frontend/nginx/DB to run generate-react-schema
- output is treated as active topology or callable execution authority
```
