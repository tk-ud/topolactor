# react-schema-topology-seed-translator Tool Chain — Usage Report (2026-07-06 — Completed)

## Scope

- Task: `credential_management_ui_seed_rebuild` (PR #573)
- Tools exercised: `directory-map`, `yaml-section-query`, `agent-ui-initial-contract`,
  `agent-ui-local-test`, `topology-seed-discussion translator-entry-gate`,
  `react-schema-topology-seed-translator generate-react-schema` /
  `generate-topology-seed`

## What worked well

- `agent-ui-initial-contract start` inlining the routed prompt + triggered
  protocol full text + `workflow_procedure` in one call was a real time
  saver — did not need to separately open `.agent/prompt/*`,
  `.agent/protocols/*`, or `.agent/skills/agent-workflow.md`.
- `yaml-section-query --list-sections` / `--path` on the large SSOT yamls
  (`instance-port-substrate-ssot.yaml`, `react-schema-topology-seed-translator-ssot.yaml`)
  let me navigate to `existing_credential_management_projection_extension`,
  `completion_gate`, `declared_seed_surface_catalog`, `wiring_lane_contract`,
  etc. directly instead of reading multi-hundred-line files whole.
- The pre-authored fixtures
  (`.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092*.input.json`)
  were exactly the right input for this bundle and meant the `inputText`
  markup did not need to be hand-authored from scratch.
- `generate-react-schema` / `generate-topology-seed` both passed with
  `validationErrorCount: 0` on the first run and produced a fully
  SSOT-cross-referenced (`sourceYamlRefs` on every node), lane-classified
  (`wiringLane` + `authorityMarker` matching `wiring_lane_contract`) record
  set — this is genuinely useful authoring output, not boilerplate.
- `translator-entry-gate` and the executable proofs
  (`check-react-schema-topology-seed-translator.sh`,
  `check-schema-seed-translator-entry-gate.sh`) gave fast, unambiguous
  pass/fail signal.

## Friction / issues found

1. **`agent-ui-initial-contract resolve-ssot --target <name>` has an
   opaque/undiscoverable target-name vocabulary.** Calling it with
   `--target instance-port-substrate` (a reasonable guess derived from the
   SSOT filename) returned `not_found` with no candidate list or hint of
   what names are valid. Had to bypass it and call
   `yaml-section-query --file docs/design/instance-port-substrate-ssot.yaml`
   directly. Minor, but the tool advertises itself as the entry point for
   this step and silently degrades to "not found" instead of suggesting
   `.agent/docs/ssot-map.yaml`'s `work_type` names or similar.

2. **(Real, cost-generating issue) The translator's output shape is a
   nested tree, but the seed's actual storage (`manifest.topology jsonb[]`)
   has a GIN index (`idx_manifest_topology`, defined in
   `db/manifest_tables.sql`) that treats each array element as one
   indexable item, capped at Postgres's page-derived ~2712 byte limit.**
   Nothing in `react-schema-topology-seed-translator-ssot.yaml`
   (`output_format_contract`, `topology_ui_seed_contract`,
   `declared_seed_surface_catalog`, `generated_artifact_operation_policy`)
   or the tool's own validation rules mentions this constraint or warns
   that a `topologyUiSeedCandidate.projections` tree, if embedded as a
   single jsonb array element, may exceed it. In this task, the generated
   candidate for `auth.external.credential_management.projection` was one
   coherent nested tree (~17KB) — I embedded it as a single new manifest
   topology array element, which passed the translator's own
   `validation_rules` (0 errors) but broke real Postgres INSERT during
   `bootstrap-sql-validation` / `db-schema-check` / `backend-tests` CI with
   `index row size 3776 exceeds maximum 2712 for index "idx_manifest_topology"`.
   I had to diagnose this from CI job logs, locate `idx_manifest_topology`,
   and manually flatten the tree into 35 small `topology_ui_seed_record`
   entries (one per projection/category/section/form/field/action node,
   each carrying a `parentKey`), then verify the fix against a real local
   PostgreSQL 16 instance.

   **Suggested fix (not made in this PR — out of scope for an
   `implementation_change` PR to alter the translator/SSOT itself):**
   - Add a `validation_rules` entry to
     `react-schema-topology-seed-translator-ssot.yaml` (e.g. under
     `topology_ui_seed_validation`) requiring every emitted
     `topologyUiSeedCandidate` record — or the whole `projections` tree if
     adopted as one array element — to be checked against a documented
     per-array-element byte budget for `manifest.topology`'s GIN index
     (with the actual Postgres limit and `idx_manifest_topology`'s
     definition cited as the source of truth).
   - Optionally have `generate-topology-seed` itself emit a `warning`
     (not necessarily `blocking`) `validationErrors` entry when a
     candidate node's serialized size exceeds that budget, so this is
     caught at generation time instead of at DB INSERT time in CI.

## Net assessment

Convenient overall — the fixture + translator + gate chain removed real
authoring risk (no hand-invented UI structure, full SSOT traceability per
node) and the executable proofs are trustworthy. The one real gap is #2
above: the tool chain's own contract does not currently protect the
seed-adoption step against the DB's actual storage/index constraints, so an
agent following the tool chain correctly can still produce a
translator-validated-but-DB-breaking seed diff. Recommend closing that gap
in a future `design_change` PR to the translator SSOT.

## Required Check Scope Declaration

| Check | Status |
|---|---|
| SCENARIO_CONTRACT | NOT_REQUIRED (inspection report, no runtime/persistence change) |
| BOUNDARY_MATRIX | NOT_REQUIRED |
| POLICY_JUDGMENT | NOT_REQUIRED |

## Action Taken

- Reported friction points + the index-size gap in PR #573's comment thread.
- No `.agent/tasks/todo.md` entry added: the suggested fix belongs to a
  future `design_change` bundle against
  `docs/design/react-schema-topology-seed-translator-ssot.yaml`, and its
  bundle boundary is not yet fixed — noted here as a follow-up
  investigation item per `todo_granularity_guard` instead of a canonical
  TODO.
