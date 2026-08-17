import {
  assert,
  assertEquals,
} from "https://deno.land/std@0.208.0/assert/mod.ts";

/**
 * Admin POST /api/dispatch → backend /dispatch resolves manifests from DB seed.
 * Missing dispatcher_mapping rows produce MANIFEST_NOT_FOUND (422) at runtime.
 *
 * This test keeps seed_empty.sql aligned with AdminRuntime.ExecuteDataAsync
 * admin-target operations so CI catches seed drift before live dispatch fails.
 *
 * Authority: docs/design/runtime-orchestration-ssot.yaml (manifest_resolution.api.key)
 *
 * admin-surface-topology-seed-conversion (Phase 2 proof-drift fix): the previous version of this
 * test hand-maintained REQUIRED_ADMIN_DISPATCH_AXES and never included auth_users, team_markdown,
 * or scheduler_jobs at all — a confirmed proof drift that let this test claim "every AdminRuntime
 * admin axis" while structurally excluding three whole families. This version:
 *   1. Keeps the previously-tracked baseline array unchanged (BASELINE_TRACKED_AXES) so existing
 *      regression coverage is not weakened.
 *   2. Adds scheduler_jobs's 4 actions explicitly (already fully seed-mapped; closing this
 *      Bundle's tracked gap for that family).
 *   3. DERIVES the auth_users and team_markdown axis sets from AdminRuntime.cs /
 *      AdminRuntime.TeamMarkdown.cs's own switch cases (the historically-confirmed-drifted
 *      families) rather than hand-copying them, so a future case added to either switch is
 *      automatically required here without this file being hand-edited again.
 * Deriving ALL ~100 AdminRuntime axes from source and requiring every one to have a seed mapping
 * was tried and rejected for this test: it surfaces ~10 pre-existing, out-of-Bundle-scope gaps in
 * the manifest clone-authoring / admin_csv_json_import families (recorded as a new finding in the
 * design-resolution report's remaining_gap, not fixed by this Bundle) that would otherwise fail
 * CI for surfaces this Bundle did not audit or touch.
 */

