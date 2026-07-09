#!/usr/bin/env bash
set -euo pipefail

ROOT=$(cd "$(dirname "$0")/../.." && pwd)
cd "$ROOT"

fail() { echo "FAIL $*" >&2; exit 1; }

if ! command -v rg >/dev/null 2>&1; then
  rg() {
    local fixed=0
    local line_numbers=0
    local pattern=""

    while [ "$#" -gt 0 ]; do
      case "$1" in
        -n)
          line_numbers=1
          shift
          ;;
        --fixed-strings|-F)
          fixed=1
          shift
          ;;
        --glob|-g)
          shift
          [ "$#" -gt 0 ] && shift
          ;;
        --)
          shift
          break
          ;;
        -*)
          shift
          ;;
        *)
          pattern="$1"
          shift
          break
          ;;
      esac
    done

    [ -n "$pattern" ] || return 2

    local args=()
    [ "$line_numbers" -eq 1 ] && args+=(-n)
    # Match rg behavior: when no path arguments are supplied, read from stdin.
    # Do not let grep -R default to searching the repository, because pipeline
    # checks must only inspect their piped block content.
    if [ "$#" -gt 0 ]; then
      args=(-R "${args[@]}")
    fi
    if [ "$fixed" -eq 1 ]; then
      grep "${args[@]}" -F -- "$pattern" "$@"
    else
      grep "${args[@]}" -E -- "$pattern" "$@"
    fi
  }
fi

require_term() {
  local term="$1" file="$2" desc="$3"
  rg -n --fixed-strings "$term" "$file" >/dev/null || fail "missing ${desc}: ${term} in ${file}"
}

SSOT="docs/design/instance-port-substrate-ssot.yaml"
[[ -f "$SSOT" ]] || fail "missing instance port SSOT: $SSOT"

require_term "instance_port_substrate" "$SSOT" "substrate name"
require_term "db_instance_port" "$SSOT" "db instance port kind"
require_term "runtime_instance_port" "$SSOT" "runtime instance port kind"
require_term "call_instance_postgres_function" "$SSOT" "instance postgres primitive"
require_term "call_bound_instance_function" "$SSOT" "bound instance primitive"
require_term "credential_backed_instance_function" "$SSOT" "credential-backed function contract"
require_term "provider_kind_is_data_only" "$SSOT" "provider_kind data-only rule"
require_term "required_by_bundle_is_data_only" "$SSOT" "required_by_bundle data-only rule"
require_term "plaintext_connection_string_forbidden_in_seed_ssot_projection_log" "$SSOT" "plaintext connection string prohibition"
require_term "provider_specific_runtime_handler_as_primary_design" "$SSOT" "provider-specific runtime handler prohibition"
require_term "external_instance_as_runtime_SSOT" "$SSOT" "external instance SSOT prohibition"
require_term "sibling_substrate_not_external_port_extension" "$SSOT" "sibling substrate decision"

require_term "instance_port_substrate_ref: docs/design/instance-port-substrate-ssot.yaml" docs/design/external-port-substrate-ssot.yaml "external-port sibling reference"
require_term "instance_port_runtime" docs/design/external-port-substrate-ssot.yaml "instance lane distinction"
require_term "product.instance_port_substrate" docs/system-roadmap.yaml "roadmap bundle"
# Exclude lines that merely cite docs/design/instance-port-substrate-ssot.yaml
# as a supporting/reference doc for a DIFFERENT bundle (legitimate -- other
# not_started bundles, e.g. admin-surface-topology-seed-conversion, may need
# to reference this SSOT without reopening instance-port-substrate itself as
# active work). Only a genuinely dedicated instance-port-substrate bundle
# entry (its own Bundle ID / heading / roadmap-bundle reference / implementation
# TODO file), not a bare SSOT-doc citation, should fail this check.
#
# Filter with the real `grep -v` (a coreutils binary, always present) rather
# than `rg -v`: this file's own top-of-file `rg` compatibility shim (used
# when the real ripgrep binary is not installed on the runner) does not
# recognize -v and silently drops it, which would make an `rg -v` filter here
# a silent no-op under that shim instead of an inversion.
TODO_MATCHES="$(rg -n 'instance-port-substrate|product.instance_port_substrate|instance-port-substrate-implementation-todo' .agent/tasks/todo.md || true)"
TODO_MATCHES_WITHOUT_SSOT_DOC_CITATIONS="$(printf '%s\n' "$TODO_MATCHES" | grep -v -F 'instance-port-substrate-ssot.yaml' || true)"
if [ -n "$TODO_MATCHES_WITHOUT_SSOT_DOC_CITATIONS" ]; then
  printf '%s\n' "$TODO_MATCHES_WITHOUT_SSOT_DOC_CITATIONS"
  fail "implemented instance-port-substrate must not remain in active unfinished TODO surface"
