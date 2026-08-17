// Explicit, positive read/filter operation-classification authority (round 37) -- mirrors
// docs/design/admin-master-roster-management-ssot.yaml admin_runtime_actions' "_read" groups
// verbatim as "<layer>:<action>" strings. Same set, same source, same purpose as
// .agent/scripts/react_schema_topology_seed_translator.py ADMIN_RUNTIME_READ_ACTIONS and
// backend/repository/NpgsqlUiTopologyRepository.cs AdminRuntimeReadActions -- see either one's own
// doc comment for why this is a POSITIVE allowlist (default-deny) rather than a mutation-verb
// denylist. All three must be updated together when the SSOT's admin_runtime_actions changes; drift
// is caught by frontend/tests/... adminRuntimeReadActions tests asserting set equality against a
// value derived straight from the two Python/C# mirrors.
export const ADMIN_RUNTIME_READ_ACTIONS: ReadonlySet<string> = new Set([
  "enum_dictionary:list_groups",
  "enum_dictionary:get_group",
  "auth_users:list",
  "auth_users:search",
  "auth_users:get",
  "scheduler_jobs:list_settings",
]);

// A layout node's own componentKind identifies it as Field-family authoring content
// (form_input/*) as opposed to Action/Step-family (action/*) -- the flattened layout_patch JSON
// boundary has no surviving "kind": "Field" discriminator (that is a react_schema/translator-level
// concept only), so componentKind prefix is the only signal available at render/dispatch time to
// tell a Field-owned dispatchTargetRefByTrigger override apart from an Action/Step-owned one.
export const FIELD_COMPONENT_KIND_PREFIX = "form_input/";

export function isFieldFamilyComponentKind(
  componentKind: string | null | undefined,
): boolean {
  return typeof componentKind === "string" &&
    componentKind.startsWith(FIELD_COMPONENT_KIND_PREFIX);
}