/** layer:action keys implemented in AdminRuntime for target=admin (pre-existing, unchanged). */
const BASELINE_TRACKED_AXES: string[] = [
  "context_token_registry:list",
  "context_token_registry:create",
  "context_token_registry:deprecate",
  "registry_vector:validate",
  "ui_component_bucket:create",
  "ui_component_bucket:list",
  "component_registration:register_or_update_projection_component",
  "package_generator:generate",
  "package_generator:promote",
  "package_generator:promote_package",
  "package_generator:detach_package_components",
  "mock_preset:create",
  "mock_preset:list",
  "mock_preset:get",
  "mock_preset:compile",
  "mock_preset:bind",
  "mock_preset:save_mappings",
  "ui_topology:promoted_palette",
  "ui_topology:layout_candidates",
  "layout_patch:preview",
  "layout_patch:validate",
  "layout_patch:apply",
  "layout_patch:save_tmp",
  "ui_topology:list_packages",
  "ui_topology:list_package_components",
  "ui_topology:get_package_wiring",
  "ui_topology:get_layout_patch_draft",
  "ui_topology:update_package_wiring",
  "component_style_design:list",
  "component_style_design:save_tmp",
  "component_style_design:upsert",
  "seed_runtime:save",
  "seed_runtime:load",
  "seed_runtime:validate",
  "seed_runtime:preview",
  "seed_runtime:import",
  "system_ci:list_targets",
  "system_ci:inspect",
  "ci_attention:refresh_fragments",
  "sql_attention:list_projection",
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

/** admin-surface-topology-seed-conversion issue-10: scheduler-settings subBundle, already fully seed-mapped. */
const SCHEDULER_JOBS_AXES: string[] = [
  "scheduler_jobs:list_settings",
  "scheduler_jobs:create",
  "scheduler_jobs:edit",
  "scheduler_jobs:disable",
  // scheduler-settings subBundle: the symmetric enable counterpart required by
  // admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.scheduler
  // .new_operation_note, seeded as manifest ...0000000000f4 (axes route) and additionally on the
  // scheduler.settings.projection manifest's own target="manifest" mappings.
  "scheduler_jobs:enable",
];

/** `"layer:action" =>` (or `"sub:action" =>` inside the nested team_markdown switch). */
const SWITCH_CASE_RE = /"([a-zA-Z0-9_]+:[a-zA-Z0-9_:]+)"\s*=>/g;

function extractSwitchCaseKeys(source: string): string[] {
  const keys: string[] = [];
  let m: RegExpExecArray | null;
  while ((m = SWITCH_CASE_RE.exec(source)) !== null) {
    keys.push(m[1]);
  }
  return keys;
}

/**
 * Derives auth_users:* and team_markdown:* axes straight from AdminRuntime.cs /
 * AdminRuntime.TeamMarkdown.cs — the two families historically confirmed drifted (issue-08 /
 * issue-09 / issue-11) — instead of hand-copying them into a literal array a future case addition
 * could silently outrun.
 */
async function deriveHistoricallyDriftedAxes(): Promise<string[]> {
  const adminRuntimeSrc = await Deno.readTextFile(
    new URL("../../backend/runtime/AdminRuntime.cs", import.meta.url),
  );
  const teamMarkdownSrc = await Deno.readTextFile(
    new URL(
      "../../backend/runtime/AdminRuntime.TeamMarkdown.cs",
      import.meta.url,
    ),
  );

  const directKeys = extractSwitchCaseKeys(adminRuntimeSrc);
  const authUsersKeys = directKeys.filter((k) => k.startsWith("auth_users:"));

  // AdminRuntime.TeamMarkdown.cs's nested `action switch` only has the sub-action half
  // ("template:create", "saved_view:archive", ...); AdminRuntime.cs's wildcard case strips the
  // "team_markdown:" prefix before re-dispatching here, so the full production axis is that
  // prefix plus this sub-action key.
  const teamMarkdownSubKeys = extractSwitchCaseKeys(teamMarkdownSrc);
  const teamMarkdownKeys = teamMarkdownSubKeys.map((k) => `team_markdown:${k}`);

  assert(
    authUsersKeys.length > 0,
    "expected to derive at least one auth_users:* axis from AdminRuntime.cs",
  );
  assert(
    teamMarkdownKeys.length > 0,
    "expected to derive at least one team_markdown:* axis from AdminRuntime.TeamMarkdown.cs",
  );

  return [...authUsersKeys, ...teamMarkdownKeys];
}

async function requiredAdminDispatchAxes(): Promise<string[]> {
  const derived = await deriveHistoricallyDriftedAxes();
  return [
    ...new Set([...BASELINE_TRACKED_AXES, ...SCHEDULER_JOBS_AXES, ...derived]),
  ].sort();
}

/**
 * `"layer:action"` -> `[layer, action]`, splitting on the FIRST colon only. team_markdown axes
 * carry a colon inside the action half itself (e.g. "team_markdown:template:create"), so a naive
 * `axis.split(":")` destructure would silently drop everything after the second colon.
 */
function splitAxis(axis: string): [string, string] {
  const idx = axis.indexOf(":");
  return [axis.slice(0, idx), axis.slice(idx + 1)];
}

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

function manifestBlocksRouteToAdminRuntime(
  seedSql: string,
  layer: string,
  action: string,
): boolean {
  const needle =
    `"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"${layer}","action":"${action}"`;
  const start = seedSql.indexOf(needle);
  if (start < 0) return false;
  const window = seedSql.slice(start, start + 600);
  return window.includes('"runtime_destination":"admin_runtime"');
}

Deno.test(
  "adminDispatchManifestSeed: tracked axis set covers auth_users, team_markdown, scheduler_jobs, enum_dictionary",
  async () => {
    const axes = await requiredAdminDispatchAxes();
    for (
      const family of [
        "auth_users",
        "team_markdown",
        "scheduler_jobs",
        "enum_dictionary",
      ]
    ) {
      assert(
        axes.some((a) => splitAxis(a)[0] === family),
        `tracked admin dispatch axis set unexpectedly has no "${family}:*" entry`,
      );
    }
  },
);

Deno.test(
  "adminDispatchManifestSeed: seed_empty.sql defines dispatcher_mapping for every tracked admin axis",
  async () => {
    const seedSql = await Deno.readTextFile(
      new URL("../../db/seed_empty.sql", import.meta.url),
    );
    const requiredAxes = await requiredAdminDispatchAxes();
    const seeded = extractAdminDispatcherMappings(seedSql);
    const missing = requiredAxes.filter((axis) => !seeded.has(axis));
    assert(
      missing.length === 0,
      `seed_empty.sql missing admin dispatcher_mapping for: ${
        missing.join(", ")
      }. ` +
        "POST /api/dispatch will return MANIFEST_NOT_FOUND until these rows exist.",
    );
  },
);

Deno.test(
  "adminDispatchManifestSeed: each tracked admin axis maps to admin_runtime in seed topology block",
  async () => {
    const seedSql = await Deno.readTextFile(
      new URL("../../db/seed_empty.sql", import.meta.url),
    );
    const requiredAxes = await requiredAdminDispatchAxes();
    const missingRuntime: string[] = [];
    for (const axis of requiredAxes) {
      const [layer, action] = splitAxis(axis);
      if (!manifestBlocksRouteToAdminRuntime(seedSql, layer, action)) {
        missingRuntime.push(axis);
      }
    }
    assert(
      missingRuntime.length === 0,
      `seed_empty.sql missing runtime_mapping admin_runtime near: ${
        missingRuntime.join(", ")
      }`,
    );
  },
);

Deno.test(
  "adminDispatchManifestSeed: derived historically-drifted axis sets are non-empty and de-duplicated",
  async () => {
    const derived = await deriveHistoricallyDriftedAxes();
    assert(
      derived.length >= 19,
      `expected >=19 derived auth_users+team_markdown axes, got ${derived.length}`,
    );
    assertEquals(
      new Set(derived).size,
      derived.length,
      "derived axis set must not contain duplicates",
    );
  },
);