fi
if [[ -e .agent/tasks/instance-port-substrate-implementation-todo.md ]]; then
  fail "implemented instance-port-substrate implementation TODO must be deleted or retired from active task surfaces"
fi

CREDENTIAL_SEED_BLOCK=$(sed -n '/auth\/external credential management topology projection/,/external_port_substrate canonical physical binding catalog/p' db/seed_empty.sql)
for term in \
  'auth.external.credential_management.projection' \
  'credential_management_categories' \
  'user_auth' \
  'external' \
  'instance_settings' \
  'select_mode_category' \
  'db_instance_port' \
  'runtime_instance_port' \
  'instance_connection_policy' \
  'instance_operation_authority_binding' \
  'instance_settings_json_template' \
  'download_json_template' \
  'import_json_template' \
  'validate_preview_apply_required' \
  'public_safe_shape_only' \
  'dispatchInstanceOperation' \
  'instanceTargetRef' \
  'approved_instance_operation_only'; do
  printf '%s\n' "$CREDENTIAL_SEED_BLOCK" | rg -n --fixed-strings "$term" >/dev/null || fail "credential projection extension missing term: $term"
done

for forbidden in '"credential_kind":"instance"' 'dedicated_credential_route":true' 'standalone_credential_management_plane' 'instance_function_definition' 'address_edit' 'schema_edit' 'raw_sql_edit' 'credential_edit'; do
  case "$forbidden" in
    'instance_function_definition'|'address_edit'|'schema_edit'|'raw_sql_edit'|'credential_edit')
      printf '%s\n' "$CREDENTIAL_SEED_BLOCK" | rg -n --fixed-strings '"forbiddenEditors":["instance_function_definition","address_edit","schema_edit","raw_sql_edit","credential_edit"]' >/dev/null || fail "missing Design Inspector forbidden editor boundary"
      ;;
    *)
      if printf '%s\n' "$CREDENTIAL_SEED_BLOCK" | rg -n --fixed-strings "$forbidden" >/dev/null; then
        fail "forbidden credential projection marker found: $forbidden"
      fi
      ;;
  esac
done

for leak in 'connection_string' 'endpoint_real_value' 'raw_sql' 'private_key' 'runtime_only_decrypted_payload' 'approval_bypass_authority'; do
  printf '%s\n' "$CREDENTIAL_SEED_BLOCK" | rg -n --fixed-strings "$leak" | rg -n --fixed-strings 'forbidden' >/dev/null || fail "JSON template leak guard missing forbidden marker: $leak"
done

require_term '"dispatchInstanceOperation"' frontend/islands/UiBuilderAdmin.tsx "Design Inspector instance operation action vocabulary"
require_term "instanceTargetRef" frontend/islands/UiBuilderAdmin.tsx "Design Inspector instance target ref field"
require_term '"allowedAssignments":["trigger","payloadFrom","outputProp"]' db/seed_empty.sql "Design Inspector allowed assignment seed boundary"
require_term 'list_instance_operation_authoring_candidates' frontend/lib/uiBuilderEventAuthoringHooks.ts "Design Inspector approved candidate loading"
require_term 'instanceOperationCandidates.map' frontend/components/NodeEventAuthoringPanel.tsx "Design Inspector select-only instance candidate options"
require_term 'RUNTIME_INTERACTION_INSTANCE_TARGET_REF_NOT_APPROVED' backend/repository/NpgsqlUiTopologyRepository.cs "backend unapproved instance targetRef rejection"
require_term 'jsonb_path_query' backend/repository/NpgsqlUiTopologyRepository.cs "DB manifest-record approved instance operation source"
require_term 'designInspectorEventCandidates' backend/repository/NpgsqlUiTopologyRepository.cs "Design Inspector approved event candidate source"
if rg -n 'jsonTemplateShape\.instance_operation_authority_binding|jsonTemplateShape"' backend/repository/NpgsqlUiTopologyRepository.cs; then
  fail "Design Inspector approved candidates must not come from public-safe jsonTemplateShape"
