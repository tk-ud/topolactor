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
  if grep -qF -- "$term" "$file"; then
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
check_dir ".agent/protocols"
check_dir ".agent/skills"
check_dir ".agent/tests"
check_dir ".agent/tasks"
check_dir ".agent/reports"
check_dir ".agent/checklists"
check_dir ".agent/checklists/fixtures/policy-judgment"
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

check_file "docs/agent-development-os.md"
check_file "docs/framework-core.yaml"
check_file "docs/framework-policy.yaml"
check_file "docs/file-structure.yaml"
check_file "docs/registrar-admin-ui-specification.md"

check_file ".agent/docs/structure-map.yaml"
check_file ".agent/docs/required-paths.yaml"
check_file ".agent/rules/rule.md"
check_file ".agent/protocols/completion.md"
check_file ".agent/protocols/policy-judgment.md"
check_file ".agent/protocols/scenario-contract.md"
check_file ".agent/protocols/runtime-boundary-matrix.md"
check_file ".agent/protocols/reports-and-todos.md"
check_file ".agent/skills/structure-check.md"
check_file ".agent/tests/check-structure.sh"
check_file ".agent/tests/check-backend-tests.sh"
check_file ".agent/tests/check-frontend-types.sh"
check_file ".agent/tasks/todo.md"
check_file ".agent/reports/README.md"
check_file ".agent/checklists/policy-judgment.md"
check_file ".agent/checklists/check-policy-judgment.sh"
check_file ".agent/checklists/fixtures/policy-judgment/pass.md"
check_file ".agent/checklists/fixtures/policy-judgment/fail-unanswered.md"
check_file ".agent/checklists/fixtures/policy-judgment/fail-policy-violation.md"
check_file ".agent/checklists/fixtures/policy-judgment/fail-partial-diff.md"
check_file ".agent/checklists/fixtures/policy-judgment/fail-local-checks.md"
check_file ".agent/checklists/fixtures/policy-judgment/fail-remaining-todos.md"

check_file ".github/workflows/structure-check.yml"
check_file ".github/workflows/backend-tests.yml"
check_file ".github/workflows/frontend-types.yml"
check_file ".github/workflows/default-entity-search.yml"
check_file ".agent/tests/check-default-entity-search.sh"

check_file ".agent/scripts/create-tmp.sh"
check_file ".agent/scripts/delete-tmp.sh"
check_file ".agent/tmp/.gitkeep"

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
check_file "backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj"
check_file "backend/tests/Topolactor.Integration.Tests/DefaultEntitySearchIntegrationTests.cs"

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
check_file "frontend/tests/defaultEntitySearch.test.ts"

# ─── Required content terms ───────────────────────────────────────────────────

echo ""
echo "=== Content checks ==="
check_content "README.md" "data-driven topology runtime"
check_content "README.md" "docs/agent-development-os.md"

check_content "docs/agent-development-os.md" "external overview and agenda"
check_content "docs/agent-development-os.md" "governance layer"
check_content "docs/agent-development-os.md" "not application runtime logic"
check_content "docs/agent-development-os.md" "AGENTS.md"

check_content "AGENTS.md" ".agent/rules/rule.md"
check_content "AGENTS.md" ".agent/tests/check-structure.sh"

check_content "AGENTS.md" "Agent Contract"
check_content "AGENTS.md" "Runtime Boundary Failure Matrix"
check_content ".agent/rules/rule.md" "Runtime Boundary Failure Matrix"
check_content ".agent/protocols/runtime-boundary-matrix.md" "Runtime Boundary Failure Matrix"
check_content ".agent/protocols/completion.md" "Completion Sequence"
check_content ".agent/protocols/policy-judgment.md" "Policy Judgment Gate"
check_content ".agent/protocols/scenario-contract.md" "Temporary Scenario Contract"
check_content ".agent/protocols/reports-and-todos.md" "routine inspection reports"
check_content ".agent/protocols/reports-and-todos.md" "Recursive Verification Gate"
check_content ".agent/scripts/create-tmp.sh" "Runtime Boundary Failure Matrix"
check_content ".agent/docs/structure-map.yaml" "temporary scenario contract"
check_content ".agent/docs/structure-map.yaml" "periodic audit"
check_content ".agent/docs/required-paths.yaml" "Runtime Boundary Failure Matrix"
check_content ".agent/reports/README.md" "routine inspection reports"
check_content ".agent/reports/README.md" "Do not use this directory as the default place for normal PR summaries"

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

check_content ".agent/tests/check-default-entity-search.sh" "dotnet test"
check_content ".agent/tests/check-default-entity-search.sh" "deno test"
check_content ".github/workflows/default-entity-search.yml" "bash .agent/tests/check-default-entity-search.sh"
check_content "backend/tests/Topolactor.Integration.Tests/DefaultEntitySearchIntegrationTests.cs" "default:entity:search"
check_content "backend/tests/Topolactor.Integration.Tests/DefaultEntitySearchIntegrationTests.cs" "ATTRACTOR_RESOLVE_FAILED"
check_content "frontend/tests/defaultEntitySearch.test.ts" "renderEmission"
check_content "frontend/tests/defaultEntitySearch.test.ts" "ATTRACTOR_RESOLVE_FAILED"

