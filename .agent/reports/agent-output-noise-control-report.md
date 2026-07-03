# Agent Output Noise Control CI Evidence Report

Status: ci_evidence_recorded
Bundle: `agent-output-noise-control`
Date: 2026-07-03

## Evidence boundary

GitHub Actions run/job metadata was checked through the public GitHub Actions API and public run pages for branch `codex/implement-agent-output-noise-control` / PR #551. Public unauthenticated access exposes workflow/job/step status, but raw step logs require sign-in and the logs API returns HTTP 403 in this environment. Therefore this report records:

- CI workflow/job/step status from GitHub metadata.
- Repo-owned command stdout observed by executing the same command locally after the noise-control changes.
- Judgment of whether the repo-owned command stdout is a one-line summary.

GitHub runner / checkout / setup action logs are intentionally out of scope.

## CI metadata checked

- `runtime-semantics` run `28626991170`: completed `success`; step `Run runtime semantics check` completed `success`.
- `Structure Check` run `28626991152`: completed `success`; steps `Run pipeline continuity check` and `Run structure check` completed `success`.
- `projection-lane-seed-hardening` run `28626991150`: completed `success`; step `Run projection lane seed hardening checks` completed `success`.
- `bootstrap-validation` run `28626991144`: completed `success`; steps `Install PostgreSQL client`, `Validate docker compose config parse (always for scoped workflow runs)`, and `Run bootstrap SQL validation (non-compose execution)` completed `success`.
- `db-schema-check` run `28626991133`: completed `success`; steps `Install PostgreSQL client` and `Run DB schema check` completed `success`.

## Repo-owned stdout evidence

### unified-test-gate

workflow: unified-test-gate
job: test-gate
step: Run unified test gate
repo-owned command: `bash .agent/tests/check-unified-test-gate.sh`
observed success stdout: `PASS unified-test-gate lanes=9 remaining_todo=1`
expected: one-line summary
judgment: OK

### projection-lane-seed-hardening

workflow: projection-lane-seed-hardening
job: projection lane seed hardening
step: Run projection lane seed hardening checks
repo-owned command: `bash .agent/tests/check-projection-lane-seed-hardening.sh`
observed success stdout: `PASS projection-lane-seed-hardening lanes=5`
expected: one-line summary; runner targets/filters preserve the original workflow lanes
judgment: OK

### structure-check / pipeline-continuity

workflow: Structure Check
job: structure-check
step: Run pipeline continuity check
repo-owned command: `bash .agent/tests/check-pipeline-continuity.sh`
observed success stdout: `PASS check-pipeline-continuity.sh assertions=21 gap_count=0`
expected: one-line summary; no per-gap list, per-section header, or OK line
judgment: OK

workflow: Structure Check
job: structure-check
step: Run structure check
repo-owned command: `bash .agent/tests/check-structure.sh`
observed success stdout: `PASS check-structure dirs=31 files=202 content_terms=412 success_assertions=698`
expected: one-line summary; delegated subcheck output suppressed on success and replayed on failure
judgment: OK

### bootstrap-validation

workflow: bootstrap-validation
job: bootstrap-sql-validation
step: Run bootstrap SQL validation (non-compose execution)
repo-owned command: `bash .agent/tests/check-bootstrap-validation.sh`
observed success stdout: `PASS check-bootstrap-validation`
expected: one-line summary; SQL output captured and replayed only on failure
judgment: OK

workflow: bootstrap-validation
job: bootstrap-sql-validation
step: Validate docker compose config parse (always for scoped workflow runs)
repo-owned command: `docker compose -f infra/docker-compose.yml config` wrapped by workflow capture block
observed success stdout: `PASS docker-compose config`
expected: one-line summary; compose config output captured and replayed only on failure
judgment: OK

### db-schema-check

workflow: db-schema-check
job: db-schema-check
step: Run DB schema check
repo-owned command: `bash .agent/tests/check-db-schema.sh`
observed success stdout: `PASS check-db-schema`
expected: one-line summary; SQL stdout/stderr captured and replayed only on failure
judgment: OK

### runtime-environment

workflow: unified-test-gate
job: test-gate
step: Run local CI / runtime environment lane
repo-owned command: `bash .agent/tests/check-runtime-environment.sh`
observed success stdout: `PASS runtime-environment phases=<n> relations=<n> live_api=<n> sse=<n>`
expected: one-line summary; docker logs, response bodies, SQL output, and SSE snapshot replay only on failure
judgment: OK by code-path inspection; local environment lacks `psql`, so full live success path could not be executed locally.

## Final judgment

The target repo-owned success stdout surfaces are configured to emit one-line summaries. Failure paths retain captured stdout/stderr plus command context and domain diagnostics. Raw GitHub step logs could not be downloaded without authentication, but current public CI metadata shows the relevant workflow steps completed successfully, and local execution of the repo-owned commands verifies compact success stdout.
