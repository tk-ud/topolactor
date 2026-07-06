#!/usr/bin/env bash
# check-agent-ui-protocol-ssot.sh — Agent UI protocol SSOT/reference wiring gate
#
# Structural proof only: verifies that the Agent UI protocol SSOT is connected
# to its reference/log/temp-file contract surfaces. This does not prove an
# .agent/tools/agent-ui implementation exists or is complete.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

FAILURES=0
PASS_COUNT=0

fail() { echo "FAIL: $1" >&2; FAILURES=$((FAILURES + 1)); }
ok() { PASS_COUNT=$((PASS_COUNT + 1)); }

require_file() {
  local path="$1"
  if [ -f "$path" ]; then
    ok
  else
    fail "required file missing: $path"
  fi
}

require_term() {
  local path="$1"
  local term="$2"
  if [ ! -f "$path" ]; then
    fail "term check skipped (file missing): $path — expected term: $term"
    return
  fi
  if grep -qF -- "$term" "$path"; then
    ok
  else
    fail "term not found in $path: $term"
  fi
}

forbid_term() {
  local path="$1"
  local term="$2"
  if [ ! -f "$path" ]; then
    fail "forbidden-term check skipped (file missing): $path — forbidden term: $term"
    return
  fi
  if grep -qF -- "$term" "$path"; then
    fail "forbidden term found in $path: $term"
  else
    ok
  fi
}

required_files=(
  "docs/governance/agent-ui-protocol-ssot.yaml"
  "docs/governance/reference/agent-ui-tool-output-reference.yaml"
  "docs/governance/reference/agent-ui-senario-tmp-reference.yaml"
  "docs/governance/reference/agent-ui-negative-boundary-reference.yaml"
  ".agent/tools/logs/tool.log"
  ".agent/scripts/check_agent_ui_protocol.py"
)

for path in "${required_files[@]}"; do
  require_file "$path"
done

ssot_terms=(
  "agent-ui-tool-output-reference.yaml"
  "agent-ui-senario-tmp-reference.yaml"
  "agent-ui-negative-boundary-reference.yaml"
  "initial_contract"
  "implementation"
  "local_test"
  ".agent/tools/logs/tool.log"
  "senario-tmp.md"
)

for term in "${ssot_terms[@]}"; do
  require_term "docs/governance/agent-ui-protocol-ssot.yaml" "$term"
done

ssot_forbidden_terms=(
  "markdown_template: |-"
)

for term in "${ssot_forbidden_terms[@]}"; do
  forbid_term "docs/governance/agent-ui-protocol-ssot.yaml" "$term"
done

tool_output_terms=(
  "authority_split"
  "ai_authored"
  "tool_generated"
  "Append only"
  "owner: human"
  "periodic_manual"
  "AI-authored uuid"
  "AI-authored datetime"
  "AI-authored worktype metadata"
  "AI-authored tool.log records"
)

for term in "${tool_output_terms[@]}"; do
  require_term "docs/governance/reference/agent-ui-tool-output-reference.yaml" "$term"
done

senario_tmp_terms=(
  "# contracts"
  "# negative cases"
  "NG boundary"
)

for term in "${senario_tmp_terms[@]}"; do
  require_term "docs/governance/reference/agent-ui-senario-tmp-reference.yaml" "$term"
done

negative_boundary_terms=(
  "NG boundary"
)

for term in "${negative_boundary_terms[@]}"; do
  require_term "docs/governance/reference/agent-ui-negative-boundary-reference.yaml" "$term"
done

gitignore_terms=(
  "senario-tmp.md"
)

for term in "${gitignore_terms[@]}"; do
  require_term ".gitignore" "$term"
done

if python3 "$REPO_ROOT/.agent/scripts/check_agent_ui_protocol.py"; then
  ok
else
  fail "Python Agent UI protocol boundary check failed"
fi

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "PASS check-agent-ui-protocol-ssot.sh assertions=${PASS_COUNT}"
  exit 0
fi

echo "=== $FAILURES Agent UI protocol SSOT check(s) failed ===" >&2
exit 1
