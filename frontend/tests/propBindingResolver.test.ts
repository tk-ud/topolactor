/**
 * propBindingResolver.test.ts
 *
 * Tests for array prop binding resolution:
 *   - resolveRuntimeDataPath
 *   - resolvePropBindings
 *   - validatePropBindingsStructure
 *   - applyPropBindingTransform
 *   - COMPONENT_ARRAY_PROP_CAPABILITIES
 */
import {
  assertEquals,
  assertExists,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  ALLOWED_PROP_BINDING_TRANSFORMS,
  applyPropBindingTransform,
  COMPONENT_ARRAY_PROP_CAPABILITIES,
  EMISSION_NAVIGATION_SEQUENCE_SOURCE,
  resolvePropBindings,
  resolveRuntimeDataPath,
  resolveRuntimeNavigationSequence,
  validatePropBindingsStructure,
  validatePropBindingTarget,
} from "../runtime/propBindingResolver.ts";
import type { HubNavigationSequenceItem } from "../api/dispatch.ts";

// ── resolveRuntimeDataPath ──────────────────────────────────────────────────

Deno.test("resolveRuntimeDataPath: resolves emission.data.rows", () => {
  const data = { rows: [{ id: 1 }, { id: 2 }] };
  const result = resolveRuntimeDataPath(data, "emission.data.rows");
  assertEquals(result, [{ id: 1 }, { id: 2 }]);
});

Deno.test("resolveRuntimeDataPath: resolves nested path emission.data.meta.count", () => {
  const data = { meta: { count: 42 } };
  const result = resolveRuntimeDataPath(data, "emission.data.meta.count");
  assertEquals(result, 42);
});

Deno.test("resolveRuntimeDataPath: returns undefined for missing path", () => {
  const data: Record<string, unknown> = {};
  const result = resolveRuntimeDataPath(data, "emission.data.rows");
  assertEquals(result, undefined);
});

Deno.test("resolveRuntimeDataPath: returns undefined for non-emission.data. prefix", () => {
  const data = { rows: [] };
  const result = resolveRuntimeDataPath(data, "data.rows");
  assertEquals(result, undefined);
});

Deno.test("resolveRuntimeDataPath: resolves activeColumns", () => {
  const data = { activeColumns: ["name", "age"] };
  const result = resolveRuntimeDataPath(data, "emission.data.activeColumns");
  assertEquals(result, ["name", "age"]);
});

// ── applyPropBindingTransform ────────────────────────────────────────────────

Deno.test("applyPropBindingTransform: activeColumnsToTableColumns converts strings", () => {
  const result = applyPropBindingTransform(["name", "age"], "activeColumnsToTableColumns", { source: "emission.data.activeColumns" });
  assertEquals(result, { ok: true, value: [{ key: "name", header: "name" }, { key: "age", header: "age" }] });
});

Deno.test("applyPropBindingTransform: activeColumnsToTableColumns passes objects through", () => {
  const cols = [{ key: "name", header: "Name" }];
  const result = applyPropBindingTransform(cols, "activeColumnsToTableColumns", { source: "emission.data.activeColumns" });
  assertEquals(result, { ok: true, value: cols });
});

Deno.test("applyPropBindingTransform: activeColumnsToTableColumns uses labelPath on objects", () => {
  const cols = [{ key: "name", displayLabel: "名前" }];
  const result = applyPropBindingTransform(cols, "activeColumnsToTableColumns", { source: "emission.data.activeColumns", labelPath: "displayLabel" });
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals((result.value[0] as Record<string, unknown>).header, "名前");
  }
});

Deno.test("applyPropBindingTransform: rowsToOptions without paths passes through", () => {
  const rows = [{ id: 1, label: "A" }];
  const result = applyPropBindingTransform(rows, "rowsToOptions", { source: "emission.data.rows" });
  assertEquals(result, { ok: true, value: rows });
});

Deno.test("applyPropBindingTransform: rowsToOptions with labelPath and valuePath maps fields", () => {
  const rows = [{ id: 1, name: "Alice" }, { id: 2, name: "Bob" }];
  const result = applyPropBindingTransform(rows, "rowsToOptions", { source: "emission.data.rows", labelPath: "name", valuePath: "id" });
  assertEquals(result.ok, true);
  if (result.ok) {
    const opts = result.value as Array<Record<string, unknown>>;
    assertEquals(opts[0].value, 1);
    assertEquals(opts[0].label, "Alice");
    assertEquals(opts[1].value, 2);
    assertEquals(opts[1].label, "Bob");
  }
});

