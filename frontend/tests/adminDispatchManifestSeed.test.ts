import { assert } from "https://deno.land/std@0.208.0/assert/mod.ts";

/**
 * Admin POST /api/dispatch → backend /dispatch resolves manifests from DB seed.
 * Missing dispatcher_mapping rows produce MANIFEST_NOT_FOUND (422) at runtime.
 *
 * This test keeps seed_empty.sql aligned with AdminRuntime.ExecuteDataAsync
 * admin-target operations so CI catches seed drift before live dispatch fails.
 *
 * Authority: docs/design/runtime-orchestration-ssot.yaml (manifest_resolution.api.key)
 */

/** layer:action keys implemented in AdminRuntime for target=admin. */
const REQUIRED_ADMIN_DISPATCH_AXES: string[] = [
  "context_token_registry:list",
  "context_token_registry:create",
  "context_token_registry:deprecate",
  "registry_vector:validate",
  "ui_component_bucket:create",
  "ui_component_bucket:list",
  "package_generator:generate",
  "package_generator:promote",
  "package_generator:promote_package",
  "ui_topology:promoted_palette",
  "ui_topology:layout_candidates",
  "layout_patch:preview",
  "layout_patch:validate",
  "layout_patch:apply",
  "ui_topology:list_packages",
  "ui_topology:list_package_components",
  "ui_topology:get_package_wiring",
  "ui_topology:get_layout_patch_draft",
  "ui_topology:update_package_wiring",
  "component_style_design:list",
  "component_style_design:upsert",
  "seed_runtime:save",
  "seed_runtime:load",
  "seed_runtime:validate",
  "seed_runtime:preview",
  "seed_runtime:import",
  "system_ci:list_targets",
  "system_ci:inspect",
  "ci_attention:refresh_fragments",
  "admin_csv_json_import:upload_preview",
  "admin_csv_json_import:apply",
  "admin_csv_json_import:list_manifests",
  "admin_csv_json_import:list_schemas",
  "manifest:list",
  "manifest:get",
  "manifest:validate",
  "manifest:create_draft",
  "manifest:update_draft",
  "manifest:promote",
  "manifest:deprecate",
  "manifest:assign_hub_grouping",
  "manifest:assign_screen_data_shape",
  "manifest:list_relationship_remote_targets",
  "promotion_manifest:list",
  "promotion_manifest:get",
  "promotion_manifest:validate",
  "promotion_manifest:update_draft",
  "content_bundle:list_hubs",
  "content_bundle:list_entities",
  "content_bundle:list_relations",
  "content_bundle:list_states",
  "content_bundle:get_entity",
  "content_bundle:search",
  "content_bundle:create_entity_draft",
  "content_bundle:validate_draft",
  "content_bundle:preview_draft",
  "content_bundle:promote_draft",
  "content_bundle:get_hub",
  "content_bundle:get_relation",
  "content_bundle:update_entity_draft",
  "content_bundle:list_hub_relations",
  "hub_navigation:list_manifests",
  "hub_navigation:get_hub_relations",
  "hub_navigation:create",
  "hub_navigation:update",
  "hub_navigation:deprecate",
  "hub_navigation:reorder",
  "enum_dictionary:list_groups",
  "enum_dictionary:get_group",
  "enum_dictionary:create_group",
  "enum_dictionary:update_group",
  "enum_dictionary:delete_group",
  "enum_dictionary:create_item",
  "enum_dictionary:update_item",
  "enum_dictionary:delete_item",
  "enum_dictionary:set_group_items",
];

function extractAdminDispatcherMappings(seedSql: string): Set<string> {
  const found = new Set<string>();
  const re =
    /"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"([^"]+)","action":"([^"]+)"/g;
  let m: RegExpExecArray | null;
  while ((m = re.exec(seedSql)) !== null) {
    found.add(`${m[1]}:${m[2]}`);
  }
  return found;
}

function manifestBlocksRouteToAdminRuntime(seedSql: string, layer: string, action: string): boolean {
  const needle =
    `"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"${layer}","action":"${action}"`;
  const start = seedSql.indexOf(needle);
  if (start < 0) return false;
  const window = seedSql.slice(start, start + 600);
  return window.includes('"runtime_destination":"admin_runtime"');
}

Deno.test(
  "adminDispatchManifestSeed: seed_empty.sql defines dispatcher_mapping for every AdminRuntime admin axis",
  async () => {
    const seedSql = await Deno.readTextFile(
      new URL("../../db/seed_empty.sql", import.meta.url),
    );
    const seeded = extractAdminDispatcherMappings(seedSql);
    const missing = REQUIRED_ADMIN_DISPATCH_AXES.filter((axis) => !seeded.has(axis));
    assert(
      missing.length === 0,
      `seed_empty.sql missing admin dispatcher_mapping for: ${missing.join(", ")}. ` +
        "POST /api/dispatch will return MANIFEST_NOT_FOUND until these rows exist.",
    );
  },
);

Deno.test(
  "adminDispatchManifestSeed: each admin axis maps to admin_runtime in seed topology block",
  async () => {
    const seedSql = await Deno.readTextFile(
      new URL("../../db/seed_empty.sql", import.meta.url),
    );
    const missingRuntime: string[] = [];
    for (const axis of REQUIRED_ADMIN_DISPATCH_AXES) {
      const [layer, action] = axis.split(":");
      if (!manifestBlocksRouteToAdminRuntime(seedSql, layer, action)) {
        missingRuntime.push(axis);
      }
    }
    assert(
      missingRuntime.length === 0,
      `seed_empty.sql missing runtime_mapping admin_runtime near: ${missingRuntime.join(", ")}`,
    );
  },
);
