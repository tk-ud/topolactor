#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

ok() {
  echo "OK  [external-port-compat] $1"
}

SEED_EMPTY="$REPO_ROOT/db/seed_empty.sql"
ABSORPTION_SEED="$REPO_ROOT/db/external_port_compat_absorption_seed.sql"
INIT_SQL="$REPO_ROOT/db/init.sql"
SHIM="$REPO_ROOT/backend/runtime/ExternalPortPolicyStepExecutorCompatibilityShim.cs"
PROGRAM="$REPO_ROOT/backend/Program.cs"

for f in "$SEED_EMPTY" "$ABSORPTION_SEED" "$INIT_SQL" "$SHIM" "$PROGRAM"; do
  if [ ! -f "$f" ]; then
    fail "required file missing: ${f#$REPO_ROOT/}"
  fi
done

if [ "$FAILURES" -eq 0 ]; then
  if grep -qF "external_port_compat_absorption_seed.sql" "$INIT_SQL"; then
    ok "init.sql applies external_port_compat_absorption_seed.sql after seed_empty.sql"
  else
    fail "db/init.sql must apply db/external_port_compat_absorption_seed.sql"
  fi

  if grep -qF "ExternalPortPolicyStepExecutorCompatibilityShim" "$PROGRAM" && grep -qF "AddSingleton<ExternalPortPolicyStepExecutor>" "$PROGRAM"; then
    ok "Program.cs registers concrete executor behind compatibility shim"
  else
    fail "Program.cs must register ExternalPortPolicyStepExecutorCompatibilityShim with concrete inner executor"
  fi

  if grep -qF "EXTERNAL_PORT_COMPAT_OPERATION_REQUIRES_ABSTRACT_FUNCTION_KEY" "$SHIM" && grep -qF "AbstractFunctionExecutor" "$SHIM"; then
    ok "shim fail-closes compatibility entries without abstract_function_key"
  else
    fail "shim must require abstract_function_key and delegate to AbstractFunctionExecutor"
  fi

  if grep -qF "OfType<ExternalPortHttpResponse>" "$SHIM"; then
    ok "shim bridges http_response back to ExternalPortExecutionContext"
  else
    fail "shim must bridge ExternalPortHttpResponse for capture_response compatibility"
  fi

  legacy_rows="$(grep -En "'[^']+'[[:space:]]*,[[:space:]]*'[^']+'[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*,[[:space:]]*'(build_http_request|send_http|enqueue_scheduler_event|append_runtime_event_log)'" "$SEED_EMPTY" || true)"
  if [ -z "$legacy_rows" ]; then
    ok "seed_empty.sql has no compatibility operation rows"
  else
    while IFS= read -r row; do
      [ -z "$row" ] && continue
      id="$(printf '%s\n' "$row" | sed -n "s/.*'\([0-9a-fA-F-]\{36\}\)'.*/\1/p" | head -n 1)"
      if [ -z "$id" ]; then
        fail "could not parse policy_step_id from legacy row: $row"
        continue
      fi
      if grep -qF "$id" "$ABSORPTION_SEED"; then
        ok "legacy policy step $id is absorbed by abstract_function_key seed"
      else
        fail "legacy policy step $id lacks absorption UPDATE in db/external_port_compat_absorption_seed.sql"
      fi
    done <<< "$legacy_rows"
  fi
fi

if [ "$FAILURES" -eq 0 ]; then
  echo "PASS: external port compatibility absorption guard"
  exit 0
fi

echo "FAIL: external port compatibility absorption guard (${FAILURES})" >&2
exit 1
