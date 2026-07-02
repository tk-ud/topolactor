# Agent Output Noise Control Report

Status: audit_inventory
Bundle: `agent-output-noise-control`
Date: 2026-07-03

## Judgment

`implementation_change` に進める。

先行 `design_change` は不要。現行 todo の問題点、目的、改善方針、対象資料、対象ファイル、OK/NG 軸は Bundle 単位で成立している。

この report は調査中の一時 surface であり、Bundle 実装完了時に削除する。

## Coverage Boundary

Tree-equivalent surface inventory was checked for:

- `.agent/tests/*.sh`: GitHub code search `path:.agent/tests extension:sh`
- `.agent/scripts/**/*`: GitHub code search `path:.agent/scripts`
- `.agent/tools/*`: GitHub code search `path:.agent/tools`
- `.github/workflows/*.yml`: GitHub code search `path:.github/workflows extension:yml`
- `.agent/docs/required-paths.yaml`
- `.agent/docs/test-bundles.yaml`
- workflow references to tests/scripts

Observed surface count:

- `.agent/tests/*.sh`: 34 files
- `.agent/scripts/**/*`: 18 files
- `.agent/tools/*`: 5 files
- `.github/workflows/*.yml`: 9 files

This report lists **implementation target files only**. Checked files whose success output is already short enough, whose output is explicit artifact output, or whose failure path must remain operationally verbose are not listed as implementation targets unless they affect normal Agent / CI success paths.

Explicit non-target after tree-equivalent check:
- `.agent/tests/check-static-ssot-purity.sh`: success output is one line; failure path prints forbidden pattern and matching lines.
- `.agent/tests/check-test-proof-manifest-integrity.sh`: thin wrapper / compact pass summary.
- `.agent/tests/check-agent-tools-surface.sh`: thin wrapper; target is `.agent/scripts/check_agent_tools_surface.py`.
- `.agent/tests/check-ssot-vocabulary-contract.sh`: thin wrapper; target is `.agent/scripts/check_ssot_vocabulary_contract.py`.
- `.agent/tests/check-ssot-proof-surface-connectivity.sh`: thin wrapper / compact summary.
- `.agent/tests/check-system-roadmap.sh`: thin wrapper; target is `.agent/scripts/check_system_roadmap.py`.
- `.agent/scripts/lib/minimal_yaml.py`: library only; no normal stdout surface.
- `.agent/scripts/create-tmp.sh`, `.agent/scripts/delete-tmp.sh`, `.agent/scripts/cleanup-topology-seed-discussion-artifacts.sh`: explicit local cleanup/create helpers; success output is bounded and operational.
- `.agent/scripts/advance-workflow-phase.sh`: deprecated optional local memo; output is bounded and not CI/proof authority.
- `.agent/scripts/pre-tool-edit-guard.sh`: hook reminder output is intentional and bounded; do not route it into proof/completion output.
- `.agent/scripts/bootstrap-local-tools.sh`, `.agent/scripts/bootstrap-local-postgres.sh`: opt-in local bootstrap; operational install/docker output is not normal CI success proof. Keep failure detail if touched separately.
- `.agent/tools/README.md`: documentation only.
- `.agent/tools/directory-map`, `.agent/tools/proof-surface-map`, `.agent/tools/yaml-section-query`, `.agent/tools/topology-seed-discussion`: explicit read-only observation entrypoints, not success logs. Do not suppress explicit user/tool output; inspect only default/no-selector large-output behavior through `.agent/scripts/agent_tools/readonly_observation.py`.
- `.github/workflows/structure-check.yml`: delegates to target scripts; fix underlying scripts, keep `check-structure.sh` last.
- `.github/workflows/frontend-types.yml`, `.github/workflows/default-entity-search.yml`, `.github/workflows/runtime-semantics.yml`: delegate to target test scripts; no direct workflow change needed unless script routing changes.
- `.github/workflows/unified-test-gate.yml`: delegates to target orchestrator/scripts; no direct workflow change needed unless lane routing changes.

## Governance Boundary

