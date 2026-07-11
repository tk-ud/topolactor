#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

if ! command -v dotnet &>/dev/null; then
  echo "ERROR: dotnet is required for backend tests. Install .NET SDK and retry." >&2
  echo "This check was NOT executed — missing tool is not a pass." >&2
  exit 1
fi

cd "${REPO_ROOT}"
source .agent/scripts/lib/noise_control.sh

noise_run "backend_runtime_tests" dotnet test backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj --nologo --verbosity minimal

# Integration continuity proof for UI topology registration boundary.
if [[ -n "${TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY:-}" ]]; then
  if [[ -z "${TOPOLACTOR_TEST_DB_CONNECTION:-}" ]]; then
    echo "ERROR: TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY is set but TOPOLACTOR_TEST_DB_CONNECTION is empty." >&2
    exit 1
  fi
  noise_run "backend_db_continuity_tests" dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj \
    --filter "UiTopologyRegistrationContinuityIntegrationTests|ComponentEventAppendIntegrationTests|LayoutProjectionContinuityLiveDbEndToEndTests|ExternalPortPolicyRepositoryLiveDbTests|AuditApprovalPortConsumerLiveDbTests|EmailPortConsumerLiveDbTests|StripePortConsumerLiveDbTests|AggregateTriggerRepositoryLiveDbTests|AggregateTriggerSubstrateRouteLiveDbTests|AdminRuntimeAggregateTriggerDefinitionPersistenceLiveDbTests|CredentialManagementHubRelationUiProjectionLiveDbTests" \
    --nologo --verbosity minimal
fi
