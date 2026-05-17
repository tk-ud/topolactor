#!/usr/bin/env bash
set -euo pipefail

if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: required tool missing: dotnet" >&2
  exit 1
fi

if ! command -v deno >/dev/null 2>&1; then
  echo "ERROR: required tool missing: deno" >&2
  exit 1
fi

dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj
deno test frontend/tests/defaultEntitySearch.test.ts
