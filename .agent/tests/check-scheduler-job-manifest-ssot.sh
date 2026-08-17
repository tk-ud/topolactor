#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0
PASS_COUNT=0
VERBOSE="${CHECK_VERBOSE:-0}"

fail() { echo "FAIL: $1" >&2; FAILURES=$((FAILURES + 1)); }
pass() { PASS_COUNT=$((PASS_COUNT + 1)); if [ "$VERBOSE" = "1" ]; then echo "OK  : $1"; fi; }

check_file() {
  local path="$1"
  if [ -f "$REPO_ROOT/$path" ]; then
    pass "file exists: $path"
  else
    fail "missing file: $path"
  fi
}

check_term() {
  local path="$1" term="$2"
  if grep -qF -- "$term" "$REPO_ROOT/$path"; then
    pass "$path -> $term"
  else
    fail "$path missing: $term"
  fi
}

check_absent() {
  local path="$1" term="$2"
  if grep -qF -- "$term" "$REPO_ROOT/$path"; then
    fail "$path must NOT contain: $term"
  else
    pass "$path absent: $term"
  fi
}

if [ "$VERBOSE" = "1" ]; then echo "=== Scheduler job manifest substrate (implemented) wiring check ==="; fi

# ── SSOT ────────────────────────────────────────────────────────────────────
check_file "docs/design/scheduler-job-manifest-ssot.yaml"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "scheduler_job_manifest_ssot"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "scheduler_job_manifest_substrate"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "admin.contents"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "trigger_kind"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "schedule_policy_kind"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "manual_run_allowed"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "input_status_pending_value"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "input_status_processing_value"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "input_status_completed_value"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "input_status_failed_value"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "AbstractFunctionExecutor"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "current_change_boundary"
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "implemented_substrate_design_document"

# ── ssot-map ─────────────────────────────────────────────────────────────────
check_term ".agent/docs/ssot-map.yaml" "scheduler_job_manifest_substrate"
check_term ".agent/docs/ssot-map.yaml" "cron/background scheduler job body"
check_term ".agent/docs/ssot-map.yaml" "admin.contents scheduler job authoring"
check_term ".agent/docs/ssot-map.yaml" "abstract function step chain"
check_term ".agent/docs/ssot-map.yaml" "scheduler job run ledger"
check_term ".agent/docs/ssot-map.yaml" "scheduler job manifest tables"
check_term ".agent/docs/ssot-map.yaml" "input lease / run ledger"
check_term ".agent/docs/ssot-map.yaml" "representative existing cron absorption"
check_term ".agent/docs/ssot-map.yaml" "docs/design/scheduler-job-manifest-ssot.yaml"

# ── roadmap (implemented) ────────────────────────────────────────────────────
check_term "docs/system-roadmap.yaml" "product.scheduler_job_manifest_substrate"
check_term "docs/system-roadmap.yaml" "scheduler-job-manifest-substrate-implementation"
check_term "docs/system-roadmap.yaml" "input_lease_and_run_ledger"
check_term "docs/system-roadmap.yaml" "abstract_function_step_chain_dispatch"
check_term "docs/system-roadmap.yaml" "representative_existing_cron_absorption"
check_term "docs/system-roadmap.yaml" "representative_existing_cron_absorption_implemented_log_retention"

# Roadmap block must be implemented (not partial/not_started) and not production_ready.
roadmap_block="$(
  awk '
    /^[[:space:]]{4}product\.scheduler_job_manifest_substrate:/ { in_block=1 }
    in_block && /^[[:space:]]{4}product\.[A-Za-z0-9_.-]+:/ && $1 != "product.scheduler_job_manifest_substrate:" { exit }
    in_block { print }
  ' "$REPO_ROOT/docs/system-roadmap.yaml"
)"
if printf '%s\n' "$roadmap_block" | grep -Eq "status: implemented"; then
  pass "roadmap item is implemented"
else
  fail "roadmap item must be implemented"
fi
if printf '%s\n' "$roadmap_block" | grep -Eq "production_ready: true"; then
  fail "roadmap item must not claim production_ready"
else
  pass "roadmap item is not production_ready"