Deno.test("applyPropBindingTransform: rowsToOptions with only labelPath sets label only", () => {
  const rows = [{ id: 1, name: "Alice" }];
  const result = applyPropBindingTransform(rows, "rowsToOptions", { source: "emission.data.rows", labelPath: "name" });
  assertEquals(result.ok, true);
  if (result.ok) {
    const opt = result.value[0] as Record<string, unknown>;
    assertEquals(opt.label, "Alice");
    assertEquals("value" in opt, false);
  }
});

Deno.test("applyPropBindingTransform: rowsToOptions with keyPath sets key", () => {
  const rows = [{ id: 1, name: "Alice" }, { id: 2, name: "Bob" }];
  const result = applyPropBindingTransform(rows, "rowsToOptions", { source: "emission.data.rows", keyPath: "id", labelPath: "name", valuePath: "id" });
  assertEquals(result.ok, true);
  if (result.ok) {
    const opt = result.value[0] as Record<string, unknown>;
    assertEquals(opt.key, 1);
    assertEquals(opt.label, "Alice");
    assertEquals(opt.value, 1);
  }
});

Deno.test("applyPropBindingTransform: rowsToOptions with only keyPath sets key without label/value", () => {
  const rows = [{ id: 1, name: "Alice" }];
  const result = applyPropBindingTransform(rows, "rowsToOptions", { source: "emission.data.rows", keyPath: "id" });
  assertEquals(result.ok, true);
  if (result.ok) {
    const opt = result.value[0] as Record<string, unknown>;
    assertEquals(opt.key, 1);
    assertEquals("label" in opt, false);
    assertEquals("value" in opt, false);
    assertEquals(opt.name, "Alice");
  }
});

Deno.test("applyPropBindingTransform: unknown transform returns error", () => {
  const result = applyPropBindingTransform([], "eval()", { source: "emission.data.rows" });
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.error.includes("LAYOUT_NODE_PROP_BINDING_INVALID_TRANSFORM"), true);
  }
});

// ── validatePropBindingTarget ────────────────────────────────────────────────

Deno.test("validatePropBindingTarget: data_display/table accepts rows and columns", () => {
  assertEquals(validatePropBindingTarget("data_display/table", "rows"), undefined);
  assertEquals(validatePropBindingTarget("data_display/table", "columns"), undefined);
});

Deno.test("validatePropBindingTarget: action/button rejects any array prop", () => {
  const err = validatePropBindingTarget("action/button", "items");
  assertExists(err);
  assertEquals(err!.includes("LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_COMPONENT"), true);
});

Deno.test("validatePropBindingTarget: data_display/tree rejects unknown prop", () => {
  const err = validatePropBindingTarget("data_display/tree", "rows");
  assertExists(err);
  assertEquals(err!.includes("LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_PROP"), true);
});

Deno.test("validatePropBindingTarget: display/card_list accepts items", () => {
  assertEquals(validatePropBindingTarget("display/card_list", "items"), undefined);
});

Deno.test("validatePropBindingTarget: display/card rejects items (single-record only)", () => {
  const err = validatePropBindingTarget("display/card", "items");
  assertExists(err);
  assertEquals(
    err!.includes("LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_COMPONENT") ||
    err!.includes("LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_PROP"),
    true,
  );
});

// ── resolvePropBindings ──────────────────────────────────────────────────────

Deno.test("resolvePropBindings: emission.data.rows → items prop", () => {
  const baseProps = { data: { label: "List" } };
  const bindings = { items: { source: "emission.data.rows" } };
  const emissionData = { rows: [{ id: 1 }, { id: 2 }] };
  const result = resolvePropBindings(baseProps, bindings, "display/card_list", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.items, [{ id: 1 }, { id: 2 }]);
    assertEquals((result.props.data as Record<string, unknown>).label, "List");
  }
});

Deno.test("resolvePropBindings: emission.data.rows → rows prop for table", () => {
  const baseProps = { data: { label: "Table" } };
  const bindings = { rows: { source: "emission.data.rows" } };
  const emissionData = { rows: [{ name: "Alice" }] };
  const result = resolvePropBindings(baseProps, bindings, "data_display/table", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.rows, [{ name: "Alice" }]);
  }
});

Deno.test("resolvePropBindings: activeColumns + transform → columns", () => {
  const baseProps = {};
  const bindings = { columns: { source: "emission.data.activeColumns", transform: "activeColumnsToTableColumns" } };
  const emissionData = { activeColumns: ["name", "age"] };
  const result = resolvePropBindings(baseProps, bindings, "data_display/table", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.columns, [{ key: "name", header: "name" }, { key: "age", header: "age" }]);
  }
});

