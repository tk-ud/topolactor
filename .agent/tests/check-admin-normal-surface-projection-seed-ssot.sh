#!/usr/bin/env bash
set -euo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SSOT="$REPO_ROOT/docs/design/admin-normal-surface-projection-seed-ssot.yaml"
MAP="$REPO_ROOT/.agent/docs/ssot-map.yaml"
INDEX="$REPO_ROOT/.agent/docs/design-ssot-index.md"
CATALOG="$REPO_ROOT/frontend/components/catalog.ts"
FAILURES=0
pass(){ echo "OK  : $1"; }
fail(){ echo "FAIL: $1" >&2; FAILURES=$((FAILURES+1)); }
require_file(){ [ -f "$1" ] && pass "file exists: ${1#$REPO_ROOT/}" || fail "missing file: ${1#$REPO_ROOT/}"; }
require_term(){ local file="$1" term="$2"; if rg -q --fixed-strings -- "$term" "$file"; then pass "${file#$REPO_ROOT/} contains $term"; else fail "${file#$REPO_ROOT/} missing $term"; fi; }
require_absent(){ local file="$1" term="$2"; if rg -q --fixed-strings -- "$term" "$file"; then fail "${file#$REPO_ROOT/} must not contain $term"; else pass "${file#$REPO_ROOT/} excludes $term"; fi; }
require_file "$SSOT"
require_term "$MAP" "docs/design/admin-normal-surface-projection-seed-ssot.yaml"
require_term "$INDEX" "docs/design/admin-normal-surface-projection-seed-ssot.yaml"
for term in \
  "hub_surface_axis_admin_normal" \
  "admin:" \
  "normal:" \
  "credentials:" \
  "users:" \
  "enum:" \
  "dashboard:" \
  "projection_axis:" \
  "component_bindings:" \
  "prop_bindings:" \
  "event_bindings:" \
  "backend_emission_bindings:" \
  "search_filter_input_contract:" \
  "mutation_confirmation_contract:" \
  "hub_relation_navigation_binding:" \
  "fail_close_manifest_resolution" \
  "secret_projection_denied" \
  "users_credentials_separation" \
  "preview, validate, explicit_confirm, write, diff_log" \
  "design_blocking:" \
  "seed_implementation_start_conditions:"; do
  require_term "$SSOT" "$term"
done
for term in \
  "docs/design/db-schema.yaml" \
  "db/auth_tables.sql" \
  "db/enum_tables.sql" \
  "db/team_markdown_tables.sql" \
  "db/topology_tables.sql" \
  "frontend/components/catalog.ts" \
  "docs/design/auth-db-session-credential-ssot.yaml" \
  "docs/design/enum-dictionary-ssot.yaml" \
  "docs/design/team-markdown-dashboard-saved-view-ssot.yaml" \
  "docs/design/ui-ux-primitive-catalog-ssot.yaml"; do
  require_term "$SSOT" "$term"
done
for key in \
  "md_viewer.projection" \
  "md_translation_authoring_surface.authoring" \
  "textarea.template" \
  "search_input.alias" \
  "form_field.template" \
  "select.template" \
  "button.primitive" \
  "table.primitive" \
  "panel.alias"; do
  require_term "$SSOT" "$key"
  require_term "$CATALOG" "componentKey: \"$key\""
done
require_term "$REPO_ROOT/db/auth_tables.sql" "CREATE TABLE IF NOT EXISTS auth.credentials"
require_term "$REPO_ROOT/db/topology_tables.sql" "CREATE TABLE IF NOT EXISTS topology.external_credential_vault"
require_term "$REPO_ROOT/db/topology_tables.sql" "CREATE TABLE IF NOT EXISTS hubs.hub_relations"
require_term "$REPO_ROOT/db/team_markdown_tables.sql" "CREATE TABLE IF NOT EXISTS topology.team_markdown_saved_view"
require_term "$REPO_ROOT/db/enum_tables.sql" "CREATE TABLE IF NOT EXISTS enum.items"
require_absent "$SSOT" "password_hash:"
require_absent "$SSOT" "token_hash: example"
require_absent "$SSOT" "encrypted_payload: example"
if [ "$FAILURES" -eq 0 ]; then
  echo "PASS admin-normal surface projection seed SSOT proof"
  exit 0
fi
echo "FAIL admin-normal surface projection seed SSOT proof failures=$FAILURES" >&2
exit 1
