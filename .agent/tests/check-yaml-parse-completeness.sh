#!/usr/bin/env bash
# check-yaml-parse-completeness.sh — minimal_yaml silent-truncation gate (CI entrypoint).
#
# Structured processing (yaml load, regex extraction, top-level key diffing) is
# delegated to a Python3 stdlib script; repo governance tooling structured
# processing must use Python3 stdlib only (Ruby is prohibited — see
# docs/framework-policy.yaml repo_governance_tooling_dependency_policy).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

python3 "$ROOT/.agent/scripts/check_yaml_parse_completeness.py"