fi
if printf '%s\n' "$roadmap_block" | grep -Eq "completion_condition:|evidence:"; then
  pass "roadmap item carries completion_condition/evidence"
else
  fail "implemented roadmap item must carry completion_condition or evidence"
fi

# ── TODO bundle must be removed (implemented reached) ─────────────────────────
check_absent ".agent/tasks/todo.md" "## Bundle \`scheduler-job-manifest-substrate-implementation\`"

# ── Implementation surfaces ──────────────────────────────────────────────────
check_file "backend/scheduler/SchedulerJobRunner.cs"
check_file "backend/repository/NpgsqlSchedulerJobManifestRepository.cs"
check_file "backend/runtime/SchedulerJobPrimitiveAdapters.cs"
check_file "backend/runtime/AdminRuntime.SchedulerSettings.cs"

# scheduler-settings subBundle (admin-surface-topology-seed-conversion, 2026-08-17): the
# hardcoded frontend/islands/SchedulerJobSettingsPanel.tsx was retired in favor of a thin
# ProjectionShell wrapper pinned to the seeded scheduler.settings.projection manifest by
# manifest_key -- the same route retirement pattern admin-enum (AdminEnumsRoster.tsx) and
# team-dashboard already established. This check now verifies the thin wrapper's own shape
# instead of the deleted island's file existence.
if [ -f "$REPO_ROOT/frontend/islands/SchedulerJobSettingsPanel.tsx" ]; then
  fail "frontend/islands/SchedulerJobSettingsPanel.tsx must be removed (retired to a thin ProjectionShell wrapper)"
else
  pass "frontend/islands/SchedulerJobSettingsPanel.tsx removed (retired)"
fi
check_term "frontend/routes/admin/scheduler.tsx" "ProjectionShell"
check_term "frontend/routes/admin/scheduler.tsx" "scheduler.settings.projection"

# on_error SSOT vocabulary present; legacy non-SSOT skip_step removed.
check_term "backend/scheduler/SchedulerJobRunner.cs" "fail_run"
check_term "backend/scheduler/SchedulerJobRunner.cs" "skip_input"
check_term "backend/scheduler/SchedulerJobRunner.cs" "mark_input_failed"
check_term "backend/scheduler/SchedulerJobRunner.cs" "LeaseDueInputRowsAsync"
check_term "backend/scheduler/SchedulerJobRunner.cs" "UpsertAuthorizedOutputAsync"
check_absent "backend/scheduler/SchedulerJobRunner.cs" "skip_step"

# admin.contents authoring dispatch.
check_term "backend/runtime/AdminRuntime.cs" "scheduler_jobs:create"
check_term "backend/runtime/AdminRuntime.cs" "scheduler_jobs:edit"
check_term "backend/runtime/AdminRuntime.cs" "scheduler_jobs:disable"
check_term "backend/runtime/AdminRuntime.SchedulerSettings.cs" "SCHEDULER_JOB_SECRET_FIELD_FORBIDDEN"

# Representative cron absorption: RetentionScheduler removed; seed manifest present.
if [ -f "$REPO_ROOT/backend/scheduler/RetentionScheduler.cs" ]; then
  fail "RetentionScheduler.cs must be removed (absorbed into scheduler job manifest substrate)"
else
  pass "RetentionScheduler.cs removed (absorbed)"
fi
check_term "db/seed_empty.sql" "log_retention_sweep"
check_term "db/seed_empty.sql" "system.log_retention"
check_term "db/seed_empty.sql" "scheduler_jobs\",\"action\":\"create"

# Tests
check_file "backend/tests/Topolactor.Runtime.Tests/SchedulerJobRunnerTests.cs"
check_file "backend/tests/Topolactor.Runtime.Tests/AdminRuntimeSchedulerAuthoringTests.cs"
check_file "backend/tests/Topolactor.Runtime.Tests/SchedulerJobManifestRepositoryGuardTests.cs"
check_file "frontend/tests/schedulerJobManifestProjection.test.ts"

if [ "$FAILURES" -eq 0 ]; then
  echo "PASS check-scheduler-job-manifest-ssot.sh assertions=${PASS_COUNT}"
else
  echo "=== $FAILURES scheduler job manifest wiring check(s) failed ===" >&2
  exit 1
fi
