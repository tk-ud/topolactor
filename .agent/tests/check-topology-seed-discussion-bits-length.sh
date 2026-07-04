#!/usr/bin/env bash
# check-topology-seed-discussion-bits-length.sh — bits_length metadata gate
#
# Thin CI entrypoint; structured assertions live in
# .agent/scripts/check_topology_seed_discussion_bits_length.py.
# Registered as a runner_surface for the agent-tools-proof-and-structure-gate
# bundle in .agent/docs/test-bundles.yaml and enumerated in
# .agent/docs/required-paths.yaml.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

python3 "$REPO_ROOT/.agent/scripts/check_topology_seed_discussion_bits_length.py" "$@"
