#!/usr/bin/env bash
# check-structure.sh — local structure check SSOT
# Verifies required directories, files, and architecture-critical content terms.
# Requires only bash. No credentials, no build tools, no business data.
# Exits non-zero on any failure.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

check_dir() {
  local d="$REPO_ROOT/$1"
  if [ -d "$d" ]; then
    echo "OK  [dir]  $1"
  else
    fail "Directory missing: $1"
  fi
}

check_file() {
  local f="$REPO_ROOT/$1"
  if [ -f "$f" ]; then
    echo "OK  [file] $1"
  else
    fail "File missing: $1"
  fi
}

check_content() {
  local file="$REPO_ROOT/$1"
  local term="$2"
  if [ ! -f "$file" ]; then
    fail "Content check skipped (file missing): $1 — expected term: $term"
    return
  fi
  if grep -qF "$term" "$file"; then
    echo "OK  [term] $1 → \"$term\""
  else
    fail "Term not found in $1: \"$term\""
  fi
}

# ─── Required directories ────────────────────────────────────────────────────

echo ""
echo "=== Directory checks ==="
check_dir ".agent/docs"
check_dir ".agent/rules"
check_dir ".agent/skills"
check_dir ".agent/tests"
check_dir ".agent/tasks"
check_dir ".agent/reports"
check_dir "docs"
check_dir "db"
check_dir "backend/endpoint"
check_dir "backend/runtime"
check_dir "backend/mapper"
check_dir "backend/repository"
check_dir "backend/guard"
check_dir "backend/schema"
check_dir "frontend/routes"
check_dir "frontend/islands"
check_dir "frontend/components"
check_dir "frontend/package"
check_dir "frontend/schema"
check_dir "frontend/registry"
check_dir "frontend/runtime"
check_dir "frontend/api"

# ─── Required files ───────────────────────────────────────────────────────────

echo ""
echo "=== File checks ==="
check_file "README.md"
check_file "NOTICE.md"
check_file "AGENTS.md"

check_file "docs/framework-core.yaml"
check_file "docs/framework-policy.yaml"
check_file "docs/file-structure.yaml"

check_file ".agent/docs/structure-map.yaml"
check_file ".agent/docs/required-paths.yaml"
check_file ".agent/rules/rule.md"
check_file ".agent/skills/structure-check.md"
check_file ".agent/tests/check-structure.sh"
check_file ".agent/tests/check-backend-tests.sh"
check_file ".agent/tests/check-frontend-types.sh"
check_file ".agent/tasks/todo.md"
check_file ".agent/reports/README.md"

check_file ".github/workflows/structure-check.yml"
check_file ".github/workflows/backend-tests.yml"
check_file ".github/workflows/frontend-types.yml"

check_file "db/schema.sql"
check_file "db/topology_tables.sql"
check_file "db/promotion_tables.sql"
check_file "db/seed_empty.sql"
check_file "db/README.md"

check_file "backend/endpoint/DispatchEndpoint.cs"
check_file "backend/runtime/RuntimeExecutor.cs"
check_file "backend/runtime/OperationVectorResolver.cs"
check_file "backend/runtime/AttractorResolver.cs"
check_file "backend/runtime/StructureMapResolver.cs"
check_file "backend/runtime/PackageResolver.cs"
check_file "backend/runtime/SchemaResolver.cs"
check_file "backend/runtime/EmissionBuilder.cs"
check_file "backend/mapper/SemanticMapper.cs"
check_file "backend/repository/TopologyRepository.cs"
check_file "backend/repository/DiffLogRepository.cs"
check_file "backend/guard/RuntimeGuard.cs"
check_file "backend/schema/Contracts.cs"
check_file "backend/README.md"
check_file "backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj"
check_file "backend/tests/Topolactor.Runtime.Tests/RuntimeExecutorTests.cs"
check_file "backend/tests/Topolactor.Runtime.Tests/OperationVectorResolverTests.cs"
check_file "backend/tests/Topolactor.Runtime.Tests/RuntimeGuardTests.cs"

check_file "deno.json"
check_file "frontend/routes/index.tsx"
check_file "frontend/routes/admin/index.tsx"
check_file "frontend/routes/runtime-status.tsx"
check_file "frontend/islands/OperationPanel.tsx"
check_file "frontend/components/ProjectionView.tsx"
check_file "frontend/components/EmissionView.tsx"
check_file "frontend/package/defaultPackage.ts"
check_file "frontend/schema/defaultSchema.ts"
check_file "frontend/registry/componentRegistry.ts"
check_file "frontend/runtime/resolveOperationVector.ts"
check_file "frontend/runtime/renderEmission.ts"
check_file "frontend/runtime/restoreResume.ts"
check_file "frontend/api/dispatch.ts"
check_file "frontend/structure_map.ts"
check_file "frontend/README.md"

# ─── Required content terms ───────────────────────────────────────────────────

echo ""
echo "=== Content checks ==="
check_content "README.md" "data-driven topology runtime"

check_content "AGENTS.md" ".agent/rules/rule.md"
check_content "AGENTS.md" ".agent/tests/check-structure.sh"

check_content ".agent/rules/rule.md" "Data-defined topology is the architecture subject"
check_content ".agent/rules/rule.md" "OperationVector is internal runtime representation"
check_content ".agent/rules/rule.md" "Broken refs are explicit errors"

check_content "docs/file-structure.yaml" "data_defined_topology_is_the_architecture_subject"
check_content "docs/file-structure.yaml" "operation_vector_is_internal_runtime_representation"

check_content "backend/runtime/RuntimeExecutor.cs" "RuntimeExecutor"
check_content "backend/runtime/RuntimeExecutor.cs" "OperationVectorResolver"
check_content "backend/runtime/RuntimeExecutor.cs" "AttractorResolver"
check_content "backend/runtime/RuntimeExecutor.cs" "StructureMapResolver"
check_content "backend/runtime/RuntimeExecutor.cs" "PackageResolver"
check_content "backend/runtime/RuntimeExecutor.cs" "SchemaResolver"
check_content "backend/runtime/RuntimeExecutor.cs" "EmissionBuilder"

check_content "frontend/runtime/resolveOperationVector.ts" "OperationVector"
check_content "frontend/runtime/resolveOperationVector.ts" "attractorKey"

check_content "db/topology_tables.sql" "structure_maps"
check_content "db/topology_tables.sql" "attractor_key"

check_content ".github/workflows/structure-check.yml" ".agent/tests/check-structure.sh"

check_content ".github/workflows/backend-tests.yml" "bash .agent/tests/check-backend-tests.sh"
check_content ".github/workflows/frontend-types.yml" "bash .agent/tests/check-frontend-types.sh"
check_content ".agent/tests/check-backend-tests.sh" "dotnet test"
check_content ".agent/tests/check-frontend-types.sh" "deno check"
check_content "backend/tests/Topolactor.Runtime.Tests/RuntimeExecutorTests.cs" "default:entity:search"
check_content "backend/tests/Topolactor.Runtime.Tests/RuntimeExecutorTests.cs" "ATTRACTOR_RESOLVE_FAILED"
check_content "backend/tests/Topolactor.Runtime.Tests/OperationVectorResolverTests.cs" "default:entity:search"

# ─── Result ───────────────────────────────────────────────────────────────────

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "=== All checks passed ==="
  exit 0
else
  echo "=== $FAILURES check(s) failed ===" >&2
  exit 1
fi
