/**
 * renderEmissionPropBindings.test.ts
 *
 * Tests for array prop binding integration in renderEmission.
 * Verifies that emission.data.* is resolved and injected into component props
 * via propBindings on layout nodes.
 */
import {
  assertEquals,
  assertExists,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import { mergeNodeLocalProps, renderEmission } from "../runtime/renderEmission.ts";
import { resolvePropBindings } from "../runtime/propBindingResolver.ts";
import {
  buildVisualLayoutPatchJson,
  parseVisualLayoutPatchJson,
} from "../runtime/visualLayoutUtils.ts";
import type { Emission } from "../api/dispatch.ts";

// ── mergeNodeLocalProps regression: existing behavior unchanged ──────────────

Deno.test("mergeNodeLocalProps: static propsJson still works after propBindings addition", () => {
  const base = { data: { label: "Default" } };
  const result = mergeNodeLocalProps(base, '{"data":{"label":"Override"}}', null);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals((result.props.data as Record<string, unknown>).label, "Override");
  }
});

Deno.test("mergeNodeLocalProps: rejects array propsJson (existing invariant)", () => {
  const base = { data: { label: "Default" } };
  const result = mergeNodeLocalProps(base, '[1,2,3]', null);
  assertEquals(result.ok, false);
});

Deno.test("mergeNodeLocalProps: stateJson merged into props.data", () => {
  const base = { data: { open: false } };
  const result = mergeNodeLocalProps(base, null, '{"open":true}');
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals((result.props.data as Record<string, unknown>).open, true);
  }
});

// ── propBindings resolution priority ────────────────────────────────────────

Deno.test("propBindings wins over propsJson for same key (priority 4 > 2)", () => {
  const baseProps = { items: ["static-from-propsJson"] };
  const bindings = { items: { source: "emission.data.rows" } };
  const emissionData = { rows: [{ id: 1 }, { id: 2 }] };
  const result = resolvePropBindings(baseProps, bindings, "display/card_list", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.items, [{ id: 1 }, { id: 2 }]);
  }
});

Deno.test("propBindings: other props from propsJson are preserved", () => {
  const baseProps = { title: "Card List", items: [] };
  const bindings = { items: { source: "emission.data.rows" } };
  const emissionData = { rows: [{ id: 1 }] };
  const result = resolvePropBindings(baseProps, bindings, "display/card_list", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.title, "Card List");
    assertEquals(result.props.items, [{ id: 1 }]);
  }
});

// ── table: rows + columns binding ────────────────────────────────────────────

Deno.test("propBindings: rows and columns bound separately for table", () => {
  const baseProps = {};
  const bindings = {
    rows: { source: "emission.data.rows" },
    columns: { source: "emission.data.activeColumns", transform: "activeColumnsToTableColumns" },
  };
  const emissionData = {
    rows: [{ name: "Alice", age: 30 }],
    activeColumns: ["name", "age"],
  };
  const result = resolvePropBindings(baseProps, bindings, "data_display/table", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.rows, [{ name: "Alice", age: 30 }]);
    assertEquals(result.props.columns, [{ key: "name", header: "name" }, { key: "age", header: "age" }]);
  }
});

// ── select/radio/checkbox: options binding ────────────────────────────────────

Deno.test("propBindings: emission.data.rows → options for form_input/select", () => {
  const baseProps = {};
  const bindings = { options: { source: "emission.data.rows", labelPath: "name", valuePath: "id" } };
  const emissionData = { rows: [{ id: 1, name: "Option A" }, { id: 2, name: "Option B" }] };
  const result = resolvePropBindings(baseProps, bindings, "form_input/select", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(Array.isArray(result.props.options), true);
    assertEquals((result.props.options as unknown[]).length, 2);
  }
});

Deno.test("propBindings: emission.data.rows → options for form_input/radio_group", () => {
  const baseProps = {};
  const bindings = { options: { source: "emission.data.rows" } };
  const emissionData = { rows: [{ id: 1 }, { id: 2 }] };
  const result = resolvePropBindings(baseProps, bindings, "form_input/radio_group", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(Array.isArray(result.props.options), true);
  }
});

