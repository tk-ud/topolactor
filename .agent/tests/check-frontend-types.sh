#!/usr/bin/env bash
# check-frontend-types.sh — local frontend type check SSOT
# Verifies Fresh/Deno/Preact skeleton type consistency without backend/db coupling.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

if ! command -v deno >/dev/null 2>&1; then
  echo "ERROR: Deno is required for frontend type check. Install Deno and retry." >&2
  exit 1
fi

deno check \
  frontend/routes/index.tsx \
  frontend/routes/admin/index.tsx \
  frontend/routes/admin/manifests.tsx \
  frontend/routes/admin/contents.tsx \
  frontend/routes/admin/ui-builder.tsx \
  frontend/routes/demo-article.tsx \
  frontend/routes/runtime-status.tsx \
  frontend/islands/OperationPanel.tsx \
  frontend/islands/ReplyPanel.tsx \
  frontend/components/ProjectionView.tsx \
  frontend/components/EmissionView.tsx \
  frontend/package/defaultPackage.ts \
  frontend/schema/defaultSchema.ts \
  frontend/registry/componentRegistry.ts \
  frontend/runtime/resolveOperationVector.ts \
  frontend/runtime/renderEmission.ts \
  frontend/runtime/restoreResume.ts \
  frontend/runtime/projectionConstructor.ts \
  frontend/runtime/projectionRuntime.ts \
  frontend/api/dispatch.ts \
  frontend/structure_map.ts
