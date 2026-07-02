#!/usr/bin/env bash
# check-ssot-proof-surface-connectivity.sh — SSOT proof surface connectivity gate (CI entrypoint).
#
# Responsibility:
#   Verify that every docs/ is_ssot=true candidate (observed via the general
#   directory-JSON-observation mechanism, emit-directory-tree-json.py) is
#   connected to at least one reachable .agent/tests/ executable proof
#   surface (also observed via the same general mechanism, as
#   is_executable_test=true). docs/ SSOT candidates and .agent/tests/
#   executable proof candidates sit on the same observation plane; a mere
#   .agent/docs/ssot-map.yaml reference does not count as a proof
#   connection.
#
# Structured JSON/YAML processing (tree walk, set membership, connectivity
# resolution) is Python3 stdlib only; this shell script is only a CI
# entrypoint/orchestration wrapper (see docs/framework-policy.yaml
# repo_governance_tooling_dependency_policy).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

python3 "$REPO_ROOT/.agent/scripts/check_ssot_proof_surface_connectivity.py"
