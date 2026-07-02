#!/usr/bin/env bash
# check-agent-tools-surface.sh — .agent/tools proof/structure gate (CI entrypoint).
#
# Responsibility:
#   Verify the .agent/tools governance contract declared in
#   docs/governance/agent-governance-routing-ssot.yaml
#   (agent_repo_observation_tools_surface) and .agent/tools/README.md:
#   required entrypoint existence, executable permission, thin
#   read-only-entrypoint boundary (delegates to
#   .agent/scripts/agent_tools/readonly_observation.py instead of forking
#   structured processing), mutation-argument rejection, and baseline output
#   free of SSOT authority / proof completion / completion judgment / semantic
#   audit judgment / implemented-partial-not_started status claims.
#
#   This check does not itself judge SSOT authority, proof completion, or
#   implementation status of any bundle observed by .agent/tools; it only
#   verifies .agent/tools' own structural / read-only / no-authority contract.
#
# Structured subprocess invocation and JSON verification are Python3 stdlib
# only; this shell script is only a CI entrypoint/orchestration wrapper (see
# docs/framework-policy.yaml repo_governance_tooling_dependency_policy).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

python3 "$REPO_ROOT/.agent/scripts/check_agent_tools_surface.py" "$@"
