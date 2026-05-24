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

dotnet test backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj --nologo --verbosity minimal

# Integration continuity proof for UI topology registration boundary.
if [[ -n "${TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY:-}" ]]; then
  dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj \
    --filter UiTopologyRegistrationContinuityIntegrationTests \
    --nologo --verbosity minimal
fi
