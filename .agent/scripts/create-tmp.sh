#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$REPO_ROOT/.agent/tmp"
TMP_FILE="$TMP_DIR/tmp.txt"

mkdir -p "$TMP_DIR"

if [ -f "$TMP_FILE" ]; then
  echo "tmp already exists: .agent/tmp/tmp.txt"
  exit 0
fi

cat > "$TMP_FILE" <<'EOF'
# Temporary Scenario Contract
# This file is a temporary scenario contract, not a free-form planning memo.
# Fill this out BEFORE implementation; update only if intended scenario changes.
#
# 1) User-visible scenario or runtime claim
# 2) Entry operation / request shape
# 3) Expected canonical runtime route
# 4) Expected read / write / append / cache / return order
# 5) Seed / fixture / policy data involved
# 6) Expected emission / projection / status
# 7) Required side effects, including failure / cold-start / insufficient paths
# 8) Runtime Boundary Failure Matrix coverage (1-10) and intentional out-of-scope reasons
# 9) Known non-goals / out-of-scope paths
#
# Runtime Boundary Failure Matrix:
# 1. success path
# 2. authentication / authorization failure
# 3. request validation failure
# 4. malformed id / malformed payload
# 5. not found
# 6. persistence constraint failure
# 7. repository / backend unavailable
# 8. frontend proxy status propagation
# 9. UI-visible error state
# 10. post-write read consistency
#
# After implementation:
# 10) Full branch diff verification result
#
# Before completion:
# - Verify full branch diff against this scenario contract
# - Delete this file via: bash .agent/scripts/delete-tmp.sh
EOF

echo "created: .agent/tmp/tmp.txt"
