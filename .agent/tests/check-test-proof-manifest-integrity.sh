#!/usr/bin/env bash
# check-test-proof-manifest-integrity.sh — audits the system-wide test proof manifest SSOT (CI entrypoint).
#
# Structured processing (YAML load, proof-graph integrity validation) is
# delegated to a Python3 stdlib script; repo governance tooling structured
# processing must use Python3 stdlib only (Ruby is prohibited — see
# docs/framework-policy.yaml repo_governance_tooling_dependency_policy).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

python3 "$REPO_ROOT/.agent/scripts/check_test_proof_manifest_integrity.py"
