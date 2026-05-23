#!/usr/bin/env bash
# check-structure.sh — local structure check SSOT
# Verifies required directories, files, and architecture-critical content terms.
# Requires only bash. No credentials, no build tools, no business data.
# Exits non-zero on any failure.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0


if ! command -v rg >/dev/null 2>&1; then
  rg() {
    local opts=()
    while [ $# -gt 0 ]; do
      case "$1" in
        -n|-q|-s|-i|-v) opts+=("$1"); shift ;;
        -P|-U) shift ;;
        --) shift; break ;;
        -*) shift ;;
        *) break ;;
      esac
    done
    local pattern="${1:-}"
    [ $# -gt 0 ] && shift
    grep -E "${opts[@]}" -- "$pattern" "$@"
  }
fi

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



check_checklist_template_clean() {
  local file="$1"
  local bad
  bad="$(grep -En '^Answer:[[:space:]]*(yes|no|n/a)[[:space:]]*$' "$file" || true)"
  if [ -n "$bad" ]; then
    fail "$file contains filled Answer values; template must remain blank"
    return
  fi
  echo "OK  [template-clean] $file"
}

check_tmp_runtime_artifacts() {
  local tmp_dir="$REPO_ROOT/.agent/tmp"
  local leftovers
  leftovers="$(find "$tmp_dir" -mindepth 1 \( -type f -o -type d \) ! -path "$tmp_dir/.gitkeep" | sort || true)"
  if [ -n "$leftovers" ]; then
    fail "Runtime tmp artifacts must be cleaned before completion; found: ${leftovers//$'\n'/, }"
  else
    echo "OK  [tmp]  runtime artifacts cleaned (.gitkeep only)"
  fi
}

check_tmp_tracked_files() {
  local tracked
  tracked="$(git -C "$REPO_ROOT" ls-files .agent/tmp)"
  if [ "$tracked" = ".agent/tmp/.gitkeep" ]; then
    echo "OK  [tmp]  tracked files in .agent/tmp are limited to .gitkeep"
  else
    fail "Tracked files under .agent/tmp must be only .agent/tmp/.gitkeep; found: ${tracked:-<none>}"
  fi
}


