/**
 * propBindingContractSync.test.ts
 *
 * Verifies frontend/backend capability table and transform allowlist stay in sync
 * with the SSOT (admin-console-workflow-ssot.yaml layout_node_props_contract).
 *
 * This test documents the canonical expected values. If frontend constants diverge
 * from the SSOT or from the backend C# constants, this test will fail.
 *
 * The backend C# equivalent is in:
 *   backend/runtime/StructureMapResolver.cs ComponentArrayPropCapabilities / AllowedPropBindingTransforms
 * and is exercised in:
 *   backend/tests/Topolactor.Runtime.Tests/PropBindingContractSyncTests.cs
 */
import {
  assertEquals,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  ALLOWED_PROP_BINDING_TRANSFORMS,
  COMPONENT_ARRAY_PROP_CAPABILITIES,
} from "../runtime/propBindingResolver.ts";

// ── SSOT canonical capability table ─────────────────────────────────────────
// Canonical source: admin-console-workflow-ssot.yaml layout_node_props_contract.component_array_prop_capabilities
// Must match backend/runtime/StructureMapResolver.cs ComponentArrayPropCapabilities exactly.

const SSOT_COMPONENT_ARRAY_PROP_CAPABILITIES: Record<string, string[]> = {
  "data_display/table":                ["rows", "columns"],
  "data_display/data_grid":            ["rows", "columns"],
  "data_display/list":                 ["rows", "items"],
  "data_display/tree":                 ["nodes", "items"],
  "data_display/json":                 ["data"],
  "display/card_list":                 ["items"],
  "disclosure/accordion":              ["items"],
  "form_input/select":                 ["options"],
  "form_input/checkbox":               ["options"],
  "form_input/radio_group":            ["options"],
  "form_input/checkbox_group":         ["options"],
  "form_input/search_input":           ["value"],
  "table_op/faceted_filter_bar":       ["filters"],
  "table_op/column_filter":            ["options"],
  "table_op/column_visibility_editor": ["columns"],
  "table_op/sort_control":             ["columns"],
  "table_op/group_by_control":         ["columns"],
  "table_op/bulk_action_panel":        ["actions"],
  "table_op/virtualized_data_table":   ["rows", "columns"],
  "inline_edit/audit_diff_drawer":          ["entries"],
  "calc_topology/aggregation_preview_table": ["data"],
  "calc_topology/hub_statistics_panel":      ["data"],
};

// ── SSOT canonical transform allowlist ───────────────────────────────────────
// Canonical source: admin-console-workflow-ssot.yaml layout_node_props_contract.propBindings.transform.allowlist
// Must match backend/runtime/StructureMapResolver.cs AllowedPropBindingTransforms exactly.

const SSOT_ALLOWED_TRANSFORMS: string[] = [
  "activeColumnsToTableColumns",
  "rowsToOptions",
  "navigationLinksToCardItems",
];

// ── Tests ────────────────────────────────────────────────────────────────────

Deno.test("propBindingContractSync: COMPONENT_ARRAY_PROP_CAPABILITIES matches SSOT canonical table", () => {
  const ssotKeys = Object.keys(SSOT_COMPONENT_ARRAY_PROP_CAPABILITIES).sort();
  const implKeys = Object.keys(COMPONENT_ARRAY_PROP_CAPABILITIES).sort();
  assertEquals(implKeys, ssotKeys, "component kind keys diverged from SSOT");

  for (const kind of ssotKeys) {
    const ssotProps = [...SSOT_COMPONENT_ARRAY_PROP_CAPABILITIES[kind]].sort();
    const implProps = [...(COMPONENT_ARRAY_PROP_CAPABILITIES[kind] ?? [])].sort();
    assertEquals(implProps, ssotProps, `prop list for "${kind}" diverged from SSOT`);
  }
});

Deno.test("propBindingContractSync: ALLOWED_PROP_BINDING_TRANSFORMS matches SSOT canonical allowlist", () => {
  const ssotSorted = [...SSOT_ALLOWED_TRANSFORMS].sort();
  const implSorted = [...ALLOWED_PROP_BINDING_TRANSFORMS].sort();
  assertEquals(implSorted, ssotSorted, "transform allowlist diverged from SSOT");
});

Deno.test("propBindingContractSync: no unknown component kinds in COMPONENT_ARRAY_PROP_CAPABILITIES", () => {
  for (const kind of Object.keys(COMPONENT_ARRAY_PROP_CAPABILITIES)) {
    assertEquals(
      kind in SSOT_COMPONENT_ARRAY_PROP_CAPABILITIES,
      true,
      `Unknown component kind "${kind}" not in SSOT canonical table`,
    );
  }
});

Deno.test("propBindingContractSync: no unknown transforms in ALLOWED_PROP_BINDING_TRANSFORMS", () => {
  for (const t of ALLOWED_PROP_BINDING_TRANSFORMS) {
    assertEquals(
      SSOT_ALLOWED_TRANSFORMS.includes(t),
      true,
      `Unknown transform "${t}" not in SSOT canonical allowlist`,
    );
  }
});