fi
require_term 'RUNTIME_INTERACTION_INSTANCE_CANDIDATE_SOURCE_UNAVAILABLE' backend/repository/NpgsqlUiTopologyRepository.cs "explicit candidate source failure"
require_term 'ValidateLayoutPatchAsync_DispatchInstanceOperation_CandidateSourceUnavailable_FailsClose' backend/tests/Topolactor.Runtime.Tests/NpgsqlUiTopologyRepositoryLayoutPatchValidationTests.cs "candidate source unavailable negative test"
require_term 'ProjectInstanceOperationAuthoringCandidate_MalformedTargetRef_FailsClose' backend/tests/Topolactor.Runtime.Tests/NpgsqlUiTopologyRepositoryLayoutPatchValidationTests.cs "malformed active candidate negative test"
require_term 'INSTANCE_OPERATION_CANDIDATE_TARGET_REF_MALFORMED' backend/repository/NpgsqlUiTopologyRepository.cs "malformed candidate fail-close error"
if rg -n 'ListSeedDefinedApprovedInstanceOperationCandidates|FindRepositoryFile\("db", "seed_empty.sql"\)|File\.ReadAllText\([^)]*seed_empty\.sql' backend/repository/NpgsqlUiTopologyRepository.cs; then
  fail "instance operation candidates must come from DB/manifest records, not runtime seed file scanning"
fi
require_term 'ValidateLayoutPatchAsync_DispatchInstanceOperation_ApprovedCandidate_Passes' backend/tests/Topolactor.Runtime.Tests/NpgsqlUiTopologyRepositoryLayoutPatchValidationTests.cs "approved instance operation positive test"
require_term 'ValidateLayoutPatchAsync_DispatchInstanceOperation_MissingInstanceTargetRef_FailsClose' backend/tests/Topolactor.Runtime.Tests/NpgsqlUiTopologyRepositoryLayoutPatchValidationTests.cs "missing instance targetRef negative test"
require_term 'ValidateLayoutPatchAsync_DispatchInstanceOperation_InvalidPrefix_FailsClose' backend/tests/Topolactor.Runtime.Tests/NpgsqlUiTopologyRepositoryLayoutPatchValidationTests.cs "invalid instance targetRef prefix negative test"
require_term 'ValidateLayoutPatchAsync_DispatchInstanceOperation_UnknownOperationBinding_FailsClose' backend/tests/Topolactor.Runtime.Tests/NpgsqlUiTopologyRepositoryLayoutPatchValidationTests.cs "unknown instance operation negative test"
require_term 'actionType is "dispatchExternalPort" or "dispatchInstanceOperation"' backend/repository/NpgsqlUiTopologyRepository.cs "backend runtime interaction validation for instance dispatch"
require_term '"instance-port:"' backend/repository/NpgsqlUiTopologyRepository.cs "backend instance targetRef prefix guard"

if rg -n 'placeholder="instance-port:<portKind>:<instancePortId>:<operationBindingKey>"|onInput=\{\(e\) => updateInteraction\(i, \{ instanceTargetRef' frontend/islands/UiBuilderAdmin.tsx; then
  fail "Design Inspector instanceTargetRef must not be free-text authored"
fi
require_term ".agent/tests/check-instance-port-substrate.sh" .github/workflows/unified-test-gate.yml "CI path trigger"
require_term ".agent/tests/check-instance-port-substrate.sh" .agent/tests/check-unified-test-gate.sh "unified gate design check"
require_term "docs/design/instance-port-substrate-ssot.yaml" .agent/docs/ssot-map.yaml "SSOT map entry"
require_term "docs/design/instance-port-substrate-ssot.yaml" .agent/docs/required-paths.yaml "required path entry"
require_term "instance_port_substrate_design_wiring" .agent/docs/test-bundles.yaml "test bundle entry"

