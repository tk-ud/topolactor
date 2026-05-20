#!/usr/bin/env bash
# check-pipeline-continuity.sh — bidirectional pipeline identity continuity check
# Validates API command lane and SSE projection lane identity fields at each node.
# Requires only bash and grep — no build tools, no credentials.
#
# API lane failures exit non-zero (broken continuity).
# SSE lane gaps report as GAP (not_implemented nodes are expected and do not fail CI).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

FAILURES=0
GAPS=0

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

gap() {
  echo "GAP:  $1"
  GAPS=$((GAPS + 1))
}

check_term() {
  local file="$REPO_ROOT/$1"
  local term="$2"
  local node="$3"
  if [ ! -f "$file" ]; then
    fail "[$node] file missing: $1"
    return
  fi
  if grep -qF -- "$term" "$file"; then
    echo "OK  [$node] $(basename "$1") → \"$term\""
  else
    fail "[$node] $1 missing identity: \"$term\""
  fi
}

check_term_gap() {
  local path="$REPO_ROOT/$1"
  local term="$2"
  local node="$3"
  if grep -rqF -- "$term" "$path" 2>/dev/null; then
    echo "OK  [$node] $1 → \"$term\""
  else
    gap "[$node] \"$term\" absent from $1 — not_implemented (see pipeline-continuity-ssot.yaml)"
  fi
}

# ─── API command lane ─────────────────────────────────────────────────────────

echo ""
echo "=== API command lane ==="

echo "--- [frontend.component] ---"
check_term "frontend/islands/OperationPanel.tsx"          "operationType"     "frontend.component"
check_term "frontend/islands/OperationPanel.tsx"          "dispatchOperation"  "frontend.component"

echo "--- [frontend.resolve_vector] ---"
check_term "frontend/runtime/resolveOperationVector.ts"   "OperationType"     "frontend.resolve_vector"
check_term "frontend/runtime/resolveOperationVector.ts"   "attractorKey"      "frontend.resolve_vector"
check_term "frontend/runtime/resolveOperationVector.ts"   "Create"            "frontend.resolve_vector"
check_term "frontend/runtime/resolveOperationVector.ts"   "diffUpdate"        "frontend.resolve_vector"
check_term "frontend/runtime/resolveOperationVector.ts"   "logicalDelete"     "frontend.resolve_vector"

echo "--- [frontend.api_dispatch] ---"
check_term "frontend/api/dispatch.ts"                     "DispatchRequest"   "frontend.api_dispatch"
check_term "frontend/api/dispatch.ts"                     "DispatchResponse"  "frontend.api_dispatch"
check_term "frontend/api/dispatch.ts"                     "Emission"          "frontend.api_dispatch"
check_term "frontend/api/dispatch.ts"                     "componentIds"      "frontend.api_dispatch"

echo "--- [backend.endpoint] ---"
check_term "backend/endpoint/DispatchEndpoint.cs"         "EndpointRequestDto" "backend.endpoint"
check_term "backend/endpoint/DispatchEndpoint.cs"         "RuntimeExecutor"   "backend.endpoint"

echo "--- [backend.runtime_executor] ---"
check_term "backend/runtime/RuntimeExecutor.cs"           "ExecuteAsync"      "backend.runtime_executor"
check_term "backend/runtime/RuntimeExecutor.cs"           "OperationVectorResolver" "backend.runtime_executor"
check_term "backend/runtime/RuntimeExecutor.cs"           "AttractorResolver" "backend.runtime_executor"

echo "--- [backend.schema] ---"
check_term "backend/schema/Contracts.cs"                  "OperationType"     "backend.schema"
check_term "backend/schema/Contracts.cs"                  "AttractorKey"      "backend.schema"
check_term "backend/schema/Contracts.cs"                  "ComponentIds"      "backend.schema"

echo "--- [backend.emission_builder] ---"
check_term "backend/runtime/EmissionBuilder.cs"           "Emission"          "backend.emission_builder"
check_term "backend/runtime/EmissionBuilder.cs"           "ComponentIds"      "backend.emission_builder"

echo "--- [frontend.projection] ---"
check_term "frontend/components/EmissionView.tsx"         "componentIds"      "frontend.projection"
check_term "frontend/components/ProjectionView.tsx"       "componentIds"      "frontend.projection"

# ─── SSE projection lane ──────────────────────────────────────────────────────

echo ""
echo "=== SSE projection lane ==="

echo "--- [backend.attention_compute] ---"
check_term "backend/schema/ContextRouteContracts.cs"      "HubAttentionCurrentRecord" "backend.attention_compute"
check_term "backend/schema/ContextRouteContracts.cs"      "AttentionScore"    "backend.attention_compute"

echo "--- [backend.sse_emitter] (gap check — not_implemented) ---"
check_term_gap "backend"                                  "text/event-stream" "backend.sse_emitter"

echo "--- [frontend.sse_receiver] (gap check — not_implemented) ---"
check_term_gap "frontend"                                 "EventSource"       "frontend.sse_receiver"

echo "--- [frontend.sse_projection] ---"
check_term "frontend/components/ProjectionView.tsx"       "componentIds"      "frontend.sse_projection"

# ─── Result ───────────────────────────────────────────────────────────────────

echo ""
if [ "$FAILURES" -gt 0 ]; then
  echo "=== $FAILURES pipeline continuity failure(s) ===" >&2
  exit 1
fi

if [ "$GAPS" -gt 0 ]; then
  echo "=== Pipeline continuity: API lane OK | SSE lane: $GAPS gap(s) (not_implemented — see docs/design/pipeline-continuity-ssot.yaml) ==="
else
  echo "=== Pipeline continuity: all checks passed ==="
fi
