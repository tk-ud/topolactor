#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SSOT="$REPO_ROOT/docs/design/enum-dictionary-ssot.yaml"
FAIL=0

fail(){ echo "FAIL: $1" >&2; FAIL=$((FAIL+1)); }
ok(){ echo "OK: $1"; }

[ -f "$SSOT" ] || { echo "FAIL: missing $SSOT" >&2; exit 1; }
ok "enum dictionary SSOT exists"

grep -q 'enum_dictionary_ssot:' "$SSOT" || fail "enum_dictionary_ssot root key missing"
grep -q 'enum.items' "$SSOT" || fail "enum.items canonical table missing in SSOT"
grep -q 'enumGroupId' "$SSOT" || fail "enumGroupId column contract missing in SSOT"
grep -q 'ENUM_GROUP_NOT_FOUND' "$SSOT" || fail "ENUM_GROUP_NOT_FOUND fail_close missing"

INIT="$REPO_ROOT/db/init.sql"
grep -q 'enum_tables.sql' "$INIT" || fail "db/init.sql must include enum_tables.sql"
grep -q 'enum_seed.sql' "$INIT" || fail "db/init.sql must include enum_seed.sql"

ENUM_SQL="$REPO_ROOT/db/enum_tables.sql"
[ -f "$ENUM_SQL" ] || fail "db/enum_tables.sql missing"
grep -q 'enum-dictionary-ssot.yaml' "$ENUM_SQL" || fail "enum_tables.sql must reference enum-dictionary-ssot.yaml"

grep -q 'EnumGroupId' "$REPO_ROOT/backend/schema/ManifestManagementContracts.cs" \
  || fail "AdminManifestScreenColumnDto must include EnumGroupId"
grep -q 'enumGroupId' "$REPO_ROOT/frontend/lib/manifestScreenDesign.ts" \
  || fail "ManifestScreenColumnDraft must include enumGroupId"
grep -q 'enum_dictionary:list_groups' "$REPO_ROOT/backend/runtime/AdminRuntime.cs" \
  || fail "AdminRuntime must handle enum_dictionary:list_groups"

if [ "$FAIL" -eq 0 ]; then
  echo "=== check-enum-dictionary passed ==="
else
  echo "=== check-enum-dictionary failed: $FAIL ===" >&2
  exit 1
fi
