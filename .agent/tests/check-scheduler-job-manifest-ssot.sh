#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0

fail() { echo "FAIL: $1" >&2; FAILURES=$((FAILURES + 1)); }
pass() { echo "OK  : $1"; }

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

echo "=== Scheduler job manifest SSOT wiring check ==="

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
check_term "docs/design/scheduler-job-manifest-ssot.yaml" "wire_system_roadmap_not_started_item"

check_term ".agent/docs/ssot-map.yaml" "scheduler_job_manifest_substrate"
check_term ".agent/docs/ssot-map.yaml" "cron/background scheduler job body"
check_term ".agent/docs/ssot-map.yaml" "admin.contents scheduler job authoring"
check_term ".agent/docs/ssot-map.yaml" "abstract function step chain"
check_term ".agent/docs/ssot-map.yaml" "scheduler job run ledger"
check_term ".agent/docs/ssot-map.yaml" "scheduler job manifest tables"
check_term ".agent/docs/ssot-map.yaml" "input lease / run ledger"
check_term ".agent/docs/ssot-map.yaml" "representative existing cron absorption"
check_term ".agent/docs/ssot-map.yaml" "docs/design/scheduler-job-manifest-ssot.yaml"

check_term "docs/system-roadmap.yaml" "product.scheduler_job_manifest_substrate"
check_term "docs/system-roadmap.yaml" "scheduler-job-manifest-substrate-implementation"
check_term "docs/system-roadmap.yaml" "status: not_started"
check_term "docs/system-roadmap.yaml" "scheduler_job_manifest_tables"
check_term "docs/system-roadmap.yaml" "input_lease_and_run_ledger"
check_term "docs/system-roadmap.yaml" "abstract_function_step_chain_dispatch"
check_term "docs/system-roadmap.yaml" "external_service_reference_boundary"
check_term "docs/system-roadmap.yaml" "representative_existing_cron_absorption"
check_term "docs/system-roadmap.yaml" "scheduler_job_manifest_tables_not_implemented"

check_term ".agent/tasks/todo.md" "scheduler-job-manifest-substrate-implementation"
check_term ".agent/tasks/todo.md" "docs/design/scheduler-job-manifest-ssot.yaml"
check_term ".agent/tasks/todo.md" "scheduler job manifest tables"
check_term ".agent/tasks/todo.md" "admin.contents"
check_term ".agent/tasks/todo.md" "abstract function step chain"
check_term ".agent/tasks/todo.md" "external port credential reference"
check_term ".agent/tasks/todo.md" "representative existing cron absorption"

roadmap_block="$(
  awk '
    /^[[:space:]]{4}product\.scheduler_job_manifest_substrate:/ { in_block=1 }
    in_block && /^[[:space:]]{4}product\.[A-Za-z0-9_.-]+:/ && $1 != "product.scheduler_job_manifest_substrate:" { exit }
    in_block { print }
  ' "$REPO_ROOT/docs/system-roadmap.yaml"
)"
if printf '%s\n' "$roadmap_block" | grep -Eq "status: implemented|status: production_ready|production_ready: true"; then
  fail "scheduler job manifest roadmap item must not be marked implemented/production_ready by the wiring check"
else
  pass "roadmap item remains non-implemented"
fi

if [ "$FAILURES" -eq 0 ]; then
  echo "=== Scheduler job manifest SSOT wiring check passed ==="
else
  echo "=== $FAILURES scheduler job manifest wiring check(s) failed ===" >&2
  exit 1
fi