// ── tree: nodes binding ──────────────────────────────────────────────────────

Deno.test("propBindings: emission.data.rows → nodes for data_display/tree", () => {
  const baseProps = {};
  const bindings = { nodes: { source: "emission.data.rows" } };
  const emissionData = { rows: [{ id: 1, children: [] }] };
  const result = resolvePropBindings(baseProps, bindings, "data_display/tree", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.nodes, [{ id: 1, children: [] }]);
  }
});

// ── serialization round-trip ─────────────────────────────────────────────────

Deno.test("buildVisualLayoutPatchJson: propBindings round-trips through serialization", () => {
  const nodes = [{
    nodeId: "n1",
    componentKey: "card-list",
    componentKind: "display/card_list",
    isDraftOnly: false,
    slotKey: "root",
    orderIndex: 0,
    parentNodeId: null,
    gridCol: 0,
    gridRow: 0,
    x: 0,
    y: 0,
    width: 400,
    height: 300,
    propBindings: {
      items: { source: "emission.data.rows", keyPath: "id" },
    },
  }];
  const json = buildVisualLayoutPatchJson(nodes);
  const parsed = JSON.parse(json);
  assertExists(parsed.nodes[0].propBindings);
  assertEquals(parsed.nodes[0].propBindings.items.source, "emission.data.rows");
  assertEquals(parsed.nodes[0].propBindings.items.keyPath, "id");
});

Deno.test("parseVisualLayoutPatchJson: propBindings deserialized from layout_patch_json", () => {
  const json = JSON.stringify({
    nodes: [{
      nodeId: "n1",
      componentKey: "table",
      nodeKind: "catalog_component",
      slotKey: "root",
      orderIndex: 0,
      parentNodeId: null,
      gridCol: 0,
      gridRow: 0,
      width: 400,
      height: 300,
      propBindings: {
        rows: { source: "emission.data.rows" },
        columns: { source: "emission.data.activeColumns", transform: "activeColumnsToTableColumns" },
      },
    }],
  });
  const result = parseVisualLayoutPatchJson(json, [{ componentKey: "table", componentKind: "data_display/table", isDraftOnly: false }]);
  assertEquals(result.ok, true);
  if (result.ok) {
    const node = result.value.nodes[0];
    assertExists(node.propBindings);
    assertEquals(node.propBindings!.rows.source, "emission.data.rows");
    assertEquals(node.propBindings!.columns.transform, "activeColumnsToTableColumns");
  }
});

// ── CardList factory end-to-end-ish ─────────────────────────────────────────

Deno.test("CardList: emission.data.rows bound via propBindings resolves to CardListItem shape", () => {
  const baseProps = {};
  const bindings = { items: { source: "emission.data.rows", keyPath: "id" } };
  const emissionData = {
    rows: [
      { id: "a1", title: "記事 A", body: "本文 A", variant: "info" },
      { id: "a2", title: "記事 B", body: "本文 B" },
    ],
  };
  const result = resolvePropBindings(baseProps, bindings, "display/card_list", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    const items = result.props.items as Array<Record<string, unknown>>;
    assertEquals(items.length, 2);
    assertEquals(items[0].id, "a1");
    assertEquals(items[0].title, "記事 A");
    assertEquals(items[1].body, "本文 B");
  }
});

Deno.test("CardList: empty rows array resolves to empty items (no error)", () => {
  const baseProps = { items: [{ id: "stale" }] };
  const bindings = { items: { source: "emission.data.rows" } };
  const emissionData = { rows: [] };
  const result = resolvePropBindings(baseProps, bindings, "display/card_list", emissionData);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.items, []);
  }
});

