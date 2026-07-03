#!/usr/bin/env bash
# check-agent-ui-tool.sh — Agent UI tool implementation contract gate
#
# This is intentionally implementation-facing. It fails until the Agent UI tool
# entrypoints required by docs/governance/agent-ui-protocol-ssot.yaml are
# implemented.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

python3 "$REPO_ROOT/.agent/scripts/check_agent_ui_tool.py" "$@"