Deno.test("resolvePropBindings: missing source path leaves prop absent (no error)", () => {
  const baseProps = { existing: "value" };
  const bindings = { items: { source: "emission.data.rows" } };
  const emissionData: Record<string, unknown> = {};
  const result = resolvePropBindings(baseProps, bindings, "display/card_list", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals("items" in result.props, false);
    assertEquals(result.props.existing, "value");
  }
});

Deno.test("resolvePropBindings: propsJson and propBindings priority — binding wins", () => {
  const baseProps = { items: ["static"] };
  const bindings = { items: { source: "emission.data.rows" } };
  const emissionData = { rows: [{ id: 1 }] };
  const result = resolvePropBindings(baseProps, bindings, "display/card_list", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.items, [{ id: 1 }]);
  }
});

Deno.test("resolvePropBindings: non-emission.data. source returns error", () => {
  const baseProps = {};
  const bindings = { items: { source: "data.rows" } };
  const emissionData = { rows: [] };
  const result = resolvePropBindings(baseProps, bindings, "display/card_list", emissionData);
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.error.includes("LAYOUT_NODE_PROP_BINDING_INVALID_SOURCE"), true);
  }
});

Deno.test("resolvePropBindings: scalar source returns error", () => {
  const baseProps = {};
  const bindings = { items: { source: "emission.data.count" } };
  const emissionData = { count: 42 };
  const result = resolvePropBindings(baseProps, bindings, "display/card_list", emissionData);
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.error.includes("LAYOUT_NODE_PROP_BINDING_SCALAR_SOURCE"), true);
  }
});

Deno.test("resolvePropBindings: non-capability component returns error", () => {
  const baseProps = {};
  const bindings = { items: { source: "emission.data.rows" } };
  const emissionData = { rows: [] };
  const result = resolvePropBindings(baseProps, bindings, "action/button", emissionData);
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.error.includes("LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_COMPONENT"), true);
  }
});

Deno.test("resolvePropBindings: invalid transform returns error", () => {
  const baseProps = {};
  const bindings = { items: { source: "emission.data.rows", transform: "evilEval" } };
  const emissionData = { rows: [1, 2] };
  const result = resolvePropBindings(baseProps, bindings, "display/card_list", emissionData);
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.error.includes("LAYOUT_NODE_PROP_BINDING_INVALID_TRANSFORM"), true);
  }
});

// ── validatePropBindingsStructure ────────────────────────────────────────────

Deno.test("validatePropBindingsStructure: valid binding passes", () => {
  const bindings = {
    items: { source: "emission.data.rows", keyPath: "id", labelPath: "name" },
  };
  const errs = validatePropBindingsStructure(bindings, "display/card_list");
  assertEquals(errs, []);
});

Deno.test("validatePropBindingsStructure: non-object propBindings is error", () => {
  const errs = validatePropBindingsStructure("not an object", "display/card_list");
  assertEquals(errs.length > 0, true);
  assertEquals(errs[0].includes("LAYOUT_NODE_PROP_BINDING_INVALID"), true);
});

Deno.test("validatePropBindingsStructure: invalid source is error", () => {
  const bindings = { items: { source: "wrongprefix.rows" } };
  const errs = validatePropBindingsStructure(bindings, "display/card_list");
  assertEquals(errs.some((e) => e.includes("LAYOUT_NODE_PROP_BINDING_INVALID_SOURCE")), true);
});

Deno.test("validatePropBindingsStructure: non-capability component is error", () => {
  const bindings = { items: { source: "emission.data.rows" } };
  const errs = validatePropBindingsStructure(bindings, "action/button");
  assertEquals(errs.some((e) => e.includes("LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_COMPONENT")), true);
});

Deno.test("validatePropBindingsStructure: invalid transform is error", () => {
  const bindings = { items: { source: "emission.data.rows", transform: "badTransform" } };
  const errs = validatePropBindingsStructure(bindings, "display/card_list");
  assertEquals(errs.some((e) => e.includes("LAYOUT_NODE_PROP_BINDING_INVALID_TRANSFORM")), true);
});

Deno.test("validatePropBindingsStructure: non-string labelPath is error", () => {
  const bindings = { items: { source: "emission.data.rows", labelPath: 123 as unknown as string } };
  const errs = validatePropBindingsStructure(bindings, "display/card_list");
  assertEquals(errs.some((e) => e.includes("LAYOUT_NODE_PROP_BINDING_INVALID_PATH")), true);
});

