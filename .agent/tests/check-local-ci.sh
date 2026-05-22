#!/usr/bin/env bash
set -euo pipefail

bash .agent/tests/check-unified-test-gate.sh
bash .agent/tests/check-runtime-environment.sh
# check-structure.sh must run last.
bash .agent/tests/check-structure.sh
