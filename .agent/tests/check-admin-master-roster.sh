#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SSOT="$REPO_ROOT/docs/design/admin-master-roster-management-ssot.yaml"
FAIL=0

fail(){ echo "FAIL: $1" >&2; FAIL=$((FAIL+1)); }
ok(){ echo "OK: $1"; }

[ -f "$SSOT" ] || { echo "FAIL: missing $SSOT" >&2; exit 1; }
ok "admin master roster SSOT exists"

grep -q 'admin_master_roster_management_ssot:' "$SSOT" || fail "admin_master_roster_management_ssot root key missing"
grep -q '/admin/enums' "$SSOT" || fail "/admin/enums route missing in SSOT"
grep -q '/admin/users' "$SSOT" || fail "/admin/users route missing in SSOT"
grep -q 'user_status' "$SSOT" || fail "user_status seed contract missing"
grep -q 'admin_master_roster' "$SSOT" || fail "logs.diff admin_master_roster source_set_id missing"

SEED="$REPO_ROOT/db/enum_seed.sql"
grep -q 'user_status' "$SEED" || fail "db/enum_seed.sql must seed user_status group"
grep -q 'active' "$SEED" || fail "db/enum_seed.sql must seed active status item"

grep -q 'auth_users:list' "$REPO_ROOT/backend/runtime/AdminRuntime.cs" \
  || fail "AdminRuntime must handle auth_users:list"
grep -q 'enum_dictionary:create_group' "$REPO_ROOT/backend/runtime/AdminRuntime.cs" \
  || fail "AdminRuntime must handle enum_dictionary:create_group"

[ -f "$REPO_ROOT/frontend/routes/admin/enums.tsx" ] || fail "frontend/routes/admin/enums.tsx missing"
[ -f "$REPO_ROOT/frontend/routes/admin/users.tsx" ] || fail "frontend/routes/admin/users.tsx missing"
# admin-enum subBundle closure round 36: AdminEnumsRoster.tsx is retired and deleted --
# /admin/enums is served by frontend/islands/ProjectionShell.tsx (pinned to manifest ae200), the
# same generic production runtime every other projection surface uses. Checking for
# ProjectionShell.tsx's own existence, not a re-added AdminEnumsRoster file.
[ -f "$REPO_ROOT/frontend/islands/ProjectionShell.tsx" ] || fail "ProjectionShell island missing (serves /admin/enums since AdminEnumsRoster retirement)"
if grep -qE '^import .*AdminEnumsRoster' "$REPO_ROOT/frontend/routes/admin/enums.tsx"; then
  fail "frontend/routes/admin/enums.tsx must not import the retired AdminEnumsRoster island"
fi
[ -f "$REPO_ROOT/frontend/islands/AdminUsersRoster.tsx" ] || fail "AdminUsersRoster island missing"

RUNTIME="$REPO_ROOT/docs/design/runtime-orchestration-ssot.yaml"
grep -q '/admin/enums' "$RUNTIME" || fail "runtime-orchestration must register /admin/enums"
grep -q '/admin/users' "$RUNTIME" || fail "runtime-orchestration must register /admin/users"

if [ "$FAIL" -eq 0 ]; then
  echo "=== check-admin-master-roster passed ==="
else
  echo "=== check-admin-master-roster failed: $FAIL ===" >&2
  exit 1
fi
