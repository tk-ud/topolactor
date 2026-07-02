# Agent Output Noise Control Report

Status: audit_inventory
Bundle: `agent-output-noise-control`
Date: 2026-07-03

## Judgment

`implementation_change` に進める。

先行 `design_change` は不要。現行 todo の問題点、目的、改善方針、対象資料、対象ファイル、OK/NG 軸は Bundle 単位で成立している。

この report は調査中の一時 surface であり、Bundle 実装完了時に削除する。

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

## Priority Inventory

### P0: Success path currently likely to flood stdout

#### `.agent/tests/check-structure.sh`

Current surface:
- Prints `OK` per directory.
- Prints `OK` per required file.
- Prints `OK` per required content term.
- Prints delegated subcheck OK lines.
- Ends with multiple guidance / hint lines.

Required change:
- Replace per-item success output with one structured PASS summary.
- Keep exact missing directory/file/content term details on failure.
- Keep delegated subcheck failure output.
- Run this check last as repository entry contract requires.

#### `.agent/tests/check-local-ci.sh`

Current surface:
- Captures child check output but echoes full child output on success.

Required change:
- Capture child output.
- On success, print one summary per child check.
- On failure, replay captured child output and exit non-zero.

#### `.agent/tests/check-unified-test-gate.sh`

Current surface:
- Prints multiple lane headers and lets lower dotnet / deno / bash check output pass through on success.

Required change:
- Capture each lane.
- Print bounded progress or one lane summary on success.
- Replay lane stdout/stderr on failure.
- Preserve lane identity, command, and exit code.

#### `.agent/scripts/check_agent_tools_surface.py`

Current surface:
- Prints multiple OK lines per tool and per contract axis.

Required change:
- Aggregate successful contract checks into one short PASS summary.
- Preserve per-tool failure details.
- Preserve no-mutation / no-authority / output-shape diagnostics.

### P1: Per-term / per-section OK loops

#### `.agent/tests/check-completion-judgment.sh`

Current surface:
- Prints OK per required term and file.

Required change:
- Summary count on success.
- Missing term/file detail on failure.

#### `.agent/tests/check-worktype-routing.sh`

Current surface:
- Prints OK for each file/reference/term.

Required change:
- Summary by route category on success.
- Missing reference/term detail on failure.

#### `.agent/tests/check-runtime-bundle-ssots.sh`

Current surface:
- Prints OK per SSOT file, section, term, and forbidden-pattern absence.

Required change:
- Summary by SSOT phase on success.
- Preserve forbidden-pattern hit detail on failure.

#### `.agent/tests/check-cli-mcp-port-implementation-ssot.sh`

Current surface:
- Prints OK per section, field, tool, resource, and audit term.

Required change:
- Summary by proof category on success.
- Preserve exact missing path/term on failure.

#### `.agent/tests/check-docs-ssot-connectivity.sh`

Current surface:
- Prints OK per connected SSOT, workflow, script, and test.

Required change:
- Summary counts on success.
- Preserve unconnected/orphan path detail on failure.

#### `.agent/tests/check-sql-attention-ssot.sh`

Current surface:
- Prints multiple OK lines from shell and Python validation.

Required change:
- Summary by semantic/SQL/proof lane on success.
- Preserve missing term and forbidden-hit detail on failure.

### P2: Runner / DB / docker surfaces where failure detail must be preserved

#### `.agent/tests/check-backend-tests.sh`

Current surface:
- Runs `dotnet test --verbosity minimal` directly.

Required change:
- Capture runner output.
- On success, print short test summary.
- On failure, replay full runner output.

#### `.agent/tests/check-frontend-types.sh`

Current surface:
- Runs `deno check` across multiple frontend files directly.

Required change:
- Capture runner output.
- On success, print checked file count.
- On failure, replay Deno diagnostics.

#### `.agent/tests/check-frontend-all-tests.sh`

Current surface:
- Runs Deno tests directly.

Required change:
- Capture runner output.
- On success, print short summary.
- On failure, replay Deno diagnostics.

#### `.agent/tests/check-runtime-semantics.sh`

Current surface:
- Runs dotnet / Deno runtime semantic tests directly.

Required change:
- Capture per runner.
- On success, print short semantic lane summary.
- On failure, replay full runner output.

#### `.agent/tests/check-default-entity-search.sh`

Current surface:
- Runs dotnet / Deno checks directly and prints section output.

Required change:
- Capture per section.
- On success, print short summary.
- On failure, replay full runner output.

#### `.agent/tests/check-projection-lane-seed-hardening.sh`

