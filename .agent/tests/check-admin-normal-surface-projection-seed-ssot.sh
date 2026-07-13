#!/usr/bin/env bash
set -euo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SSOT="$REPO_ROOT/docs/design/admin-normal-surface-projection-seed-ssot.yaml"
MAP="$REPO_ROOT/.agent/docs/ssot-map.yaml"
INDEX="$REPO_ROOT/.agent/docs/design-ssot-index.md"
CATALOG="$REPO_ROOT/frontend/components/catalog.ts"
MD_VIEWER="$REPO_ROOT/frontend/components/MdViewer.tsx"
MD_AUTHORING="$REPO_ROOT/frontend/components/MdTranslationAuthoringSeedSurface.tsx"
CRUD_PRESET="$REPO_ROOT/db/physical_search_crud_aggregate_preset_seed.sql"
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

require_block_term(){
  local file="$1" start="$2" end="$3" required="$4"
  if awk -v start="$start" -v end="$end" '
    index($0,start){flag=1}
    flag{print}
    flag && index($0,end){exit}
  ' "$file" | grep -qF -- "$required"; then
    pass "${file#$REPO_ROOT/} block $start..$end contains $required"
  else
    fail "${file#$REPO_ROOT/} block $start..$end missing $required"
  fi
}
require_block_absent(){
  local file="$1" start="$2" end="$3" forbidden="$4"
  if awk -v start="$start" -v end="$end" '
    index($0,start){flag=1}
    flag{print}
    flag && index($0,end){exit}
  ' "$file" | grep -qF -- "$forbidden"; then
    fail "${file#$REPO_ROOT/} block $start..$end must not contain $forbidden"
  else
    pass "${file#$REPO_ROOT/} block $start..$end excludes $forbidden"
  fi
}
require_file "$SSOT"
require_file "$CRUD_PRESET"
require_term "$MAP" "docs/design/admin-normal-surface-projection-seed-ssot.yaml"
require_term "$MAP" "docs/design/component-catalog-classification-ssot.yaml"
require_term "$MAP" "db/physical_search_crud_aggregate_preset_seed.sql"
require_term "$INDEX" "docs/design/admin-normal-surface-projection-seed-ssot.yaml"
require_term "$INDEX" 'credentials.users` は同一 `auth.users` と `auth.credentials` に対する composite account lifecycle projection'
require_term "$INDEX" 'users(status)` は同一 `auth.users` に対する status-only projection'
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
  "canonical_role_gate_binding:" \
  "topology_entry_type: capability_requirement" \
  "field: required_role" \
  "value: admin" \
  "seed_schema_mapping: operation_bindings[*].capability_requirement.required_role" \
  "capability_requirement.required_role=admin" \
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
  "docs/design/component-catalog-classification-ssot.yaml" \
  "docs/design/ui-builder-preset-ecosystem-ssot.yaml" \
  "docs/design/auth-db-session-credential-ssot.yaml" \
  "docs/design/enum-dictionary-ssot.yaml" \
  "docs/design/team-markdown-dashboard-saved-view-ssot.yaml" \
  "docs/design/ui-ux-primitive-catalog-ssot.yaml" \
  "docs/design/admin-master-roster-management-ssot.yaml" \
  "docs/design/admin-console-workflow-ssot.yaml" \
  "db/physical_search_crud_aggregate_preset_seed.sql"; do
  require_term "$SSOT" "$term"
