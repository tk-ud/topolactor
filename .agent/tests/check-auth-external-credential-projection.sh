#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
SEED="db/seed_empty.sql"

fail() { echo "FAIL: $*" >&2; exit 1; }
need() { rg -n --fixed-strings "$1" "$2" >/dev/null || fail "missing '$1' in $2"; }
absent() { if rg -n --fixed-strings "$1" "$2" >/dev/null; then fail "forbidden '$1' found in $2"; fi; }

need "auth.external.credential_management.projection" "$SEED"
need "auth-external-credential-management-topology-projection" "$SEED"
need "external_port_context" "$SEED"
need "policy_template_selection" "$SEED"
need "remoteManifestId\":\"00000000-0000-0000-0000-000000000091" "$SEED"
need "external_access_port_generic_http" "$SEED"
need "external_response_port_generic_http" "$SEED"
need "external_hook_port_scheduler_boundary" "$SEED"
need "validate_preview_apply_required\":true" "$SEED"
need "ui_builder_authority\":false" "$SEED"
need "physical_row_editor\":false" "$SEED"
need "mode\":\"seed_projection_marker_only" "$SEED"
need "canonical_execution\":\"not_wired_tableRef_dbTableName_to_wiring_physical_to_package" "$SEED"
need "policy_step_editing\":\"template_selection_only" "$SEED"

projection_block="$(sed -n '/auth\/external credential management topology projection/,/external_port_substrate generic policy seed/p' "$SEED")"
for bad in "plaintext_secret" "access_token" "refresh_token" "encrypted_payload"; do
  if printf '%s\n' "$projection_block" | rg -n --fixed-strings "$bad" | rg -v "secret_fields_forbidden" >/dev/null; then
    fail "credential projection leaks forbidden field outside forbidden marker: $bad"
  fi
done

absent "password_hash" "db/seed_empty.sql"
absent "credential_management_preset" "db/seed_empty.sql"
absent "CredentialManagementPanel" "frontend"
absent "credential-management" "frontend/routes"

echo "auth/external credential projection guard passed"