Current surface:
- Dedicated script exists and runs multiple dotnet / Deno sections.

Required change:
- Capture per section.
- On success, print short section summary.
- On failure, replay full runner output.

#### `.agent/tests/check-db-schema.sh`

Current surface:
- Some SQL file output is redirected to `/dev/null` on success.
- Query checks print OK per assertion.

Required change:
- Use temporary capture instead of blind discard where failure evidence is at risk.
- On success, print table/query assertion summary.
- On failure, print SQL command context and captured stderr/stdout.

#### `.agent/tests/check-bootstrap-validation.sh`

Current surface:
- Some psql output is discarded.
- Prints OK per bootstrap table/assertion.

Required change:
- Capture and replay on failure.
- Summarize success by assertion count.

#### `.agent/tests/check-migration-ui-topology.sh`

Current surface:
- Mixes static term checks, DB query checks, and simulation checks.

Required change:
- Summary by static/db/simulation lanes on success.
- Replay SQL/simulation output on failure.

#### `.agent/tests/check-runtime-environment.sh`

Current surface:
- Live docker / DB / API / SSE check.
- Failure path emits response bodies and docker logs.

Required change:
- Keep failure dumps.
- Add bounded progress lines for long live-runtime phases.
- Avoid expanding existing dependency surface.

### P3: Explicit observation JSON surfaces

These are not normal success logs. They are explicit tool outputs, but no-arg/default behavior must not force Agent to read huge payloads during normal success paths.

#### `.agent/tools/directory-map`
#### `.agent/scripts/emit-directory-tree-json.py`

Current surface:
- Can emit full directory JSON to stdout.

Required change:
- Do not suppress explicit JSON output.
- If default/no-arg mode is too large, prefer bounded summary default plus explicit full-output flag, or document current explicit-output expectation.

#### `.agent/tools/proof-surface-map`

Current surface:
- Can emit all proof surface mappings when no selector is provided.

Required change:
- Do not treat output as proof completion.
- Prefer bounded default summary or require explicit selector/full flag if implementation chooses to change behavior.

#### `.agent/tools/yaml-section-query`

Current surface:
- Already has bounded traversal behavior and explicit error JSON modes.

Required change:
- No major noise change required unless consistency cleanup is done with sibling tools.

#### `.agent/tools/topology-seed-discussion`

Current surface:
- Build-template / candidate outputs may be intentionally large.

Required change:
- Treat as explicit discussion artifact output.
- Do not suppress explicit user-requested template output.
- Avoid using this output as completion judgment.

## Workflow Surfaces

### `.github/workflows/structure-check.yml`

Required change:
- Prefer fixing underlying scripts.
- Keep `check-structure.sh` last.

### `.github/workflows/unified-test-gate.yml`

Required change:
- Prefer fixing `.agent/tests/check-unified-test-gate.sh` and underlying runner scripts.
- Workflow should not require reading full successful test logs.

### `.github/workflows/projection-lane-seed-hardening.yml`

Current surface:
- Runs direct dotnet / Deno commands in workflow jobs, bypassing the dedicated script surface.

Required change:
- Route through `.agent/tests/check-projection-lane-seed-hardening.sh` if possible, then apply capture-on-success / replay-on-failure there.

### `.github/workflows/bootstrap-validation.yml`

Current surface:
- Has `docker compose config > /dev/null` and docker smoke surfaces.

Required change:
- Keep configuration validation quiet on success, but capture output for failure if useful.
- Do not hide failure cause.

## Implementation Scope

Implement as one Bundle, not small isolated fixes.

Recommended order:

1. Add shared bash helpers only if they stay dependency-free and reduce duplication.
2. Convert P0 structural/orchestrator surfaces.
3. Convert P1 per-term OK loops.
4. Convert P2 runner/DB/docker surfaces with capture-on-success and replay-on-failure.
5. Review P3 `.agent/tools` defaults without changing authority boundary.
6. Adjust workflows only where they bypass fixed script surfaces.
7. Run required checks.
8. Delete this report after Bundle completion.

## OK Axis

- Successful checks emit one line or short structured summary.
- Long-running checks emit bounded progress only.
- Failure paths preserve precise error details.
- No failure evidence is hidden by `/dev/null`.
- No new external dependency is added.
- `.agent/tools` remains observation only.
- Agent does not need to read full successful stdout/stderr or huge JSON in normal success paths.
- This report is deleted when implementation is complete.

## NG Axis

- Success path still prints per-file, per-term, full runner stdout/stderr, or full JSON dumps by default.
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
