#!/usr/bin/env bash
set -euo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SSOT="$REPO_ROOT/docs/design/admin-normal-surface-projection-seed-ssot.yaml"
MAP="$REPO_ROOT/.agent/docs/ssot-map.yaml"
INDEX="$REPO_ROOT/.agent/docs/design-ssot-index.md"
CATALOG="$REPO_ROOT/frontend/components/catalog.ts"
MD_VIEWER="$REPO_ROOT/frontend/components/MdViewer.tsx"
MD_AUTHORING="$REPO_ROOT/frontend/components/MdTranslationAuthoringSeedSurface.tsx"
FAILURES=0
pass(){ echo "OK  : $1"; }
fail(){ echo "FAIL: $1" >&2; FAILURES=$((FAILURES+1)); }
require_file(){ [ -f "$1" ] && pass "file exists: ${1#$REPO_ROOT/}" || fail "missing file: ${1#$REPO_ROOT/}"; }
contains_fixed(){
  local file="$1" term="$2"
  if command -v rg >/dev/null 2>&1; then
    rg -q --fixed-strings -- "$term" "$file"
  else
    grep -qF -- "$term" "$file"
  fi
}
require_term(){ local file="$1" term="$2"; if contains_fixed "$file" "$term"; then pass "${file#$REPO_ROOT/} contains $term"; else fail "${file#$REPO_ROOT/} missing $term"; fi; }
require_absent(){ local file="$1" term="$2"; if contains_fixed "$file" "$term"; then fail "${file#$REPO_ROOT/} must not contain $term"; else pass "${file#$REPO_ROOT/} excludes $term"; fi; }
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
  "account_lifecycle_and_status_projection_split" \
  "preview, validate, explicit_confirm, write, diff_log" \
  "design_blocking:" \
  "seed_implementation_start_conditions:" \
  "composite_account_lifecycle_for_same_auth_user" \
  "create_account_with_initial_credential" \
  "delete_account_with_credentials" \
  "continuation_refinement_not_duplicate_authority" \
  "runtime_adapter_required_before_seed_generation" \
  "runtimeConnected=false" \
  "admin_capability_gate_applies_to: [preview, validate, explicit_confirm, write, diff_log]" \
  "viewer_mutation_callbacks_disabled_in_normal_projection" \
  "inputer_runtime_adapter_contract:" \
  "actual_component_props: [onSaved, onCancel, placement]" \
  "adapter_owned_events: [preview, validate, explicit_confirm, write, diff_log]" \
  "zero_active_target_manifest_for_related_hub_id" \
  "multiple_active_target_manifests_for_related_hub_id" \
  "All design_blocking entries are resolved before seed generation"; do
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
  "docs/design/ui-ux-primitive-catalog-ssot.yaml" \
  "docs/design/admin-master-roster-management-ssot.yaml" \
  "docs/design/admin-console-workflow-ssot.yaml"; do
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
require_term "$REPO_ROOT/db/auth_tables.sql" "CREATE TABLE IF NOT EXISTS auth.users"
require_term "$REPO_ROOT/db/auth_tables.sql" "CREATE TABLE IF NOT EXISTS auth.credentials"
require_term "$REPO_ROOT/db/auth_tables.sql" "password_hash"
require_term "$REPO_ROOT/db/topology_tables.sql" "CREATE TABLE IF NOT EXISTS topology.external_credential_vault"
require_term "$REPO_ROOT/db/topology_tables.sql" "CREATE TABLE IF NOT EXISTS hubs.hub_relations"
require_term "$REPO_ROOT/db/topology_tables.sql" "related_hub_id"
require_term "$REPO_ROOT/db/team_markdown_tables.sql" "CREATE TABLE IF NOT EXISTS topology.team_markdown_saved_view"
require_term "$REPO_ROOT/db/team_markdown_tables.sql" "CREATE TABLE IF NOT EXISTS topology.team_markdown_saved_view_event"
require_term "$REPO_ROOT/db/enum_tables.sql" "CREATE TABLE IF NOT EXISTS enum.items"
require_term "$REPO_ROOT/db/enum_tables.sql" "name       TEXT"
require_term "$REPO_ROOT/db/enum_tables.sql" "index_num  INTEGER"
require_term "$REPO_ROOT/db/enum_tables.sql" "group_name  TEXT"
require_term "$REPO_ROOT/db/enum_tables.sql" "position        INTEGER"
require_term "$SSOT" "enum.items.name"
require_term "$SSOT" "enum.items.index_num"
require_term "$SSOT" "enum.groups.group_name"
require_term "$SSOT" "enum.groups.index_num"
require_term "$SSOT" "enum.group_items.position"
require_term "$REPO_ROOT/docs/design/admin-master-roster-management-ssot.yaml" "auth_users:create"
require_term "$REPO_ROOT/docs/design/admin-master-roster-management-ssot.yaml" "auth_users:delete"
require_term "$REPO_ROOT/docs/design/auth-db-session-credential-ssot.yaml" "Login credentials live only in auth.credentials"
require_term "$REPO_ROOT/docs/design/admin-console-workflow-ssot.yaml" "resolves to a target manifest only when exactly one active hubs.topology_manifests row"
require_term "$REPO_ROOT/docs/design/admin-console-workflow-ssot.yaml" "first-match/oldest/MIN implicit fallback"
require_term "$MD_VIEWER" "export type MdViewerProps"
require_term "$MD_VIEWER" "savedView: SavedViewDetail"
require_term "$MD_VIEWER" "onRefresh?: (savedViewId: string) => void"
require_term "$MD_AUTHORING" "export type MdTranslationAuthoringSeedSurfaceProps"
require_term "$MD_AUTHORING" "onSaved?: (savedViewId: string) => void"
require_term "$MD_AUTHORING" 'placement?: "admin_route" | "ui_builder_child_surface"'
require_term "$CATALOG" 'componentKey: "md_translation_authoring_surface.authoring"'
require_term "$CATALOG" "runtimeConnected: false"
require_absent "$SSOT" "password_hash:"
require_absent "$SSOT" "token_hash: example"
require_absent "$SSOT" "encrypted_payload: example"
require_absent "$SSOT" "items_name"
require_absent "$SSOT" "groups_group_name"
require_absent "$SSOT" "All design_blocking entries are either resolved"
require_absent "$SSOT" "credentials surface owns credential lifecycle intent"
require_absent "$SSOT" "target_manifest_resolution: resolve exactly one active target manifest for related_hub_id and selected surface context"
if [ "$FAILURES" -eq 0 ]; then
  echo "PASS admin-normal surface projection seed SSOT proof"
  exit 0
fi
echo "FAIL admin-normal surface projection seed SSOT proof failures=$FAILURES" >&2
exit 1