// ── COMPONENT_ARRAY_PROP_CAPABILITIES ────────────────────────────────────────

Deno.test("COMPONENT_ARRAY_PROP_CAPABILITIES: table accepts rows and columns", () => {
  const caps = COMPONENT_ARRAY_PROP_CAPABILITIES["data_display/table"];
  assertEquals(Array.isArray(caps), true);
  assertEquals(caps.includes("rows"), true);
  assertEquals(caps.includes("columns"), true);
});

Deno.test("COMPONENT_ARRAY_PROP_CAPABILITIES: select accepts options", () => {
  const caps = COMPONENT_ARRAY_PROP_CAPABILITIES["form_input/select"];
  assertEquals(Array.isArray(caps), true);
  assertEquals(caps.includes("options"), true);
});

Deno.test("ALLOWED_PROP_BINDING_TRANSFORMS: contains expected transforms", () => {
  assertEquals(ALLOWED_PROP_BINDING_TRANSFORMS.has("activeColumnsToTableColumns"), true);
  assertEquals(ALLOWED_PROP_BINDING_TRANSFORMS.has("rowsToOptions"), true);
  assertEquals(ALLOWED_PROP_BINDING_TRANSFORMS.has("eval"), false);
});

Deno.test("resolveRuntimeDataPath: resolves emission.data root", () => {
  const data = { rows: [{ id: 1 }], meta: { count: 1 } };
  const result = resolveRuntimeDataPath(data, "emission.data");
  assertEquals(result, data);
});

Deno.test("resolvePropBindings: emission.data → data prop for json viewer", () => {
  const base = { title: "Debug" };
  const data = { rows: [{ id: 1 }], meta: { count: 1 } };
  const result = resolvePropBindings(
    base,
    { data: { source: "emission.data" } },
    "data_display/json",
    data,
  );
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.data, data);
    assertEquals(result.props.title, "Debug");
  }
});

Deno.test("resolvePropBindings: emission.data.preview.groupName → value prop for search_input pre-fill", () => {
  const base = {};
  const data = { preview: { groupName: "demo_status", indexNum: 1 } };
  const result = resolvePropBindings(
    base,
    { value: { source: "emission.data.preview.groupName" } },
    "form_input/search_input",
    data,
  );
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.value, "demo_status");
  }
});

Deno.test("resolvePropBindings: search_input value binding leaves prop absent when source path is missing", () => {
  const base = {};
  const data = { preview: {} };
  const result = resolvePropBindings(
    base,
    { value: { source: "emission.data.preview.groupName" } },
    "form_input/search_input",
    data,
  );
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals("value" in result.props, false);
  }
});

Deno.test("validatePropBindingsStructure: search_input accepts value target", () => {
  const errors = validatePropBindingsStructure(
    { value: { source: "emission.data.preview.groupName" } },
    "form_input/search_input",
  );
  assertEquals(errors, []);
});

Deno.test("validatePropBindingTarget: search_input rejects unknown prop", () => {
  const error = validatePropBindingTarget("form_input/search_input", "items");
  assertEquals(typeof error, "string");
});

Deno.test("validatePropBindingsStructure: json viewer accepts emission.data root", () => {
  const errs = validatePropBindingsStructure(
    { data: { source: "emission.data" } },
    "data_display/json",
  );
  assertEquals(errs, []);
});


Deno.test("resolvePropBindings: aggregation preview table accepts non-array emission.data.aggregationResults as data", () => {
  const result = resolvePropBindings(
    { title: "Aggregation results" },
    { data: { source: "emission.data.aggregationResults" } },
    "calc_topology/aggregation_preview_table",
    { aggregationResults: { rows: [{ group: "A", count: 2 }], totals: { count: 2 } } },
  );

  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.data, { rows: [{ group: "A", count: 2 }], totals: { count: 2 } });
  }
});

Deno.test("resolvePropBindings: hub statistics panel accepts full emission.data as data", () => {
  const emissionData = { aggregationResults: [{ hub: "orders", count: 3 }], summary: { hubs: 1 } };
  const result = resolvePropBindings(
    { title: "Summary stats" },
    { data: { source: "emission.data" } },
    "calc_topology/hub_statistics_panel",
    emissionData,
  );

  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.data, emissionData);
  }
});

