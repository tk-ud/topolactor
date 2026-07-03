#!/usr/bin/env bash
set -euo pipefail

missing=0

require() {
  local file="$1"
  local term="$2"
  if ! grep -Fq -- "$term" "$file"; then
    echo "FAIL $file missing: $term" >&2
    missing=1
  fi
}

require .agent/prompt/audit.md "Agent UI tool evidence checked"
require .agent/prompt/audit.md "fallback_reason_if_not_used"
require .agent/prompt/audit.md "tool_log_entry_checked"
require .agent/protocols/audit-tool-evidence.md "## required_evidence_fields"
require .agent/protocols/audit-tool-evidence.md "tool_used: yes/no/not_available"
require .agent/protocols/audit-tool-evidence.md "fallback_reason_if_not_used"
require .agent/routes/worktype-required-protocols.yaml ".agent/protocols/audit-tool-evidence.md"
require .agent/routes/worktype-required-protocols.yaml ".agent/tests/check-audit-tool-evidence.sh"

if [[ "$missing" -ne 0 ]]; then
  exit 1
fi

echo "PASS check-audit-tool-evidence.sh"