Deno.test("display/card does NOT accept items prop binding (single-record guard)", () => {
  const baseProps = {};
  const bindings = { items: { source: "emission.data.rows" } };
  const emissionData = { rows: [{ id: 1 }] };
  const result = resolvePropBindings(baseProps, bindings, "display/card", emissionData);
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(
      result.error.includes("LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_COMPONENT") ||
      result.error.includes("LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_PROP"),
      true,
    );
  }
});

// ── projection path survival: renderEmission over collection outer shape ─────
// Proof that rows / activeColumns / displayColumnMode survive the projection
// path from a screen_data_shape_query_result emission (outer shape preserved,
// no rows[0] collapse) into rendered runtimeSpec props via propBindings on an
// arbitrary (non-default) applied topology.

Deno.test("renderEmission: rows / activeColumns / displayColumnMode survive from emission.data branches into runtimeSpec props", () => {
  const emission: Emission = {
    packageId: "bbbbbbbb-0000-0000-0000-000000000002",
    layoutId: "cccccccc-0000-0000-0000-000000000003",
    componentIds: [],
    data: {
      kind: "screen_data_shape_query_result",
      manifestId: "aaaaaaaa-0000-0000-0000-000000000001",
      rows: [
        { name: "Alice", amount: 10 },
        { name: "Bob", amount: 20 },
      ],
      aggregationResults: [],
      activeColumns: ["name", "amount"],
      displayColumnMode: "selected",
    },
    layoutNodes: [
      {
        nodeId: "table-1",
        nodeKind: "catalog_component",
        componentId: "comp-table",
        componentKind: "data_display/table",
        componentKey: "table.primitive",
        orderIndex: 0,
        propBindings: {
          rows: { source: "emission.data.rows" },
          columns: {
            source: "emission.data.activeColumns",
            transform: "activeColumnsToTableColumns",
          },
        },
      },
      {
        nodeId: "json-1",
        nodeKind: "catalog_component",
        componentId: "comp-json",
        componentKind: "data_display/json",
        componentKey: "json.primitive",
        orderIndex: 1,
        propBindings: {
          data: { source: "emission.data" },
        },
      },
    ],
  };

  const specs = renderEmission(emission, {}, { previewMode: true });
  assertEquals(specs.length, 2);

  const tableSpec = specs.find((s) => s.nodeId === "table-1");
  assertExists(tableSpec);
  assertEquals(tableSpec!.componentType, "data_display/table");
  const tableProps = tableSpec!.runtimeSpec?.props as Record<string, unknown>;
  assertExists(tableProps, "table runtimeSpec props must be adapted");
  assertEquals(tableProps.rows, [
    { name: "Alice", amount: 10 },
    { name: "Bob", amount: 20 },
  ]);
  assertEquals(tableProps.columns, [
    { key: "name", header: "name" },
    { key: "amount", header: "amount" },
  ]);

  const jsonSpec = specs.find((s) => s.nodeId === "json-1");
  assertExists(jsonSpec);
  const jsonProps = jsonSpec!.runtimeSpec?.props as Record<string, unknown>;
  assertExists(jsonProps);
  const jsonData = jsonProps.data as Record<string, unknown>;
  // Full emission.data outer shape reaches the component — including
  // displayColumnMode and activeColumns branches (no rows[0] collapse).
  assertEquals(jsonData.displayColumnMode, "selected");
  assertEquals(jsonData.activeColumns, ["name", "amount"]);
  assertEquals((jsonData.rows as unknown[]).length, 2);
});

Deno.test("buildVisualLayoutPatchJson: node without propBindings omits the field", () => {
  const nodes = [{
    nodeId: "n1",
    componentKey: "button",
    componentKind: "action/button",
    isDraftOnly: false,
    slotKey: "root",
    orderIndex: 0,
    parentNodeId: null,
    gridCol: 0,
    gridRow: 0,
    x: 0,
    y: 0,
    width: 100,
    height: 40,
  }];
  const json = buildVisualLayoutPatchJson(nodes);
  const parsed = JSON.parse(json);
  assertEquals("propBindings" in parsed.nodes[0], false);
});
