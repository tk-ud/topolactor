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
- Follow-up `implementation_change` Bundle (PR572): extracted the schema<->seed
  translator entry gate core (`schema_seed_translator_entry_gate.py`,
  `validate_translator_entry()`) and wired it into this translator's entry
  (`generate-react-schema` / `generate-topology-seed` both still implemented;
  a non-`pass` `gateStatus` now fails the entry closed before conversion
  runs) and into `topology-seed-discussion` (`translator-entry-gate`
  subcommand). The gate core is translator-entry-gate authority only -- it
  is not SSOT authority, not proof completion, and not seed adoption
  authority; see `translator_input_authority`/`declared_seed_surface_catalog`
  above for those. `round-trip-check` remains deliberately
  `not_implemented_out_of_scope`. Done.
- Follow-up `implementation_change` Bundle (PR572 second follow-up): stopped
  treating generated JSON output as tracked evidence. `*seed.sql` files and
  the SSOT docs above remain the production storage authority; generated
  JSON is a local/tmp projection only (`.agent/tools/generated/*`,
  gitignored). `.agent/tools/logs/generate.log` is the tracked JSON
  Lines regeneration-trace index instead (one record per generation
  attempt), added via new `--generate-log`/`--nametag`/`--task-ref`/
  `--pr-ref`/`--source-seed-sql` CLI options on `generate-react-schema` /
  `generate-topology-seed` (opt-in only; ordinary test runs never touch the
  tracked log). Reverse generation (seed -> schema) is still not
  implemented and out of scope; `round-trip-check` remains
  `not_implemented_out_of_scope`. Done.

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
.agent/tests/fixtures/react-schema-topology-seed-translator/physical-search-crud-aggregate.react-schema.json
.agent/tests/fixtures/react-schema-topology-seed-translator/physical-search-crud-aggregate.topology-seed.input.json
.agent/tools/logs/generate.log                                 (tracked JSON Lines regeneration-trace evidence; not seed adoption authority, not proof completion)
.agent/tools/generated/*                                            (gitignored local/tmp regeneration output; never tracked, never SSOT authority)
.agent/scripts/agent_tools/schema_seed_translator_entry_gate.py    (schema<->seed translator entry gate core; translator-entry-gate authority only)
.agent/scripts/check_schema_seed_translator_entry_gate.py          (gate core executable proof)
.agent/tests/check-schema-seed-translator-entry-gate.sh            (gate core CI entrypoint)
.agent/tools/topology-seed-discussion translator-entry-gate        (external, read-only gate core caller)
docs/design/react-schema-topology-seed-translator-ssot.yaml
```

Production storage authority for anything this translator's output might
eventually inform stays with `*seed.sql` files and the SSOT docs above --
never with generated JSON. `.agent/tools/generated/*` is local/tmp
regeneration output only (gitignored, recreated on demand); it is never
tracked and never a substitute for `*seed.sql`/SSOT authority.
`.agent/tools/logs/generate.log` is the tracked trace instead: a JSON
Lines file, one record per generation attempt, naming what was generated
from what (`source`, `sourceSeedSql`, `seedKey`, `manifestId`), with what
command, and its output hash (`sha256`), so a proof can re-run the same
command and hash-check the result without the generated JSON itself being
committed. `generate.log` is trace evidence only -- it is not seed adoption
authority and not proof completion by itself; a proof treats it as a
regeneration index, re-running recorded commands into
`.agent/tools/generated/*` or `.agent/tmp/*` and checking hash consistency
when it needs the actual generated content. Seed -> schema reverse
generation is still not implemented; see `round-trip-check` below.

## Implemented CLI shape

```text
react-schema-topology-seed-translator generate-react-schema  --input <envelope.json> [--output <path>] [--scenario-uuid <uuid>] [--generate-log <path>] [--nametag <name>] [--task-ref <ref>] [--pr-ref <ref>] [--source-seed-sql <label>]
react-schema-topology-seed-translator generate-topology-seed --input <envelope.json> [--output <path>] [--scenario-uuid <uuid>] [--generate-log <path>] [--nametag <name>] [--task-ref <ref>] [--pr-ref <ref>] [--source-seed-sql <label>]
react-schema-topology-seed-translator round-trip-check       --input <envelope.json>   # fails closed: not_implemented_out_of_scope
```

`--target-surface` is not a separate CLI flag: `targetSurface` lives in the
input envelope JSON (`input_format_contract.required_fields.targetSurface`),
alongside `mode`, `inputText`, `sourceYamlRefs`, and the optional
`seedEvidence` passthrough object. Each subcommand maps to one
`input_format_contract.mode` value.

`--generate-log <path>` is opt-in: when omitted (the default for ordinary
test/CI runs), nothing is appended anywhere, so the tracked
`.agent/tools/logs/generate.log` is never touched by routine
invocations. When given, the translator appends one JSON Lines record to
that path after writing its own output: `datetime`, `nametag` (defaults to
the `--input` file stem), `mode`, `source` (the `--input` path),
`sourceSeedSql` (a caller-supplied passthrough label only -- the translator
never opens it), `seedKey`, `manifestId` (from a passed-through
`seedEvidence.screenUuid`, when present), `command`, `outputKind`
(always `translator_output_document` -- `sha256` always hashes the full
`output_format_contract`-shaped document `--output` writes/emits, never a
bare candidate by itself), `outputSchemaId` (`topolactor.translator_output.v1`),
`embeddedCandidateKind` (`react_schema_candidate` or
`topology_ui_seed_candidate` -- which candidate the hashed document
embeds, not a separate hashed artifact), `outputPath`, `sha256`,
`gateStatus`, `validationErrorCount`, `unresolvedGapCount`, `taskRef`, and
`prRef`. `--source-seed-sql`, `--task-ref`, and `--pr-ref` are free-form
passthrough labels only; none of them cause the translator to read anything
beyond its existing
`--input`/SSOT-YAML boundary.

## Output location

`--output` writes the full `output_format_contract`-shaped document (plus the
`scenario` and `seedEvidence` extension fields) to a repo-relative path;
paths outside the repository root are rejected. Generated output is not
committed: `.agent/tools/generated/*` is gitignored local/tmp regeneration
output, regenerated on demand from a `.agent/tools/logs/generate.log`
record's `command` and `source`, never a tracked evidence snapshot.

## Fixture order

```text
1. auth.external.credential_management.projection   (categories + four forms + admin_approve lifecycle) -- done
2. physical_search_crud_aggregate.v1                (canonical SPA CRUD schema fixture / schema->seed translation evidence) -- done
3. hub_search.readonly.v1                           (smallest layout_tree, few gaps) -- not started
```

`physical_search_crud_aggregate.v1`'s canonical schema fixture
(`.agent/tests/fixtures/react-schema-topology-seed-translator/physical-search-crud-aggregate.react-schema.json`)
is a standalone `topolactor.react_schema.v1` document, independent of its
topology-seed input envelope. The envelope's `inputText` is a JSON-string
copy of that same fixture, and a sync check
(`check_react_schema_topology_seed_translator.py`) asserts
`json.loads(envelope.inputText) == schema fixture` so the two never drift
apart silently. Modal/card-list UI concepts are expressed through existing
node fields (`Section.sectionKind`, `Form.mode`, `Table.display`) rather than
new react_schema node kinds.

### Seed-first proof layering (physical_search_crud_aggregate.v1)

This fixture is not an independently-invented schema: its node keys, wiring
targets, and known gaps are pulled directly from the already-registered
`db/physical_search_crud_aggregate_preset_seed.sql` preset seed's compile
snapshot, following `docs/design/ui-builder-seed-first-gap-discovery-ssot.yaml`'s
methodology of using existing seed/catalog content first and declaring genuine
gaps explicitly rather than inventing unconnected content. The proof chain is:

```text
existing seed-first preset seed / compile snapshot (db/physical_search_crud_aggregate_preset_seed.sql,
topology.mock_preset_registry + topology.mock_preset_compile_snapshot)
  -> physical-search-crud-aggregate React schema candidate
     (.agent/tests/fixtures/.../physical-search-crud-aggregate.react-schema.json)
  -> topology-seed input envelope
     (.agent/tests/fixtures/.../physical-search-crud-aggregate.topology-seed.input.json)
  -> generated topology seed candidate
     (regenerate on demand: .agent/tools/logs/generate.log records the
     command/source/sha256 for this candidate; the JSON body itself is
     .agent/tools/generated/* local/tmp output, not tracked)
```

Concretely: `crud_search_button`/`crud_submit_button`/`crud_result_list` node
keys and their `content_bundle:search`/`content_bundle:create_entity_draft`/
`content_bundle:get_entity` wiring targets are taken verbatim from the real
seed's `wiring_candidate_json`; `crud_status_filter` and `crud_submit_button`
carry the same `knownGapRef`s as the real seed's `unresolved_json`
(`enum_status_select_options_from_content_bundle_list_states`,
`form_field_values_to_create_entity_draft_payload`); `crud_add_button`
deliberately has no `contents_api_wiring` binding because the real seed's
compile snapshot has no wiring candidate for it either (an honest
`ssot_ambiguity_gap`, not an invented one); and `crud_result_list`'s
`item.click -> content_bundle:get_entity` wiring in the real seed cannot yet
be expressed by this SSOT's `Table` node kind (no `eventBinding` field), so it
surfaces as the `runtime_dispatch_or_projection_gap:
table_item_click_wiring_not_yet_expressible_in_react_schema_contract` known
gap instead of being silently dropped. `check_react_schema_topology_seed_translator.py`
reads `db/physical_search_crud_aggregate_preset_seed.sql`'s compile snapshot
directly (test/proof layer only, mirroring
`frontend/tests/presetSeedLineContract.test.ts`'s `extractCompileSnapshot()`
contracts) and cross-references it against the fixture; the translator body
itself never reads `db/*.sql`. There is no generic, surface-independent UI
composition template fixture -- every fixture here traces to one declared,
already-seeded surface.

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