done
require_term "$SSOT" "physical_seed_reference_only_not_design_authority"
require_term "$SSOT" "frontend/components/catalog.ts is implementation evidence only and must not be promoted to SSOT authority owner"
require_block_absent "$SSOT" "ui_primitive_catalog_authority:" "physical_evidence_refs:" "frontend/components/catalog.ts"
require_block_term "$SSOT" "operation_bindings:" "component_bindings:" "required_shape: list of operation_key, capability_kind, authority_ref, backend_emission_ref, confirmation_required, forbidden_projection_fields"
require_block_absent "$SSOT" "operation_bindings:" "component_bindings:" "required_shape: list of operation_key, capability_kind, authority_ref, backend_emission_ref, confirmation_required, forbidden_projection_fields, capability_requirement"
require_block_term "$SSOT" "operation_bindings:" "component_bindings:" "optional_fields:"
require_block_term "$SSOT" "operation_bindings:" "component_bindings:" "capability_requirement:"
require_block_term "$SSOT" "operation_bindings:" "component_bindings:" "schema_mapping: operation_bindings[*].capability_requirement.required_role"
require_block_term "$SSOT" "operation_bindings:" "component_bindings:" "canonical_auth_semantics_ref: docs/design/auth-db-session-credential-ssot.yaml manifest_capability_requirement"
require_block_term "$SSOT" "operation_bindings:" "component_bindings:" "If explicit required_role exists, JWT role must match it"
require_block_term "$SSOT" "operation_bindings:" "component_bindings:" "If explicit capability_requirement is omitted and runtime destination does not infer a role requirement"
require_block_term "$SSOT" "operation_bindings:" "component_bindings:" "conditional_required_for: mutation_operation_bindings"
require_block_term "$SSOT" "operation_bindings:" "component_bindings:" "topology_entry_type: capability_requirement"
require_block_term "$SSOT" "operation_bindings:" "component_bindings:" "required_role: admin"
require_block_term "$SSOT" "inputer_mutation_contract:" "viewer_read_search_filter_contract:" "topology_entry_type: capability_requirement"
require_block_term "$SSOT" "inputer_mutation_contract:" "viewer_read_search_filter_contract:" "required_role: admin"
require_block_term "$SSOT" "operation_bindings:" "component_bindings:" "viewer_read_search_filter_contract:"
require_block_term "$SSOT" "viewer_read_search_filter_contract:" "prohibited_substitutes:" "capability_requirement: omitted"
require_block_term "$SSOT" "viewer_read_search_filter_contract:" "prohibited_substitutes:" "required_role_field: omitted"
require_block_term "$SSOT" "viewer_read_search_filter_contract:" "prohibited_substitutes:" "explicit_admin_role_gate: absent"
require_block_term "$SSOT" "viewer_read_search_filter_contract:" "prohibited_substitutes:" "inferred_admin_requirement: absent"
require_block_absent "$SSOT" "viewer_read_search_filter_contract:" "prohibited_substitutes:" "required_role: none"
require_block_absent "$SSOT" "viewer_read_search_filter_contract:" "prohibited_substitutes:" "required_role: admin"

for term in \
  "source_snapshot_json" \
  "visual_tree_json" \
  "mock_preset_object_mapping" \
  "mock_preset_wiring_candidate" \
  "mock_preset_compile_snapshot" \
  "requires_event_binding" \
  "wiring_kind" \
  "target_surface" \
  "target_ref" \
  "payloadFrom" \
  "payloadResolverRef" \
  "propBindings" \
  "unresolved_knownGapRef" \
  "tmp_canvas_draft_human_adjustment_preview_validate_apply_boundary" \
  "content_bundle_operation_refs_must_not_be_copied" \
  "target_authority: auth_users:* account lifecycle authority" \
  "target_authority: auth_users:* status operation boundary" \
  "target_authority: enum_dictionary:* authority" \
  "target_authority: team Markdown authority"; do
  require_term "$SSOT" "$term"
done
for term in \
  "source_snapshot_json" \
  "visual_tree_json" \
  "mock_preset_object_mapping" \
  "mock_preset_wiring_candidate" \
  "mock_preset_compile_snapshot" \
  "requires_event_binding" \
  "wiring_kind" \
  "target_surface" \
  "target_ref" \
  "payloadFrom" \
  "payloadResolverRef" \
  "propBindings" \
  "knownGapRef" \
  "activeTopologyWrite" \
  "requiresHumanAdjustmentBeforeApply"; do
  require_term "$CRUD_PRESET" "$term"
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
require_absent "$SSOT" "required_capability: admin"
require_absent "$SSOT" "required_role: none"
require_absent "$SSOT" "viewer_read_projection_required_role: none"
if [ "$FAILURES" -eq 0 ]; then
  echo "PASS admin-normal surface projection seed SSOT proof"
  exit 0
fi
echo "FAIL admin-normal surface projection seed SSOT proof failures=$FAILURES" >&2
exit 1