- SSOT / governance:
  - `docs/framework-policy.yaml`
  - `.agent/protocols/audit.md`
  - `.agent/protocols/implementation-change.md`
  - `.agent/docs/test-bundles.yaml`
  - `docs/design/test-proof-manifest-ssot.yaml`
- Do not add `jq`, `node`, `ruby`, `pip`, `npm`, `gem` dependency.
- Keep Python3 stdlib only / bash orchestration only for `.agent` tooling.
- Do not silence success output by blindly redirecting to `/dev/null` if failure evidence would also be hidden.
- Preserve failure details: missing file, missing term, command, stdout/stderr, response body, docker logs, SQL failure context.
- `.agent/tools` output remains read-only observation only. It is not SSOT authority, proof completion, or completion judgment.
- Do not return scope to `agent-readonly-repo-observation-tools-surface`.

## Success Output Policy

Successful checks should emit one short structured summary, for example:

```text
PASS check-structure dirs=9 files=167 content_terms=284 subchecks=7
```

Long-running checks may emit bounded progress lines, for example:

```text
processing unified-test-gate lane=backend-tests 40%
```

Failure paths must emit enough detail to reproduce and classify the failure. Success minimization must not remove error evidence.

## Implementation Target Files

### P0: structural / orchestrator success flood

#### `.agent/tests/check-structure.sh`

Reason:
- Prints `OK` per directory, required file, required content term, delegated subcheck, and guidance line.

Required change:
- Replace per-item success output with one structured PASS summary.
- Keep exact missing directory/file/content term details on failure.
- Keep delegated subcheck failure output.
- Keep this check last.

#### `.agent/tests/check-local-ci.sh`

Reason:
- Captures child output but echoes full child output on success.

Required change:
- Capture child output.
- On success, print one summary per child check.
- On failure, replay captured child output and exit non-zero.

#### `.agent/tests/check-unified-test-gate.sh`

Reason:
- Orchestrates multiple lanes and lets lower dotnet / deno / bash output pass through on success.

Required change:
- Capture each lane.
- Print bounded progress or one lane summary on success.
- Replay lane stdout/stderr on failure.
- Preserve lane identity, command, and exit code.

#### `.agent/scripts/check_agent_tools_surface.py`

Reason:
- Prints multiple OK lines per tool and per contract axis.

Required change:
- Aggregate successful contract checks into one short PASS summary.
- Preserve per-tool failure details.
- Preserve no-mutation / no-authority / output-shape diagnostics.

### P1: per-term / per-section OK loops

#### `.agent/tests/check-completion-judgment.sh`

Required change:
- Summary count on success.
- Missing term/file detail on failure.

#### `.agent/tests/check-worktype-routing.sh`

Required change:
- Summary by route category on success.
- Missing reference/term detail on failure.

#### `.agent/tests/check-runtime-bundle-ssots.sh`

Required change:
- Summary by SSOT phase on success.
- Preserve forbidden-pattern hit detail on failure.

#### `.agent/tests/check-cli-mcp-port-implementation-ssot.sh`

Required change:
- Summary by proof category on success.
- Preserve exact missing path/term on failure.

#### `.agent/tests/check-docs-ssot-connectivity.sh`

Required change:
- Summary counts on success.
- Preserve unconnected/orphan path detail on failure.

#### `.agent/tests/check-sql-attention-ssot.sh`

Required change:
- Summary by semantic/SQL/proof lane on success.
- Preserve missing term and forbidden-hit detail on failure.

#### `.agent/tests/check-pipeline-continuity.sh`

Reason:
- Prints lane headers, many OK lines, roadmap entry OK lines, and full `GAP` enumeration on normal success.

Required change:
- Summary by body / hardcode_guard / wiring_audit / gap_status on success.
- Preserve exact missing lane/term/file detail on failure.
- Replace full success GAP dump with count + pointer to SSOT unless explicitly requested.

#### `.agent/tests/check-system-ci-admin-runtime-callable-ssot.sh`

Reason:
- Prints OK per file and required term.

Required change:
- Summary count on success.
- Preserve missing file/term detail on failure.

### P2: runner / DB / docker surfaces where failure detail must be preserved

#### `.agent/tests/check-backend-tests.sh`

