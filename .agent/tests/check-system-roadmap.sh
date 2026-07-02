#!/usr/bin/env bash
# check-system-roadmap.sh — system roadmap YAML SSOT structural + drift check (CI entrypoint).
#
# Structured processing (YAML load, duplicate-key detection, drift scanning)
# is delegated to a Python3 stdlib script; repo governance tooling structured
# processing must use Python3 stdlib only (Ruby is prohibited — see
# docs/framework-policy.yaml repo_governance_tooling_dependency_policy).
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
ROADMAP="$REPO_ROOT/docs/system-roadmap.yaml"

python3 "$REPO_ROOT/.agent/scripts/check_system_roadmap.py" "$REPO_ROOT" "$ROADMAP"