check_no_in_progress_todos() {
  local todo_file="$REPO_ROOT/.agent/tasks/todo.md"
  if [ ! -f "$todo_file" ]; then
    fail "TODO marker check skipped (file missing): .agent/tasks/todo.md"
    return
  fi
  if rg -n "^\s*<!--\s*agent:in-progress\s*-->\s*$" "$todo_file" >/dev/null; then
    fail ".agent/tasks/todo.md contains unfinished in-progress marker: <!-- agent:in-progress -->"
  else
    echo "OK  [todo] no in-progress marker in .agent/tasks/todo.md"
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
check_dir ".agent/checklists/fixtures/boundary-identity"
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
check_dir "infra"

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
check_file "docs/design/runtime-orchestration-ssot.yaml"
check_file "docs/design/db-schema.yaml"
check_file "docs/design/sql-attention-logs-ssot.md"
check_file "docs/design/sql-attention-logs-ssot.yaml"

check_file ".agent/README.md"
check_file ".agent/docs/structure-map.yaml"
check_file ".agent/docs/required-paths.yaml"
check_file ".agent/docs/ssot-map.yaml"
check_file ".agent/rules/rule.md"
check_file ".agent/protocols/completion.md"
check_file ".agent/protocols/index.yaml"
check_file ".agent/protocols/policy-judgment.md"
check_file ".agent/protocols/scenario-contract.md"
check_file ".agent/protocols/runtime-boundary-matrix.md"
check_file ".agent/protocols/completion-summary.md"
check_file ".agent/protocols/registry-tensor-policy.md"
check_file ".agent/skills/agent-workflow.md"
check_file ".agent/skills/structure-check.md"
check_file ".agent/tests/check-structure.sh"
check_file ".agent/tests/check-backend-tests.sh"
check_file ".agent/tests/check-frontend-types.sh"
check_file ".agent/tests/check-completion-judgment.sh"
check_file ".agent/tests/check-local-ci.sh"
check_file ".agent/tasks/todo.md"
check_file ".agent/reports/README.md"
check_file ".agent/checklists/policy-judgment.md"
check_file ".agent/checklists/boundary-identity.md"
check_file ".agent/checklists/check-boundary-identity.sh"
check_file ".agent/checklists/check-policy-judgment.sh"
check_file ".agent/checklists/README.md"
check_file ".agent/checklists/fixtures/policy-judgment/pass.md"
check_file ".agent/checklists/fixtures/policy-judgment/fail-unanswered.md"
check_file ".agent/checklists/fixtures/policy-judgment/fail-policy-violation.md"
check_file ".agent/checklists/fixtures/policy-judgment/fail-partial-diff.md"
check_file ".agent/checklists/fixtures/policy-judgment/fail-local-checks.md"
check_file ".agent/checklists/fixtures/policy-judgment/fail-remaining-todos.md"

check_file ".agent/checklists/fixtures/boundary-identity/pass.md"
check_file ".agent/checklists/fixtures/boundary-identity/fail-unanswered.md"
check_file ".agent/checklists/fixtures/boundary-identity/fail-missing-identity.md"
check_file ".agent/checklists/fixtures/boundary-identity/fail-missing-leakage-test.md"
check_file ".agent/checklists/fixtures/boundary-identity/fail-missing-todo-update.md"

check_file ".github/workflows/structure-check.yml"
check_file ".github/workflows/backend-tests.yml"
check_file ".github/workflows/frontend-types.yml"
check_file ".github/workflows/default-entity-search.yml"
check_file ".github/workflows/runtime-semantics.yml"
check_file ".github/workflows/bootstrap-validation.yml"
check_file ".agent/tests/check-default-entity-search.sh"
check_file ".agent/tests/check-bootstrap-validation.sh"
check_file ".agent/tests/check-pipeline-continuity.sh"
check_file ".agent/tests/check-unified-test-gate.sh"
check_file ".agent/tests/check-sql-attention-ssot.sh"
check_file "docs/design/pipeline-continuity-ssot.yaml"

check_content ".agent/tests/check-local-ci.sh" "set -euo pipefail"
check_content ".agent/tests/check-local-ci.sh" "check-unified-test-gate.sh"
check_content ".agent/tests/check-local-ci.sh" "check-runtime-environment.sh"
check_content ".agent/tests/check-local-ci.sh" "check-structure.sh"
check_content ".agent/tests/check-local-ci.sh" "must run last"

check_file ".github/workflows/unified-test-gate.yml"

check_file ".agent/scripts/create-tmp.sh"
check_file ".agent/scripts/delete-tmp.sh"
check_file ".agent/tmp/.gitkeep"

check_file "db/schema.sql"
check_content "db/schema.sql" "CREATE SCHEMA IF NOT EXISTS logs"
check_content "db/schema.sql" "CREATE SCHEMA IF NOT EXISTS topologys"
check_content "db/schema.sql" "CREATE SCHEMA IF NOT EXISTS hubs"
check_file "db/topology_tables.sql"
check_file "db/ui_topology_tables.sql"
check_file "db/promotion_tables.sql"
check_file "db/sql_attention_logs_tables.sql"
check_file "db/seed_empty.sql"
check_file "db/README.md"
check_content "db/sql_attention_logs_tables.sql" "statistics_json"
check_content "db/sql_attention_logs_tables.sql" "phase_vector_json"
check_content "db/sql_attention_logs_tables.sql" "logs.attention"
check_content "db/sql_attention_logs_tables.sql" "logs.current"
check_content "db/sql_attention_logs_tables.sql" "logs.hub_current"
check_content "db/sql_attention_logs_tables.sql" "score_band"
check_content "db/sql_attention_logs_tables.sql" "neighbor_score"
check_content "db/sql_attention_logs_tables.sql" "hub_current_id"

check_file "backend/endpoint/DispatchEndpoint.cs"
check_file "backend/endpoint/SseEndpoint.cs"
check_file "backend/scheduler/RuntimeTimelineScheduler.cs"
check_file "backend/runtime/ManifestDispatcher.cs"
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
check_file "frontend/runtime/frontendScheduler.ts"
check_file "frontend/runtime/sseReceiver.ts"
check_file "frontend/runtime/sseDispatcher.ts"
check_file "frontend/routes/api/sse.ts"
check_file "frontend/api/dispatch.ts"
check_file "frontend/structure_map.ts"
check_file "frontend/README.md"
check_file "frontend/tests/defaultEntitySearch.test.ts"
check_file "frontend/tests/pipelineContinuity.test.ts"

check_file "infra/docker-compose.yml"
check_file "infra/nginx.conf"
check_file "infra/.env.example"

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

check_content "docs/design/sql-attention-logs-ssot.md" "logs.current"
check_content "docs/design/sql-attention-logs-ssot.md" "logs.attention"
check_content "docs/design/sql-attention-logs-ssot.md" "l2 norm"
check_content "docs/design/sql-attention-logs-ssot.md" "physical table"
check_content "docs/design/sql-attention-logs-ssot.md" "norm-level"
check_content "docs/design/sql-attention-logs-ssot.md" "phase_expansion_limit"
check_content "docs/design/sql-attention-logs-ssot.md" "neighbor_score_min"
check_content "docs/design/sql-attention-logs-ssot.md" "phase_vector_json"
check_content "docs/design/sql-attention-logs-ssot.md" "SQL Attention is not topology search"
check_content "docs/design/sql-attention-logs-ssot.md" "hub-attractor exploration"
check_content "docs/design/sql-attention-logs-ssot.md" "SQL Attention is not registry search"
check_content "docs/design/sql-attention-logs-ssot.md" "projection"
check_content "docs/design/sql-attention-logs-ssot.md" "Tensor"
check_content "docs/design/sql-attention-logs-ssot.md" "attractor_key"

check_content ".agent/rules/rule.md" "Runtime Boundary Failure Matrix"
check_content ".agent/protocols/runtime-boundary-matrix.md" "Runtime Boundary Failure Matrix"
check_content ".agent/protocols/index.yaml" "grep route"
check_content ".agent/protocols/index.yaml" "not a judgment SSOT"
check_content ".agent/protocols/index.yaml" "version: 2"
check_content ".agent/protocols/index.yaml" "sections:"
check_content ".agent/protocols/index.yaml" "read_when"
check_content ".agent/protocols/index.yaml" "completion-governance"
check_content ".agent/protocols/index.yaml" "todo-carry-over"
check_content ".agent/protocols/index.yaml" "report-surfaces"
check_content ".agent/protocols/index.yaml" "completion-summary"
check_content ".agent/protocols/completion.md" "Completion Sequence"
check_content ".agent/protocols/policy-judgment.md" "Policy Judgment Gate"
check_content ".agent/protocols/scenario-contract.md" "Temporary Scenario Contract"
check_content ".agent/protocols/report-surfaces.md" "routine inspection reports"
check_content ".agent/protocols/completion.md" "Recursive Verification Gate"
check_content ".agent/protocols/registry-tensor-policy.md" "Registry Tensor Policy"
check_content ".agent/protocols/registry-tensor-policy.md" "tensor basis / vector basis"
check_content ".agent/protocols/registry-tensor-policy.md" "Drift / GAP classification"
check_content "docs/framework-policy.yaml" "registry_table_role: semantic_matrix"
check_content "docs/design/context-route-recommendation.md" "registry table は単なる辞書/設定/metadata ではなく"
check_content "docs/design/context-route-recommendation.yaml" "registry_table_role: semantic_matrix"
check_content ".agent/protocols/registry-tensor-policy.md" "registry table must be explained as semantic matrix"
check_content ".agent/rules/rule.md" "registry-tensor-policy.md"
check_content "docs/framework-policy.yaml" "registry_tensor_principle"
check_content ".agent/scripts/create-tmp.sh" "Runtime Boundary Failure Matrix"
check_content ".agent/docs/structure-map.yaml" "temporary scenario contract"
check_content ".agent/docs/structure-map.yaml" "periodic audit"
check_content ".agent/docs/required-paths.yaml" "Runtime Boundary Failure Matrix"
check_content ".agent/docs/ssot-map.yaml" "required_docs"
check_content ".agent/docs/ssot-map.yaml" "supporting_docs"
check_content ".agent/docs/ssot-map.yaml" "protocols"
check_content ".github/workflows/runtime-semantics.yml" "docs/design/runtime-orchestration-ssot.yaml"
check_content ".github/workflows/runtime-semantics.yml" ".agent/tests/check-runtime-semantics.sh"
check_content ".github/workflows/bootstrap-validation.yml" "db/**/*.sql"
check_content ".github/workflows/bootstrap-validation.yml" "infra/docker-compose.yml"
check_content ".github/workflows/bootstrap-validation.yml" "docker compose -f infra/docker-compose.yml config"
check_content ".github/workflows/bootstrap-validation.yml" "compose smoke"
check_content ".github/workflows/bootstrap-validation.yml" "bash .agent/tests/check-bootstrap-validation.sh"
check_content ".agent/tests/check-bootstrap-validation.sh" "ON_ERROR_STOP=1"
check_content ".agent/tests/check-bootstrap-validation.sh" "db/init.sql"
check_content ".agent/tests/check-bootstrap-validation.sh" "sed 's#/db/#db/#g' db/init.sql"
check_content ".agent/tests/check-bootstrap-validation.sh" "mktemp"
check_content ".agent/tests/check-bootstrap-validation.sh" "trap cleanup EXIT"
check_content ".agent/tests/check-bootstrap-validation.sh" "manifest"
check_content ".agent/tests/check-bootstrap-validation.sh" "ui_component_bucket"
check_content ".agent/tests/check-bootstrap-validation.sh" "ui_topology_tensor"
check_content ".agent/tests/check-bootstrap-validation.sh" "context_event"
check_content ".agent/tests/check-bootstrap-validation.sh" "context_hub_recommendation_current"
check_content "docs/design/runtime-orchestration-ssot.yaml" "runtime_orchestration_ssot"
check_content "docs/design/runtime-orchestration-ssot.yaml" "runtime_timeline_scheduler"
check_content "docs/design/runtime-orchestration-ssot.yaml" "manifest_dispatcher"
check_content "docs/design/runtime-orchestration-ssot.yaml" "topology_transform_runtime"
check_content "docs/design/db-schema.yaml" "db_schema"
check_content "docs/design/db-schema.yaml" "topology_definition"
check_content "docs/design/db-schema.yaml" "manifest"
check_content "docs/design/db-schema.yaml" "wiring_definition_for_dispatcher_and_projection"
check_content ".agent/reports/README.md" "routine inspection reports"
check_content ".agent/reports/README.md" "Do not use this directory as the default place for normal PR summaries"
check_content ".agent/README.md" "Skills are task procedures"
check_content ".agent/README.md" "Protocols are not an always-read bundle"
check_content ".agent/README.md" ".agent Directory Purpose"
check_content ".agent/skills/agent-workflow.md" "STRUCTURE_CHECK"
check_content ".agent/skills/agent-workflow.md" "SCENARIO_CONTRACT"
check_content ".agent/skills/agent-workflow.md" "READ_ENTRY"
check_content ".agent/skills/agent-workflow.md" "READ_TASK_MATERIALS"
check_content ".agent/skills/agent-workflow.md" "READ_TARGET_SURFACES"
check_content ".agent/skills/agent-workflow.md" "DEFINE_SCOPE"

check_content ".agent/rules/rule.md" "Data-defined topology is the architecture subject"
check_content ".agent/rules/rule.md" "OperationVector is internal runtime representation"
check_content ".agent/rules/rule.md" "Broken refs are explicit errors"
check_content ".agent/rules/rule.md" "Workflow Order Invariant"
check_content ".agent/rules/rule.md" "READ_TASK_MATERIALS"
check_content ".agent/rules/rule.md" "READ_TARGET_SURFACES"
check_content ".agent/rules/rule.md" "DEFINE_SCOPE"
check_content ".agent/rules/rule.md" "structure check is not a judgment substitute"
check_content ".agent/protocols/scenario-contract.md" "Precondition: Workflow Order Invariant Gate"
check_content ".agent/protocols/scenario-contract.md" "docs/ SSOT reload"

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

check_content ".github/workflows/structure-check.yml" "check-completion-judgment.sh"
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
check_content ".github/workflows/structure-check.yml" "check-pipeline-continuity.sh"
check_content "docs/design/pipeline-continuity-ssot.yaml" "pipeline_continuity_ssot"
check_content "docs/design/pipeline-continuity-ssot.yaml" "api_command_lane"
check_content "docs/design/pipeline-continuity-ssot.yaml" "sse_projection_lane"
check_content "docs/design/pipeline-continuity-ssot.yaml" "contract_guard"
check_content "docs/design/pipeline-continuity-ssot.yaml" "required_identity"
check_content "docs/design/pipeline-continuity-ssot.yaml" "hardcode_guard"
check_content "docs/design/pipeline-continuity-ssot.yaml" "canonical_route_identity_and_boundary_contract"
check_content "docs/design/pipeline-continuity-ssot.yaml" "pipeline_body_test"
check_content ".agent/tests/check-pipeline-continuity.sh" "check-default-entity-search.sh"
check_content ".agent/tests/check-pipeline-continuity.sh" "hardcode_guard"
check_content "frontend/runtime/frontendScheduler.ts" "queueClientCommand"
check_content "frontend/runtime/sseDispatcher.ts" "SseDispatcher"
check_content "frontend/runtime/sseReceiver.ts" "SseReceiver"
check_content "frontend/routes/api/sse.ts" "text/event-stream"
check_content "frontend/tests/pipelineContinuity.test.ts" "sseDispatcher"
check_content "frontend/tests/pipelineContinuity.test.ts" "queueClientCommand"
check_content "backend/scheduler/RuntimeTimelineScheduler.cs" "RuntimeTimelineScheduler"
check_content "backend/runtime/ManifestDispatcher.cs" "ManifestDispatcher"
check_content "backend/endpoint/SseEndpoint.cs" "text/event-stream"
check_content "backend/tests/Topolactor.Integration.Tests/DefaultEntitySearchIntegrationTests.cs" "default:entity:search"
check_content "backend/tests/Topolactor.Integration.Tests/DefaultEntitySearchIntegrationTests.cs" "ATTRACTOR_RESOLVE_FAILED"
check_content "frontend/tests/defaultEntitySearch.test.ts" "renderEmission"
check_content "frontend/tests/defaultEntitySearch.test.ts" "ATTRACTOR_RESOLVE_FAILED"

check_content ".github/workflows/unified-test-gate.yml" "FUNCTION_BOUNDARY"
check_content ".github/workflows/unified-test-gate.yml" "RUNTIME_INTEGRATION"
check_content ".github/workflows/unified-test-gate.yml" "FRONTEND_CONTRACT"
check_content ".github/workflows/unified-test-gate.yml" "STRUCTURE_GATE"
check_content ".github/workflows/unified-test-gate.yml" "bash .agent/tests/check-unified-test-gate.sh"
check_content ".github/workflows/unified-test-gate.yml" "bash .agent/tests/check-structure.sh"
check_content ".agent/tests/check-unified-test-gate.sh" "FUNCTION_BOUNDARY"
check_content ".agent/tests/check-unified-test-gate.sh" "RUNTIME_INTEGRATION"
check_content ".agent/tests/check-unified-test-gate.sh" "FRONTEND_CONTRACT"
check_content ".agent/tests/check-unified-test-gate.sh" "NOT_COVERED"
check_content ".agent/tests/check-unified-test-gate.sh" "dotnet test"
check_content ".agent/tests/check-unified-test-gate.sh" "deno test"
check_content ".agent/tests/check-unified-test-gate.sh" "missing tool is not a pass"

check_content "docs/registrar-admin-ui-specification.md" "Registrar admin UI"
check_content "docs/registrar-admin-ui-specification.md" "controlled registration boundary"
check_content "docs/registrar-admin-ui-specification.md" "Draft registration"
check_content "docs/registrar-admin-ui-specification.md" "Validate refs"
check_content "docs/registrar-admin-ui-specification.md" "Promote to active registry state"
check_content "docs/registrar-admin-ui-specification.md" "Broken refs are explicit validation errors"
check_content "docs/registrar-admin-ui-specification.md" "not CRUD"
check_content "docs/registrar-admin-ui-specification.md" "frontend must not become source of truth"
check_content "docs/registrar-admin-ui-specification.md" "components bucket"
check_content "docs/registrar-admin-ui-specification.md" "package generator"
check_content "db/ui_topology_tables.sql" "ui_component_bucket"
check_content "db/ui_topology_tables.sql" "ui_topology_tensor"

check_file "docs/promotion-manifest-editor-specification.md"
check_file "docs/design/context-route-recommendation.md"
check_file "docs/design/context-route-recommendation.yaml"
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
check_content ".agent/checklists/boundary-identity.md" "End-to-End Boundary Identity"
check_content ".agent/checklists/boundary-identity.md" "Multi-instance leakage"
check_content ".agent/checklists/boundary-identity.md" "minimum leakage detection test"
check_content ".agent/checklists/boundary-identity.md" "repository mutation identity"
check_content ".agent/checklists/boundary-identity.md" "Frontend projection identity"
check_content ".agent/checklists/boundary-identity.md" "UI action identity"
check_content ".agent/checklists/check-boundary-identity.sh" "Q1=yes AND Q12=no AND Q13=no"
check_content ".agent/checklists/check-boundary-identity.sh" "--self-test"
check_content "AGENTS.md" "check-policy-judgment.sh"
check_content "AGENTS.md" "Policy Judgment Gate"
check_content ".agent/protocols/completion.md" "Remote CI Equivalence Gate"
check_content ".agent/protocols/completion.md" "Structure Check is the always-on required gate."
check_content ".agent/protocols/completion.md" "scope-irrelevant workflow-level skip is not blocking."
check_content ".agent/protocols/completion.md" "NOT_REQUIRED / OUT_OF_SCOPE declarations must be explicit"
check_content ".agent/protocols/policy-judgment.md" "CI queued/in_progress is not PASS"
check_content ".agent/protocols/policy-judgment.md" "scope-irrelevant workflow-level skip is not blocking"
check_content ".agent/checklists/policy-judgment.md" "equivalent remote CI success"
check_content ".agent/checklists/policy-judgment.md" "Structure Check is always-on"
check_content ".agent/scripts/create-tmp.sh" "Remote CI Equivalence Gate"


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
check_content ".agent/protocols/scenario-contract.md" "Boundary Extension Scenario"
check_content ".agent/protocols/scenario-contract.md" "Multi-instance leakage"
check_content ".agent/protocols/scenario-contract.md" "Frontend projection identity"
check_content ".agent/protocols/scenario-contract.md" "UI action identity"
check_content ".agent/protocols/runtime-boundary-matrix.md" "End-to-End Boundary Identity"
check_content ".agent/protocols/runtime-boundary-matrix.md" "Repository mutation identity"
check_content ".agent/protocols/runtime-boundary-matrix.md" "Frontend projection identity"
check_content ".agent/protocols/runtime-boundary-matrix.md" "UI action identity"
check_content ".agent/rules/rule.md" "End-to-End Boundary Identity"
check_content ".agent/rules/rule.md" "Multi-instance leakage"
check_content ".agent/scripts/create-tmp.sh" "Boundary Extension Scenario"
check_content ".agent/scripts/create-tmp.sh" "Multi-instance leakage scenario"
check_content ".agent/scripts/create-tmp.sh" "Frontend projection identity"
check_content ".agent/scripts/create-tmp.sh" "UI action identity"
check_content ".agent/checklists/policy-judgment.md" "scenario contract"
check_content ".agent/checklists/check-policy-judgment.sh" "scenario contract"
check_content ".github/workflows/structure-check.yml" "check-sql-attention-ssot.sh"

echo ""
echo "=== Template checklist pollution guard ==="
check_checklist_template_clean "$REPO_ROOT/.agent/checklists/policy-judgment.md"
check_checklist_template_clean "$REPO_ROOT/.agent/checklists/boundary-identity.md"

echo ""
echo "=== Checklist self-tests ==="
bash "$REPO_ROOT/.agent/checklists/check-policy-judgment.sh" --self-test
bash "$REPO_ROOT/.agent/checklists/check-boundary-identity.sh" --self-test

TMP_MEMO_PATH="$REPO_ROOT/.agent/tmp/tmp.txt"
if [ -f "$TMP_MEMO_PATH" ]; then
  fail "Temporary scenario contract must be deleted before completion: .agent/tmp/tmp.txt"
else
  echo "OK  [tmp]  .agent/tmp/tmp.txt absent"
fi

check_tmp_runtime_artifacts
check_tmp_tracked_files
check_no_in_progress_todos

# Protocol split guard
if grep -q "## Completion Sequence (Mandatory)" "$REPO_ROOT/.agent/rules/rule.md"; then
  fail "rule.md must not contain long-form Completion Sequence section; keep procedure in .agent/protocols/completion.md"
else
  echo "OK  [split] rule.md completion procedure remains split"
fi


check_content ".agent/rules/rule.md" "Audit Gap Response Gate"
check_content ".agent/protocols/completion-summary.md" "### 残タスク引き継ぎ指示"
check_content ".agent/protocols/completion-summary.md" "### output sink state"
check_content ".agent/protocols/completion-summary.md" "### test結果"
check_content ".agent/protocols/completion.md" "Audit Gap Response Gate"

check_content ".agent/rules/rule.md" "Failure Triage Self-Recursion Gate"
check_content ".agent/protocols/completion.md" "failure triage result"
check_content ".agent/protocols/completion.md" "Apply Failure Triage Self-Recursion Gate"
check_content ".agent/protocols/completion-summary.md" "This section is Auditor TODO input"
check_content ".agent/rules/rule.md" "Required Check Scope Declaration Gate"
check_content ".agent/protocols/completion.md" "required check scope declaration"
check_content ".agent/protocols/completion.md" "REQUIRED_EXECUTED"
check_content ".agent/protocols/completion-summary.md" "#### Required check scope"

check_content ".agent/rules/rule.md" "Details:"
check_content ".agent/rules/rule.md" ".agent/protocols/completion-summary.md"
check_content ".agent/rules/rule.md" ".agent/protocols/completion.md"

if rg -n "REQUIRED_EXECUTED|required check failure|expected negative test|Governance Gaps|Proposed Governance Improvements|Completion Eligibility" "$REPO_ROOT/.agent/rules/rule.md" >/dev/null; then
  fail "rule.md must remain compact for gate details; move detailed classifications/sections to protocols"
else
  echo "OK  [compact] rule.md gate details remain in protocols"
fi


# SQL Attention target-boundary negative checks
if rg -n "logs\.registry_current" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "registry_current should not remain in SQL Attention SSOT"
else
  echo "OK  [ssot] registry_current removed from SQL Attention target docs"
fi
if rg -n "registry-neighbor exploration" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "registry-neighbor exploration should not remain as SQL Attention target"
else
  echo "OK  [ssot] registry-neighbor exploration removed from SQL Attention target docs"
fi
if rg -n "registry_composition_neighbors|registry_composition_tables" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "registry_composition_neighbors/tables should not remain"
else
  echo "OK  [ssot] registry_composition_neighbors/tables should not remain"
fi
if rg -n "scheduler_runtime_registry_neighbor_exploration" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "scheduler_runtime_registry_neighbor_exploration should not remain"
else
  echo "OK  [ssot] scheduler_runtime_registry_neighbor_exploration should not remain"
fi

if rg -n "Initial registry_kind candidates" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "Initial registry_kind candidates should not remain"
else
  echo "OK  [ssot] Initial registry_kind candidates removed"
fi
if rg -n "deprecated_or_rejected:[\s\S]*logs\.hub_current" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "logs.hub_current must not be in deprecated_or_rejected"
else
  echo "OK  [ssot] logs.hub_current not in deprecated_or_rejected"
fi
if ! rg -n "physical_tables" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null || ! rg -n "logs\.hub_current" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "physical_tables must include logs.hub_current"
else
  echo "OK  [ssot] physical_tables includes logs.hub_current"
fi
if rg -n "\bregistry_table\b|\bregistry_id\b" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "registry_table/registry_id should not remain in SQL Attention attention contract"
else
  echo "OK  [ssot] registry_table/registry_id removed from SQL Attention attention contract"
fi

# ─── Result ───────────────────────────────────────────────────────────────────

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "=== All checks passed ==="
  echo "Use .agent/protocols/completion-summary.md / ## Completion Summary Template for the final completion summary."
  echo "Do not replace the final completion summary with PR body or make_pr output."
  exit 0
else
  echo "=== $FAILURES check(s) failed ===" >&2
  exit 1
fi