Required change:
- Capture runner output.
- On success, print short test summary.
- On failure, replay full runner output.

#### `.agent/tests/check-frontend-types.sh`

Required change:
- Capture runner output.
- On success, print checked file count.
- On failure, replay Deno diagnostics.

#### `.agent/tests/check-frontend-all-tests.sh`

Required change:
- Capture runner output.
- On success, print short summary.
- On failure, replay Deno diagnostics.

#### `.agent/tests/check-runtime-semantics.sh`

Required change:
- Capture per runner.
- On success, print short semantic lane summary.
- On failure, replay full runner output.

#### `.agent/tests/check-default-entity-search.sh`

Required change:
- Capture per section.
- On success, print short summary.
- On failure, replay full runner output.

#### `.agent/tests/check-projection-lane-seed-hardening.sh`

Required change:
- Capture per section.
- On success, print short section summary.
- On failure, replay full runner output.

#### `.agent/tests/check-recommendation-pressure-lane-boundary.sh`

Reason:
- Runs Deno test directly.

Required change:
- Capture runner output.
- On success, print short summary.
- On failure, replay Deno diagnostics.

#### `.agent/tests/check-db-schema.sh`

Required change:
- Use temporary capture instead of blind discard where failure evidence is at risk.
- On success, print table/query assertion summary.
- On failure, print SQL command context and captured stderr/stdout.

#### `.agent/tests/check-bootstrap-validation.sh`

Required change:
- Capture and replay on failure.
- Summarize success by assertion count.

#### `.agent/tests/check-migration-ui-topology.sh`

Required change:
- Summary by static/db/simulation lanes on success.
- Replay SQL/simulation output on failure.

#### `.agent/tests/check-runtime-environment.sh`

Required change:
- Keep failure dumps.
- Add bounded progress lines for long live-runtime phases.
- Avoid expanding existing dependency surface.

### P3: Python helper output under test wrappers

#### `.agent/scripts/check_system_roadmap.py`

Reason:
- Called by `.agent/tests/check-system-roadmap.sh`.
- Prints all warnings even when the check passes.

Required change:
- On clean success, print one compact PASS summary.
- On warning-only success, print warning count and bounded sample or pointer; avoid unbounded warning flood.
- Preserve all failure details when failures exist.

#### `.agent/scripts/check_ssot_vocabulary_contract.py`

Reason:
- Called by `.agent/tests/check-ssot-vocabulary-contract.sh`; emits multiple success OK lines.

Required change:
- Aggregate successful checks into one short PASS summary.
- Preserve exact vocabulary mismatch details on failure.

#### `.agent/scripts/check_sql_attention_ssot_yaml.py`

Reason:
- Called by `.agent/tests/check-sql-attention-ssot.sh`; emits success OK lines before shell-level success summary.

Required change:
- Aggregate success output or let parent script capture/summarize it.
- Preserve parser/contract mismatch details on failure.

### P4: explicit observation JSON / tree surfaces

These are not normal success logs. They are explicit tool outputs, but default/no-arg behavior must not force Agent to read huge payloads during normal success paths.

#### `.agent/scripts/agent_tools/readonly_observation.py`

Applies to:
- `.agent/tools/directory-map`
- `.agent/tools/proof-surface-map`
- `.agent/tools/yaml-section-query`
- `.agent/tools/topology-seed-discussion`

Required change:
- Do not suppress explicit JSON output.
- Do not treat output as proof completion.
- Keep `.agent/tools` read-only observation boundary.
- For default/no-selector large output, prefer bounded summary default plus explicit full-output flag, or document explicit-output expectation.

#### `.agent/scripts/emit-directory-tree-json.py`

Required change:
- Avoid forcing full tree JSON in normal success path.
- Preserve explicit full-output behavior when requested.

#### `.agent/scripts/emit-directory-tree-json.sh`

Required change:
- If this wrapper delegates full tree output, keep explicit full-output behavior but do not make normal success paths depend on reading the full tree.

#### `.agent/scripts/emit-docs-tree-json.sh`

Required change:
- Avoid forcing full docs tree JSON in normal success path.
- Preserve explicit full-output behavior when requested.