PRIMITIVE_REGISTRY="docs/design/abstract-function-primitive-registry-ssot.yaml"
require_term "call_instance_postgres_function" "$PRIMITIVE_REGISTRY" "primitive registry instance postgres primitive vocabulary"
require_term "call_bound_instance_function" "$PRIMITIVE_REGISTRY" "primitive registry bound instance primitive vocabulary"
require_term "credential_backed_instance_function" "$PRIMITIVE_REGISTRY" "primitive registry credential-backed instance function vocabulary"
require_term "planned_future_vocabulary" "$PRIMITIVE_REGISTRY" "primitive registry planned/future vocabulary marker"

# Existing Topolactor DB primitive must stay topology-only and fixed to the Topolactor connection string.
require_term 'PrimitiveKey => "call_postgres_function"' backend/runtime/AbstractFunctionRuntime.cs "existing postgres primitive"
require_term 'AllowedFunctionName = new(@"^topology\.' backend/runtime/AbstractFunctionRuntime.cs "topology-only function allowlist"
require_term "_connectionString" backend/runtime/AbstractFunctionRuntime.cs "Topolactor DB connection string field"

# No provider/bundle-specific runtime selector for the new instance substrate may appear.
# Existing external_port_substrate code may validate ProviderKind/RequiredByBundle as records; this guard
# targets provider-specific selector branches; provider_kind / required_by_bundle must stay data labels.
if rg -n "if[[:space:]]*\([^)]*(provider_kind|ProviderKind)[[:space:]]*==|switch[[:space:]]*\([^)]*(provider_kind|ProviderKind)|if[[:space:]]*\([^)]*(required_by_bundle|RequiredByBundle)[[:space:]]*==|switch[[:space:]]*\([^)]*(required_by_bundle|RequiredByBundle)" backend/runtime backend/repository; then
  fail "provider_kind / required_by_bundle selector found in backend runtime/repository"
fi

if rg -n "class[[:space:]]+.*ProviderSpecific.*Handler|class[[:space:]]+.*Instance.*Provider.*Handler|case[[:space:]]+\"[A-Za-z0-9_-]+_runtime\"" backend frontend db; then
  fail "provider-specific runtime handler/selector marker found"
fi

# Guard against real plaintext credentials / connection literals in seed, SSOT, projection-ish docs, and logs.
if rg -n "(Host=|Server=|Username=|User Id=|Password=|postgres(ql)?://|jdbc:postgresql://|Endpoint=|AccountKey=|BEGIN (RSA|OPENSSH) PRIVATE KEY|sk_live_|AKIA[0-9A-Z]{16})" \
  docs/design/instance-port-substrate-ssot.yaml docs/design/external-port-substrate-ssot.yaml db/seed_empty.sql db/topology_tables.sql frontend backend/runtime backend/repository .agent/tasks; then
  fail "plaintext credential, endpoint, or connection string literal marker found"
fi

# No raw SQL/function authority in frontend payload surfaces for the new primitive.
if rg -n "call_instance_postgres_function|instance_function_authority_binding|instance_connection_policy" frontend; then
  fail "instance function authority must not be introduced in frontend payload/projection"
fi

# Runtime substrate must now exist as a sibling lane with primitive adapters and fail-close tests.
require_term 'class InstancePortDispatchRuntime' backend/runtime/InstancePortRuntime.cs "instance runtime lane"
require_term 'PrimitiveKey => "call_instance_postgres_function"' backend/runtime/InstancePortRuntime.cs "instance postgres primitive adapter"
require_term 'PrimitiveKey => "call_bound_instance_function"' backend/runtime/InstancePortRuntime.cs "bound instance primitive adapter"
require_term 'ResolveInstancePortRecordAsync' backend/repository/NpgsqlInstancePortPolicyRepository.cs "instance port repository read surface"
require_term 'ResolveInstanceCredentialReferenceAsync' backend/repository/NpgsqlInstancePortPolicyRepository.cs "runtime credential resolver"
require_term 'DecryptForRuntimeUse' backend/runtime/InstancePortRuntime.cs "guarded vault crypto materialization"
if rg -n 'Convert\.ToBase64String\(credentialVaultRecord\.EncryptedPayload\)|class[[:space:]]+FakeMaterializer' backend/runtime backend/tests/Topolactor.Runtime.Tests/InstancePortRuntimeTests.cs; then
  fail "instance credential materialization must use guarded vault crypto, not placeholder/base64 or fake materializer"
