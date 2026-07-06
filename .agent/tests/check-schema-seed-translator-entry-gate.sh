#!/usr/bin/env bash
# check-schema-seed-translator-entry-gate.sh -- executable proof surface (CI entrypoint).
#
# Core under test: .agent/scripts/agent_tools/schema_seed_translator_entry_gate.py
# Verifies the schema<->seed translator entry gate core's shape detection
# (translator input envelope / react schema candidate / topology ui seed
# candidate / translator output / invalid-or-unsupported JSON),
# fail-close conditions, and its wiring into
# .agent/tools/react-schema-topology-seed-translator entry (gate blocking ->
# fail-close before conversion; gate pass -> existing conversion proceeds).
#
# Structured assertions live in
# .agent/scripts/check_schema_seed_translator_entry_gate.py (Python3 stdlib
# only); this shell script is only a CI entrypoint/orchestration wrapper (see
# docs/framework-policy.yaml repo_governance_tooling_dependency_policy).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

python3 "$REPO_ROOT/.agent/scripts/check_schema_seed_translator_entry_gate.py"
