#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0
fail(){ echo "FAIL: $1" >&2; FAILURES=$((FAILURES+1)); }
pass(){ echo "OK  : $1"; }

check_file(){ local f="$1"; [ -f "$REPO_ROOT/$f" ] && pass "file exists: $f" || fail "missing file: $f"; }
check_term(){ local f="$1" t="$2"; grep -qF -- "$t" "$REPO_ROOT/$f" && pass "$f -> $t" || fail "$f missing: $t"; }
check_absent(){ local f="$1" t="$2"; ! grep -qF -- "$t" "$REPO_ROOT/$f" && pass "$f excludes: $t" || fail "$f contains forbidden: $t"; }

section(){ awk -v start="$2" -v end="$3" '
  $0 ~ start { in_section=1 }
  in_section { print }
  in_section && $0 ~ end { exit }
' "$REPO_ROOT/$1"; }

echo "=== Abstract function completion alignment evidence check ==="

check_file ".agent/protocols/completion.md"
check_file "tasks/refactor_todo.md"
check_file "backend/tests/Topolactor.Runtime.Tests/AbstractFunctionExecutorTests.cs"
check_file "backend/tests/Topolactor.Runtime.Tests/FileStorageBundleDispatchTests.cs"
check_file "backend/tests/Topolactor.Integration.Tests/FileStoragePortConsumerLiveDbTests.cs"
check_file "backend/repository/NpgsqlExternalPortDbFunctionRepository.cs"

# Completion gate must define lane-specific evidence and migration-order constraints, not just an implemented label.
check_term ".agent/protocols/completion.md" "Abstract Function Bundle Completion Alignment Gate"
check_term ".agent/protocols/completion.md" "backend primitive executor / policy step execution"
check_term ".agent/protocols/completion.md" "seed or migration for an absorption target"
check_term ".agent/protocols/completion.md" "runtime DB mutation returning to projection or read model"
check_term ".agent/protocols/completion.md" "runtime result returning to frontend"
check_term ".agent/protocols/completion.md" "SQL Attention / recommendation primitive"
check_term ".agent/protocols/completion.md" "credential primitive"
check_term ".agent/protocols/completion.md" "concrete function/handler deletion or explicit compatibility-fallback classification"

# The implemented Bundle must name the executable evidence surfaces it relies on.
BUNDLE_BLOCK="$(section "tasks/refactor_todo.md" '## Bundle `completion-gate-and-test-alignment`' '## Bundle `projection-manifest-primitive-migration`')"
[[ "$BUNDLE_BLOCK" == *'Status: `implemented`'* ]] && pass "completion-gate-and-test-alignment implemented status present" || fail "completion-gate-and-test-alignment not implemented"
[[ "$BUNDLE_BLOCK" == *'.agent/tests/check-abstract-function-completion-alignment.sh'* ]] && pass "Bundle references executable alignment check" || fail "Bundle does not reference executable alignment check"
[[ "$BUNDLE_BLOCK" == *'AbstractFunctionExecutorTests'* ]] && pass "Bundle references backend primitive executor tests" || fail "Bundle missing backend primitive executor test reference"
[[ "$BUNDLE_BLOCK" == *'FileStorageBundleDispatchTests'* ]] && pass "Bundle references file-storage runtime/seed tests" || fail "Bundle missing file-storage runtime/seed test reference"
[[ "$BUNDLE_BLOCK" == *'FileStoragePortConsumerLiveDbTests'* ]] && pass "Bundle references live DB integration proof" || fail "Bundle missing live DB integration proof reference"
[[ "$BUNDLE_BLOCK" == *'compatibility stub'* ]] && pass "Bundle records compatibility fallback classification" || fail "Bundle missing compatibility fallback classification"
[[ "$BUNDLE_BLOCK" == *'future targets remain in their own not_started Bundles'* ]] && pass "Bundle keeps future absorption targets separate" || fail "Bundle hides future absorption targets"

# Current Bundle status map must reflect credential hardening as implemented.
check_term "tasks/refactor_todo.md" '| `credential-primitive-hardening` | implemented |'
check_term "tasks/refactor_todo.md" '| `backend-abstract-function-executor` | partial_compatibility_fallback |'

# Backend primitive executor evidence: order, result context binding, fail-close, policy-step execution, no provider/bundle branching.
check_term "backend/tests/Topolactor.Runtime.Tests/AbstractFunctionExecutorTests.cs" "ExecuteAsync_UsesManifestOrderAndBindsResultContext"
check_term "backend/tests/Topolactor.Runtime.Tests/AbstractFunctionExecutorTests.cs" "ExecuteAsync_FailsClosedWithExplicitStatus"
check_term "backend/tests/Topolactor.Runtime.Tests/AbstractFunctionExecutorTests.cs" "ExternalPortPolicyStepExecutor_ExecuteAbstractFunction_UsesExecutorInsteadOfDbFallback"
check_term "backend/tests/Topolactor.Runtime.Tests/AbstractFunctionExecutorTests.cs" "RuntimeExecutor_DoesNotReferenceProviderOrBundleBranches"
check_term "backend/tests/Topolactor.Runtime.Tests/AbstractFunctionExecutorTests.cs" "Assert.DoesNotContain(\"functionName switch\""

# File-storage absorption evidence: target seed/runtime tests prove execute_abstract_function and concrete deletion/denial checks.
check_term "backend/tests/Topolactor.Runtime.Tests/FileStorageBundleDispatchTests.cs" "SeedSql_AttachmentPolicies_UseExecuteAbstractFunctionNotDbFunction"
check_term "backend/tests/Topolactor.Runtime.Tests/FileStorageBundleDispatchTests.cs" "NpgsqlExternalPortDbFunctionRepository_AfterMigration_HasNoConcreteFsStarFunctions"
check_term "backend/tests/Topolactor.Runtime.Tests/FileStorageBundleDispatchTests.cs" "Assert.DoesNotContain(\"topology.fs_record_export_job\", source)"
check_term "backend/tests/Topolactor.Runtime.Tests/FileStorageBundleDispatchTests.cs" "topology.fs_bind_record_file_attachment"
check_term "backend/tests/Topolactor.Integration.Tests/FileStoragePortConsumerLiveDbTests.cs" "Assert.Equal(\"execute_abstract_function\", step11.OperationKey)"
check_term "backend/tests/Topolactor.Integration.Tests/FileStoragePortConsumerLiveDbTests.cs" "Assert.Equal(\"execute_abstract_function\", reader.GetString(0))"
check_term "backend/tests/Topolactor.Integration.Tests/FileStoragePortConsumerLiveDbTests.cs" "Assert.DoesNotContain(\"signed_url\", listJson"

# Credential primitive hardening evidence: C# violation fixes, seed evidence, DI wiring, no branching.
check_term "backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs" "CredentialPrimitive_BuildHttpRequest_RequiresExplicitMethod"
check_term "backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs" "CredentialPrimitive_ParseTokenRefreshResult_RequiresCryptoAdapter"
check_term "backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs" "CredentialPrimitive_ParseTokenRefreshResult_RequiresExpiresAtKey"
check_term "backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs" "SeedSql_CredentialRefreshPolicy_UsesExecuteAbstractFunction"
check_term "backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs" "SeedSql_BuildHttpRequest_AllStepsHaveExplicitMethod"
check_term "backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs" "Program_RegistersAllSixCredentialPrimitiveAdapters"
check_term "backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs" "CredentialPrimitives_DoNotBranchOnProviderKindOrRequiredByBundle"
# Non-2xx fail-close, seed-route test, and compensation test.
check_term "backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs" "CredentialHttpRequestAdapter_Non2xxResponse_FailsClose"
check_term "backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs" "SeedSql_CredentialRefreshManifest_ExecutesFullChainThroughAbstractFunctionExecutor"
check_term "backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs" "CredentialRefreshManifest_WhenHttpStepFails_CompensationCallsFailLease"
# Seed must contain compensation step (bf1b) and CredentialFailLeaseAdapter.
check_term "db/seed_empty.sql" "credential_fail_lease"
check_term "db/seed_empty.sql" "is_compensation_step"
check_term "backend/runtime/AbstractFunctionRuntime.cs" "CredentialFailLeaseAdapter"
check_term "backend/Program.cs" "CredentialFailLeaseAdapter"

# Compatibility fallback is explicit and fail-closed, not hidden as implemented runtime behavior.
check_term "backend/repository/NpgsqlExternalPortDbFunctionRepository.cs" "Compatibility placeholder for the execute_db_function operation_key"
check_term "backend/repository/NpgsqlExternalPortDbFunctionRepository.cs" "No per-function switch or payload-derived table authority exists here"
check_term "backend/repository/NpgsqlExternalPortDbFunctionRepository.cs" "EXTERNAL_PORT_DB_FUNCTION_UNKNOWN"
check_absent "backend/repository/NpgsqlExternalPortDbFunctionRepository.cs" "case \"topology.fs_"
check_absent "backend/repository/NpgsqlExternalPortDbFunctionRepository.cs" "topology.fs_record_export_job"

if [ "$FAILURES" -eq 0 ]; then
  echo "=== Abstract function completion alignment evidence check passed ==="
  exit 0
else
  echo "=== $FAILURES abstract function completion alignment check(s) failed ===" >&2
  exit 1
fi