check_content "docs/registrar-admin-ui-specification.md" "Registrar admin UI"
check_content "docs/registrar-admin-ui-specification.md" "controlled registration boundary"
check_content "docs/registrar-admin-ui-specification.md" "Draft registration"
check_content "docs/registrar-admin-ui-specification.md" "Validate refs"
check_content "docs/registrar-admin-ui-specification.md" "Promote to active registry state"
check_content "docs/registrar-admin-ui-specification.md" "Broken refs are explicit validation errors"
check_content "docs/registrar-admin-ui-specification.md" "not CRUD"
check_content "docs/registrar-admin-ui-specification.md" "frontend must not become source of truth"

check_file "docs/promotion-manifest-editor-specification.md"
check_content "docs/promotion-manifest-editor-specification.md" "Promotion manifest editor"
check_content "docs/promotion-manifest-editor-specification.md" "controlled manifest editing boundary"
check_content "docs/promotion-manifest-editor-specification.md" "Draft manifest"
check_content "docs/promotion-manifest-editor-specification.md" "Validate refs and disclosure requirements"
check_content "docs/promotion-manifest-editor-specification.md" "Promote manifest version"
check_content "docs/promotion-manifest-editor-specification.md" "Broken refs must remain explicit validation errors"
check_content "docs/promotion-manifest-editor-specification.md" "frontend must not become source of truth"
check_content "docs/promotion-manifest-editor-specification.md" "disclosure text must be explicit"
check_content "docs/promotion-manifest-editor-specification.md" "editor does not execute runtime"

check_content ".agent/checklists/policy-judgment.md" "Q1"
check_content ".agent/checklists/policy-judgment.md" "Q15"
check_content ".agent/checklists/policy-judgment.md" "Answer:"
check_content ".agent/checklists/check-policy-judgment.sh" "Answer:"
check_content ".agent/checklists/check-policy-judgment.sh" "--self-test"
check_content "AGENTS.md" "check-policy-judgment.sh"
check_content "AGENTS.md" "Policy Judgment Gate"
check_content ".agent/protocols/completion.md" "Remote CI Equivalence Gate"
check_content ".agent/protocols/completion.md" "Structure Check is the always-on required gate."
check_content ".agent/protocols/completion.md" "scope-irrelevant workflow-level skip is not blocking."
check_content ".agent/protocols/policy-judgment.md" "CI queued/in_progress is not PASS"
check_content ".agent/protocols/policy-judgment.md" "scope-irrelevant workflow-level skip is not blocking"
check_content ".agent/checklists/policy-judgment.md" "equivalent remote CI success"
check_content ".agent/checklists/policy-judgment.md" "Structure Check is always-on"
check_content ".agent/scripts/create-tmp.sh" "Remote CI Equivalence Gate"
check_content "README.md" "IF_LOCAL_NOT_EXECUTED_VERIFY_REMOTE_CI_EQUIVALENT"
check_content "README.md" "Structure Check is the always-on required gate."
check_content "README.md" "Heavy CI workflows are path-scoped."
check_content "README.md" "Scope-irrelevant skipped heavy CI is not blocking."
check_content ".agent/rules/rule.md" "Policy Judgment Gate"


check_content "AGENTS.md" "Temporary Scenario Contract"
check_content ".agent/rules/rule.md" "Temporary Scenario Contract"
check_content ".agent/rules/rule.md" "scenario contract"
check_content ".agent/scripts/create-tmp.sh" "Temporary Scenario Contract"

check_content "AGENTS.md" "Recursive Verification Gate"
check_content ".agent/protocols/completion.md" "Recursive Verification Gate"
check_content ".agent/protocols/policy-judgment.md" "Recursive Verification Gate"
check_content ".agent/protocols/scenario-contract.md" "Recursive Verification Gate"
check_content ".agent/protocols/runtime-boundary-matrix.md" "Recursive Verification Gate"
check_content ".agent/checklists/policy-judgment.md" "recursion to fix phase"
check_content ".agent/scripts/create-tmp.sh" "Blocking failures found:"
check_content ".agent/scripts/create-tmp.sh" "Expected read / write / append / cache / return order"
check_content ".agent/checklists/policy-judgment.md" "scenario contract"
check_content ".agent/checklists/check-policy-judgment.sh" "scenario contract"

TMP_MEMO_PATH="$REPO_ROOT/.agent/tmp/tmp.txt"
if [ -f "$TMP_MEMO_PATH" ]; then
  fail "Temporary scenario contract must be deleted before completion: .agent/tmp/tmp.txt"
else
  echo "OK  [tmp]  .agent/tmp/tmp.txt absent"
fi

# Protocol split guard
if grep -q "## Completion Sequence (Mandatory)" "$REPO_ROOT/.agent/rules/rule.md"; then
  fail "rule.md must not contain long-form Completion Sequence section; keep procedure in .agent/protocols/completion.md"
else
  echo "OK  [split] rule.md completion procedure remains split"
fi

# ─── Result ───────────────────────────────────────────────────────────────────

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "=== All checks passed ==="
  exit 0
else
  echo "=== $FAILURES check(s) failed ===" >&2
  exit 1
fi