Deno.test("validatePropBindingsStructure: aggregate dashboard display components allow data target", () => {
  assertEquals(
    validatePropBindingsStructure(
      { data: { source: "emission.data.aggregationResults" } },
      "calc_topology/aggregation_preview_table",
    ),
    [],
  );
  assertEquals(
    validatePropBindingsStructure(
      { data: { source: "emission.data" } },
      "calc_topology/hub_statistics_panel",
    ),
    [],
  );
});

// ── emission.navigationSequence (canonical hub_relations nav lane) ─────────

const NAV_SEQUENCE: HubNavigationSequenceItem[] = [
  { hubRelationId: "rel-1", topologyManifestId: "source-manifest", relatedHubId: "hub-1", relatedHubLabel: "Credential management", sequencePosition: 1, targetManifestId: "0000000-0000-0000-0000-000000000092" },
  { hubRelationId: "rel-2", topologyManifestId: "source-manifest", relatedHubId: "hub-2", relatedHubLabel: "Unresolved target", sequencePosition: 2, targetManifestId: null },
];

Deno.test("resolveRuntimeNavigationSequence: maps navigationSequence to resolved links, sorted by sequencePosition", () => {
  const result = resolveRuntimeNavigationSequence(NAV_SEQUENCE);
  assertEquals(result, [
    { resolvable: true, href: "?manifest=0000000-0000-0000-0000-000000000092", label: "Credential management", sequencePosition: 1, hubRelationId: "rel-1", topologyManifestId: "source-manifest", relatedHubId: "hub-1" },
    { resolvable: false, label: "Unresolved target", sequencePosition: 2, hubRelationId: "rel-2", topologyManifestId: "source-manifest", relatedHubId: "hub-2" },
  ]);
});

Deno.test("resolveRuntimeNavigationSequence: absent navigationSequence resolves to empty array, not undefined", () => {
  assertEquals(resolveRuntimeNavigationSequence(undefined), []);
});

Deno.test("resolvePropBindings: emission.navigationSequence binds raw resolved links to card_list items", () => {
  const result = resolvePropBindings(
    {},
    { items: { source: EMISSION_NAVIGATION_SEQUENCE_SOURCE } },
    "display/card_list",
    {},
    NAV_SEQUENCE,
  );
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.items, [
      { resolvable: true, href: "?manifest=0000000-0000-0000-0000-000000000092", label: "Credential management", sequencePosition: 1, hubRelationId: "rel-1", topologyManifestId: "source-manifest", relatedHubId: "hub-1" },
      { resolvable: false, label: "Unresolved target", sequencePosition: 2, hubRelationId: "rel-2", topologyManifestId: "source-manifest", relatedHubId: "hub-2" },
    ]);
  }
});

Deno.test("resolvePropBindings: navigationLinksToCardItems transform maps links to CardListItem shape", () => {
  const result = resolvePropBindings(
    {},
    {
      items: {
        source: EMISSION_NAVIGATION_SEQUENCE_SOURCE,
        transform: "navigationLinksToCardItems",
      },
    },
    "display/card_list",
    {},
    NAV_SEQUENCE,
  );
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.items, [
      {
        id: 1,
        title: "Credential management",
        footer: "?manifest=0000000-0000-0000-0000-000000000092",
        variant: "default",
        hubRelationId: "rel-1",
        topologyManifestId: "source-manifest",
        relatedHubId: "hub-1",
      },
      {
        id: 2,
        title: "Unresolved target",
        footer: undefined,
        variant: "warning",
        hubRelationId: "rel-2",
        topologyManifestId: "source-manifest",
        relatedHubId: "hub-2",
      },
    ]);
  }
});

Deno.test("resolvePropBindings: emission.navigationSequence with no navigationSequence arg resolves to empty items", () => {
  const result = resolvePropBindings(
    {},
    { items: { source: EMISSION_NAVIGATION_SEQUENCE_SOURCE } },
    "display/card_list",
    {},
  );
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.props.items, []);
});

Deno.test("resolvePropBindings: rejects an unrecognized source (not emission.data* or emission.navigationSequence)", () => {
  const result = resolvePropBindings(
    {},
    { items: { source: "emission.recommendNavigationProjection" } },
    "display/card_list",
    {},
    NAV_SEQUENCE,
  );
  assertEquals(result.ok, false);
});

Deno.test("validatePropBindingsStructure: emission.navigationSequence is a valid source for card_list items", () => {
  assertEquals(
    validatePropBindingsStructure(
      { items: { source: EMISSION_NAVIGATION_SEQUENCE_SOURCE, transform: "navigationLinksToCardItems" } },
      "display/card_list",
    ),
    [],
  );
});