fi
require_term 'VerifyInstanceConnectionPolicyAsync' backend/runtime/InstancePortRuntime.cs "connection policy verifier"
require_term 'VerifyInstanceOperationAuthorityBindingAsync' backend/runtime/InstancePortRuntime.cs "operation authority verifier"
require_term 'SanitizeInstanceFunctionResultAsync' backend/runtime/InstancePortRuntime.cs "result sanitizer"
require_term 'instance_schema' backend/runtime/InstancePortRuntime.cs "instance schema authority check"
require_term 'instance_operation' backend/runtime/InstancePortRuntime.cs "instance operation authority check"
require_term 'INSTANCE_OUTPUT_AUTHORITY_MISSING' backend/runtime/InstancePortRuntime.cs "output authority fail-close"
require_term 'instance_port_execution_success' backend/runtime/InstancePortRuntime.cs "reference-only success runtime evidence"
require_term 'instance_port_execution_fail_close' backend/runtime/InstancePortRuntime.cs "reference-only fail-close runtime evidence"
require_term 'evidenceFunctionKey = binding.FunctionKey' backend/runtime/InstancePortRuntime.cs "fail-close evidence retains resolved function key"
if rg -n 'AppendEvidenceAsync\("instance_port_execution_fail_close", contextPort: null' backend/runtime/InstancePortRuntime.cs; then
  fail "primitive/executor fail-close evidence must not discard resolved instance reference context"
fi
require_term '["instance_port_runtime"]' backend/Program.cs "ManifestDispatcher instance runtime registration"
require_term 'instance_port_runtime' db/topology_tables.sql "DDL runtime lane allowlist"
require_term 'topology.instance_connection_policy' db/topology_tables.sql "connection policy DDL"
require_term 'topology.instance_operation_authority_binding' db/topology_tables.sql "operation authority DDL"
for test_name in \
  ExecuteAsync_MissingCredentialReference_FailsClose \
  ExecuteAsync_MissingAuthority_FailsClose \
  ExecuteAsync_MalformedInstanceTargetRef_FailsClose \
  ExecuteAsync_ProviderSelectorAttempt_FailsClose \
  AbstractFunctionRuntimeLaneMismatch_FailsClose \
  CallBoundInstanceFunction_SecretResultDenial_FailsClose \
  CallInstancePostgresFunction_AuthorityHelpers_FailClose \
  ExecuteAsync_MaterializationFailure_UsesRealVaultMaterializerAndFailsClose \
  ExecuteAsync_Success_AppendsReferenceOnlyRuntimeEvidence \
  ExecuteAsync_PrimitiveFailure_AppendsReferenceOnlyFailureEvidence \
  ExecuteAsync_AuthorityMismatch_AppendsReferenceOnlyFailureEvidence \
  CallBoundInstanceFunction_MissingAuthorityKinds_FailClose \
  CallPostgresFunction_TopologyOnlyBoundary_Regression; do
  require_term "$test_name" backend/tests/Topolactor.Runtime.Tests/InstancePortRuntimeTests.cs "instance fail-close test $test_name"
done

python3 - <<'PY'
from pathlib import Path
s = Path('docs/system-roadmap.yaml').read_text()
block = s.split('product.instance_port_substrate:', 1)[1].split('\n    product.', 1)[0]
if 'status: implemented' not in block or 'production_ready: false' not in block:
    raise SystemExit('product.instance_port_substrate must be implemented but production_ready false until live integration evidence closes')
if 'generic_instance_integration_acceptance_pending' not in block:
    raise SystemExit('product.instance_port_substrate must retain explicit residual production-readiness acceptance gap')
if '.agent/tasks/instance-port-substrate-implementation-todo.md' in block:
    raise SystemExit('product.instance_port_substrate must not point to deleted implementation todo file')
PY

echo "OK instance port substrate runtime guard"