### P5: workflow files with direct noisy commands or bypassed script surface

#### `.github/workflows/backend-tests.yml`

Reason:
- Runs `sudo apt-get update && sudo apt-get install -y postgresql-client` directly.
- Runs multiple `psql -f ...` commands directly before delegating to `.agent/tests/check-backend-tests.sh`.

Required change:
- Capture apt/psql output where practical.
- Keep success quiet/summary only.
- Replay apt/psql output on failure.

#### `.github/workflows/db-schema-check.yml`

Reason:
- Runs `sudo apt-get update && sudo apt-get install -y postgresql-client` directly.

Required change:
- Keep install success quiet/summary only.
- Preserve apt failure details.

#### `.github/workflows/bootstrap-validation.yml`

Reason:
- Runs `sudo apt-get update && sudo apt-get install -y postgresql-client` directly.
- Has `docker compose -f infra/docker-compose.yml config > /dev/null`.
- Compose smoke runs docker compose commands directly.

Required change:
- Keep install/config/smoke success quiet or summarized.
- Capture output for failure where useful.
- Do not hide failure cause.

#### `.github/workflows/projection-lane-seed-hardening.yml`

Reason:
- Runs direct dotnet / Deno commands in workflow jobs, bypassing `.agent/tests/check-projection-lane-seed-hardening.sh`.

Required change:
- Route through `.agent/tests/check-projection-lane-seed-hardening.sh` if possible.
- Apply capture-on-success / replay-on-failure in the script surface.

## Implementation Scope

Implement as one Bundle, not small isolated fixes.

Recommended order:

1. Add shared bash helpers only if they stay dependency-free and reduce duplication.
2. Convert P0 structural/orchestrator surfaces.
3. Convert P1 per-term OK loops.
4. Convert P2 runner/DB/docker surfaces with capture-on-success and replay-on-failure.
5. Convert P3 helper/tool defaults without changing authority boundary.
6. Adjust only P5 workflows that bypass fixed script surfaces or run direct noisy commands.
7. Run required checks.
8. Delete this report after Bundle completion.

## Non-target Principle

Do not touch checked files whose success output is already short enough and whose failure path keeps detail, unless a target conversion requires shared helper adoption across the same local pattern.

Examples of non-target posture:
- thin wrappers that only delegate to Python and print one final pass line
- small grep checks with one or two success lines and specific failure messages
- manifest integrity checks that already emit one compact pass summary
- local bootstrap / cleanup scripts where operational progress output is intentional and not normal CI/proof output
- `.agent/tools` entrypoints whose stdout is the requested observation artifact, not a success log

## OK Axis

- Successful checks emit one line or short structured summary.
- Long-running checks emit bounded progress only.
- Failure paths preserve precise error details.
- No failure evidence is hidden by `/dev/null`.
- No new external dependency is added.
- `.agent/tools` remains observation only.
- Agent does not need to read full successful stdout/stderr or huge JSON in normal success paths.
- Explicit `.agent/tools` JSON/artifact output is preserved when requested.
- This report is deleted when implementation is complete.

## NG Axis

- Success path still prints per-file, per-term, full runner stdout/stderr, full warning list, full GAP list, full install logs, full docker logs, or full JSON dumps by default.
- Explicit `.agent/tools` output is suppressed, or is treated as SSOT/proof/completion authority.
- Error detail is hidden while reducing output.
- `/dev/null` is used where it can remove failure evidence.
- Only one small script is fixed and Bundle-wide surfaces remain noisy.
- `.agent/tools` output is treated as SSOT authority, proof completion, or completion judgment.
- New jq/node/ruby/pip/npm/gem dependency is added.
- This report remains after Bundle completion.

## Verification Candidates

Minimum verification after implementation:

```bash
bash .agent/tests/check-agent-tools-surface.sh
bash .agent/tests/check-no-ruby-dependency.sh
bash .agent/tests/check-ssot-proof-surface-connectivity.sh
bash .agent/tests/check-local-ci.sh
bash .agent/tests/check-unified-test-gate.sh
bash .agent/tests/check-structure.sh
```

Additional runner checks should be executed when their touched surfaces are changed.
