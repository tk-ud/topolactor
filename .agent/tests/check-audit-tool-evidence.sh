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

require .agent/prompt/audit.md "Agent UI tool evidence observed"
require .agent/prompt/audit.md "evidence_present: yes/no/not_applicable"
require .agent/prompt/audit.md "observed_source"
require .agent/prompt/audit.md "audit must not generate, append, or backfill tool evidence"
require .agent/protocols/audit-tool-evidence.md "audit tool evidence observation protocol"
require .agent/protocols/audit-tool-evidence.md "Audit must not generate, append, rewrite, backfill"
require .agent/protocols/audit-tool-evidence.md "Do not inspect or require the audit Agent's own tool route as target PR evidence"
require .agent/protocols/audit-tool-evidence.md "observation_judgment"
require .agent/routes/worktype-required-protocols.yaml ".agent/protocols/audit-tool-evidence.md"
require .agent/routes/worktype-required-protocols.yaml ".agent/tests/check-audit-tool-evidence.sh"

if [[ "$missing" -ne 0 ]]; then
  exit 1
fi

echo "PASS check-audit-tool-evidence.sh"
