#!/usr/bin/env bash
# check-ssot-vocabulary-contract.sh — SSOT vocabulary subset checks (CI entrypoint).
#
# Structured processing (YAML load, regex extraction, set-subset validation) is
# delegated to a Python3 stdlib script; repo governance tooling structured
# processing must use Python3 stdlib only (Ruby is prohibited — see
# docs/framework-policy.yaml repo_governance_tooling_dependency_policy).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

python3 "$ROOT/.agent/scripts/check_ssot_vocabulary_contract.py"

echo "=== SSOT vocabulary contract check passed ==="
