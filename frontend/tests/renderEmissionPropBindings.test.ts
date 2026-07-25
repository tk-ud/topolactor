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
import {
  buildCatalogComponentEventBinding,
  mergeNodeLocalProps,
  renderEmission,
} from "../runtime/renderEmission.ts";
import { resolvePropBindings } from "../runtime/propBindingResolver.ts";
import {
  buildVisualLayoutPatchJson,
  parseVisualLayoutPatchJson,
} from "../runtime/visualLayoutUtils.ts";
import type { Emission } from "../api/dispatch.ts";
import type { RuntimeDispatchSpec } from "../runtime/frontendScheduler.ts";
import { ensureRuntimeComponentRegistryInitialized } from "../runtime/runtimeComponentRegistry.ts";

const ADMIN_ENUM_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae200";

// ── mergeNodeLocalProps regression: existing behavior unchanged ──────────────

Deno.test("mergeNodeLocalProps: static propsJson still works after propBindings addition", () => {
  const base = { data: { label: "Default" } };
  const result = mergeNodeLocalProps(
    base,
    '{"data":{"label":"Override"}}',
    null,
  );
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(
      (result.props.data as Record<string, unknown>).label,
      "Override",
    );
  }
});

Deno.test("mergeNodeLocalProps: rejects array propsJson (existing invariant)", () => {
  const base = { data: { label: "Default" } };
  const result = mergeNodeLocalProps(base, "[1,2,3]", null);
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
  const result = resolvePropBindings(
    baseProps,
    bindings,
    "display/card_list",
    emissionData,
  );
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.items, [{ id: 1 }, { id: 2 }]);
  }
});

Deno.test("propBindings: other props from propsJson are preserved", () => {
  const baseProps = { title: "Card List", items: [] };
  const bindings = { items: { source: "emission.data.rows" } };
  const emissionData = { rows: [{ id: 1 }] };
  const result = resolvePropBindings(
    baseProps,
    bindings,
    "display/card_list",
    emissionData,
  );
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
    columns: {
      source: "emission.data.activeColumns",
      transform: "activeColumnsToTableColumns",
    },
  };
  const emissionData = {
    rows: [{ name: "Alice", age: 30 }],
    activeColumns: ["name", "age"],
  };
  const result = resolvePropBindings(
    baseProps,
    bindings,
    "data_display/table",
    emissionData,
  );
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.rows, [{ name: "Alice", age: 30 }]);
    assertEquals(result.props.columns, [{ key: "name", header: "name" }, {
      key: "age",
      header: "age",
    }]);
  }
});

// ── select/radio/checkbox: options binding ────────────────────────────────────

Deno.test("propBindings: emission.data.rows → options for form_input/select", () => {
  const baseProps = {};
  const bindings = {
    options: {
      source: "emission.data.rows",
      labelPath: "name",
      valuePath: "id",
    },
  };
  const emissionData = {
    rows: [{ id: 1, name: "Option A" }, { id: 2, name: "Option B" }],
  };
  const result = resolvePropBindings(
    baseProps,
    bindings,
    "form_input/select",
    emissionData,
  );
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
  const result = resolvePropBindings(
    baseProps,
    bindings,
    "form_input/radio_group",
    emissionData,
  );
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
  const result = resolvePropBindings(
    baseProps,
    bindings,
    "data_display/tree",
    emissionData,
  );
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
        columns: {
          source: "emission.data.activeColumns",
          transform: "activeColumnsToTableColumns",
        },
      },
    }],
  });
  const result = parseVisualLayoutPatchJson(json, [{
    componentKey: "table",
    componentKind: "data_display/table",
    isDraftOnly: false,
  }]);
  assertEquals(result.ok, true);
  if (result.ok) {
    const node = result.value.nodes[0];
    assertExists(node.propBindings);
    assertEquals(node.propBindings!.rows.source, "emission.data.rows");
    assertEquals(
      node.propBindings!.columns.transform,
      "activeColumnsToTableColumns",
    );
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
  const result = resolvePropBindings(
    baseProps,
    bindings,
    "display/card_list",
    emissionData,
  );
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
  const result = resolvePropBindings(
    baseProps,
    bindings,
    "display/card_list",
    emissionData,
  );
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.props.items, []);
  }
});

Deno.test("display/card does NOT accept items prop binding (single-record guard)", () => {
  const baseProps = {};
  const bindings = { items: { source: "emission.data.rows" } };
  const emissionData = { rows: [{ id: 1 }] };
  const result = resolvePropBindings(
    baseProps,
    bindings,
    "display/card",
    emissionData,
  );
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

// ─── renderEmission: onNodeValueChange option wires per-node closures ───────

Deno.test("renderEmission: onNodeValueChange option registers per-node closures carrying nodeId", () => {
  ensureRuntimeComponentRegistryInitialized();
  const captured: Array<[string, unknown]> = [];
  const emission: Emission = {
    layoutId: "layout-nv-001",
    layoutNodes: [
      {
        nodeId: "node-nv-1",
        nodeKind: "catalog_component",
        componentId: "comp-nv-001",
        componentKind: "form_input/input",
        componentKey: "text_input.primitive",
        orderIndex: 0,
      },
    ],
  };
  const specs = renderEmission(emission, {}, {
    onNodeValueChange: (nodeId, value) => captured.push([nodeId, value]),
  });
  assertExists(specs[0].runtimeSpec);
  specs[0].runtimeSpec!.onNodeValueChange?.("hello");
  assertEquals(captured, [["node-nv-1", "hello"]]);
});

// ─── renderEmission: applyLiveNodeValueOverride — display authority unified
// with dispatch authority (PR #599 review round 4) ───────────────────────────

Deno.test("renderEmission: payloadFromNodeValues option overrides a form_input/input node's default displayed value when a tracked entry exists", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-display-authority-001",
    layoutNodes: [{
      nodeId: "node-tracked-input",
      nodeKind: "catalog_component",
      componentId: "comp-tracked-input-001",
      componentKind: "form_input/input",
      componentKey: "text_input.primitive",
      orderIndex: 0,
    }],
  };
  const specs = renderEmission(emission, {}, {
    payloadFromNodeValues: { "node-tracked-input": "Alpha" },
  });
  assertExists(specs[0].runtimeSpec);
  const data = specs[0].runtimeSpec!.props.data as Record<string, unknown>;
  assertEquals(data.value, "Alpha");
});

Deno.test("renderEmission: payloadFromNodeValues does NOT override a node with no tracked entry (untouched nodes keep the emission-derived default, no invented value)", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-display-authority-002",
    layoutNodes: [{
      nodeId: "node-untouched-input",
      nodeKind: "catalog_component",
      componentId: "comp-untouched-input-001",
      componentKind: "form_input/input",
      componentKey: "text_input.primitive",
      orderIndex: 0,
    }],
  };
  const specs = renderEmission(emission, {}, {
    payloadFromNodeValues: { "some-other-node": "Alpha" },
  });
  assertExists(specs[0].runtimeSpec);
  const data = specs[0].runtimeSpec!.props.data as Record<string, unknown>;
  assertEquals(data.value, "");
});

Deno.test("renderEmission: payloadFromNodeValues does NOT invent a 'value' field for a component kind that has none (action/button)", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-display-authority-003",
    layoutNodes: [{
      nodeId: "node-button",
      nodeKind: "catalog_component",
      componentId: "comp-button-001",
      componentKind: "action/button",
      componentKey: "button.primitive",
      orderIndex: 0,
    }],
  };
  const specs = renderEmission(emission, {}, {
    // A tracked entry keyed by this exact nodeId, but action/button's default
    // props have no data.value key at all — must not gain one.
    payloadFromNodeValues: { "node-button": "should-not-appear" },
  });
  assertExists(specs[0].runtimeSpec);
  const data = specs[0].runtimeSpec!.props.data as Record<string, unknown>;
  assertEquals(Object.prototype.hasOwnProperty.call(data, "value"), false);
});

Deno.test("renderEmission: own-property identity — a nodeId shaped like an Object.prototype key with no real tracked entry does not override display value", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-display-authority-004",
    layoutNodes: [{
      nodeId: "constructor",
      nodeKind: "catalog_component",
      componentId: "comp-ctor-input-001",
      componentKind: "form_input/input",
      componentKey: "text_input.primitive",
      orderIndex: 0,
    }],
  };
  // payloadFromNodeValues is a plain {} here (own-property-identity-safe by
  // construction per liveNodeValueTracker.ts's Object.create(null) backing;
  // this proves applyLiveNodeValueOverride's OWN hasOwnProperty check holds
  // even against a plain {} with an inherited "constructor" property).
  const specs = renderEmission(emission, {}, {
    payloadFromNodeValues: {},
  });
  assertExists(specs[0].runtimeSpec);
  const data = specs[0].runtimeSpec!.props.data as Record<string, unknown>;
  assertEquals(data.value, "");
});

// ─── Lane 2 payloadFrom binding (admin_runtime, node-level dispatchPayloadFromByTrigger) ──

Deno.test("buildCatalogComponentEventBinding: payloadFrom attaches to the matching trigger only", () => {
  const spec: RuntimeDispatchSpec = {
    operationType: "create_group",
    target: "manifest",
    layer: "enum_dictionary",
    action: "create_group",
    targetRef:
      `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:create_group`,
  };
  const binding = buildCatalogComponentEventBinding(spec, {
    click: { groupName: "node:name_input.value" },
  });
  const clickBinding = binding.click as Record<string, unknown>;
  const clickRd = clickBinding.runtimeDispatch as Record<string, unknown>;
  assertEquals(clickRd.payloadFrom, { groupName: "node:name_input.value" });

  const submitBinding = binding.submit as Record<string, unknown>;
  const submitRd = submitBinding.runtimeDispatch as Record<string, unknown>;
  assertEquals(submitRd.payloadFrom, undefined);
});

// ─── node.dispatchPayloadFromByTrigger fail-close (PR #599 review round 6: a
// node-level field, independent of runtimeInteractions/actionType — malformed
// entries must fail the whole node closed, never skip/filter) ──────────────

function adminRuntimeNodeEmission(
  dispatchPayloadFromByTrigger: unknown,
): Emission {
  return {
    layoutId: "layout-admin-runtime-payloadfrom-failclose",
    layoutNodes: [
      {
        nodeId: "node-submit-button",
        nodeKind: "catalog_component",
        componentId: "comp-submit-button-001",
        componentKind: "action/button",
        componentKey: "button.primitive",
        orderIndex: 0,
        wiringKind: "admin_runtime",
        targetSurface: "manifest",
        targetRef:
          `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:create_group`,
        // deno-lint-ignore no-explicit-any
        dispatchPayloadFromByTrigger: dispatchPayloadFromByTrigger as any,
      },
    ],
  };
}

Deno.test("renderEmission: dispatchPayloadFromByTrigger with an unrecognized trigger key fails the node closed (RUNTIME_INTERACTION_TRIGGER_REQUIRED)", () => {
  ensureRuntimeComponentRegistryInitialized();
  const specs = renderEmission(
    adminRuntimeNodeEmission({
      not_a_real_trigger: { groupName: "node:name_input.value" },
    }),
    {},
  );
  assertEquals(specs[0].componentType, "error");
  const err = JSON.stringify(specs[0].def);
  assertEquals(err.includes("RUNTIME_INTERACTION_TRIGGER_REQUIRED"), true);
});

Deno.test("renderEmission: dispatchPayloadFromByTrigger with a non-object per-trigger entry fails the node closed (RUNTIME_INTERACTION_PAYLOAD_FROM_MUST_BE_OBJECT), not silently skipped", () => {
  ensureRuntimeComponentRegistryInitialized();
  const specs = renderEmission(
    adminRuntimeNodeEmission({ click: "not-an-object" }),
    {},
  );
  assertEquals(specs[0].componentType, "error");
  assertEquals(
    JSON.stringify(specs[0].def).includes(
      "RUNTIME_INTERACTION_PAYLOAD_FROM_MUST_BE_OBJECT",
    ),
    true,
  );
});

Deno.test("renderEmission: dispatchPayloadFromByTrigger with a non-string field source fails the node closed, never silently filtered out", () => {
  ensureRuntimeComponentRegistryInitialized();
  const specs = renderEmission(
    adminRuntimeNodeEmission({
      click: { groupName: "node:name_input.value", indexNum: 42 },
    }),
    {},
  );
  assertEquals(specs[0].componentType, "error");
  assertEquals(
    JSON.stringify(specs[0].def).includes(
      "RUNTIME_INTERACTION_PAYLOAD_FROM_VALUE_MUST_BE_STRING",
    ),
    true,
  );
});

Deno.test("renderEmission: dispatchPayloadFromByTrigger for DIFFERENT triggers each carry their own payloadFrom", () => {
  ensureRuntimeComponentRegistryInitialized();
  const specs = renderEmission(
    adminRuntimeNodeEmission({
      click: {
        groupName: "node:name_input.value",
        indexNum: "node:index_input.value",
      },
      submit: { groupName: "node:name_input.value" },
    }),
    {},
  );
  assertExists(specs[0].runtimeSpec);
  const clickBinding = specs[0].runtimeSpec!.eventBinding["click"] as Record<
    string,
    unknown
  >;
  const clickRd = clickBinding.runtimeDispatch as Record<string, unknown>;
  assertEquals(clickRd.payloadFrom, {
    groupName: "node:name_input.value",
    indexNum: "node:index_input.value",
  });
});

// ─── trigger authority unification (PR #599 review round 7): the SAME 5-trigger
// set buildCatalogComponentEventBinding actually creates bindings for is the sole
// authority dispatchPayloadFromByTrigger's trigger validation uses — a trigger
// normalizeAuthoredEventType() recognizes but that this lane doesn't bind
// (input/focus/blur) must fail close, never silently validate into a payloadFrom
// map with nothing to attach to. Two raw trigger keys normalizing to the same
// canonical trigger must also fail close, never silently let the later key win. ──

Deno.test("renderEmission: dispatchPayloadFromByTrigger — each of the 5 canonical triggers attaches payloadFrom to its own binding", () => {
  ensureRuntimeComponentRegistryInitialized();
  for (const trigger of ["click", "change", "select", "submit", "toggle"]) {
    const specs = renderEmission(
      adminRuntimeNodeEmission({
        [trigger]: { groupName: "node:name_input.value" },
      }),
      {},
    );
    assertExists(specs[0].runtimeSpec, `trigger "${trigger}"`);
    const binding = specs[0].runtimeSpec!.eventBinding[trigger] as Record<
      string,
      unknown
    >;
    const rd = binding.runtimeDispatch as Record<string, unknown>;
    assertEquals(
      rd.payloadFrom,
      { groupName: "node:name_input.value" },
      `trigger "${trigger}"`,
    );
  }
});

Deno.test("renderEmission: dispatchPayloadFromByTrigger — an alias key (e.g. onClick) normalizes onto its canonical binding", () => {
  ensureRuntimeComponentRegistryInitialized();
  const specs = renderEmission(
    adminRuntimeNodeEmission({
      onClick: { groupName: "node:name_input.value" },
    }),
    {},
  );
  assertExists(specs[0].runtimeSpec);
  const clickBinding = specs[0].runtimeSpec!.eventBinding["click"] as Record<
    string,
    unknown
  >;
  const clickRd = clickBinding.runtimeDispatch as Record<string, unknown>;
  assertEquals(clickRd.payloadFrom, { groupName: "node:name_input.value" });
});

Deno.test("renderEmission: dispatchPayloadFromByTrigger — trigger unspecified (absent field) keeps the pre-existing raw event-time payload passthrough for that trigger unchanged", () => {
  ensureRuntimeComponentRegistryInitialized();
  const specs = renderEmission(
    adminRuntimeNodeEmission({ click: { groupName: "node:name_input.value" } }),
    {},
  );
  assertExists(specs[0].runtimeSpec);
  const submitBinding = specs[0].runtimeSpec!.eventBinding["submit"] as Record<
    string,
    unknown
  >;
  const submitRd = submitBinding.runtimeDispatch as Record<string, unknown>;
  assertEquals(submitRd.payloadFrom, undefined);
});

for (const trigger of ["input", "onInput", "focus", "blur"]) {
  Deno.test(`renderEmission: dispatchPayloadFromByTrigger with trigger "${trigger}" (recognized by normalizeAuthoredEventType but not bound by component_wiring_execution_lane) fails the node closed (RUNTIME_INTERACTION_TRIGGER_UNSUPPORTED), never silently dropped`, () => {
    ensureRuntimeComponentRegistryInitialized();
    const specs = renderEmission(
      adminRuntimeNodeEmission({
        [trigger]: { groupName: "node:name_input.value" },
      }),
      {},
    );
    assertEquals(specs[0].componentType, "error");
    assertEquals(
      JSON.stringify(specs[0].def).includes(
        "RUNTIME_INTERACTION_TRIGGER_UNSUPPORTED",
      ),
      true,
      trigger,
    );
  });
}

Deno.test("renderEmission: dispatchPayloadFromByTrigger with a completely unknown trigger fails the node closed (RUNTIME_INTERACTION_TRIGGER_REQUIRED)", () => {
  ensureRuntimeComponentRegistryInitialized();
  const specs = renderEmission(
    adminRuntimeNodeEmission({
      bogusTrigger: { groupName: "node:name_input.value" },
    }),
    {},
  );
  assertEquals(specs[0].componentType, "error");
  assertEquals(
    JSON.stringify(specs[0].def).includes(
      "RUNTIME_INTERACTION_TRIGGER_REQUIRED",
    ),
    true,
  );
});

for (
  const [a, b] of [
    ["click", "onClick"],
    ["change", "onChange"],
    ["submit", "onSubmit"],
    ["select", "onSelect"],
    ["toggle", "onOpen"],
    ["toggle", "onClose"],
    ["onOpen", "onClose"],
  ] as const
) {
  Deno.test(`renderEmission: dispatchPayloadFromByTrigger — "${a}" + "${b}" normalize to the SAME canonical trigger and fail the node closed (RUNTIME_INTERACTION_TRIGGER_CONFLICT_AFTER_NORMALIZATION), never last-wins`, () => {
    ensureRuntimeComponentRegistryInitialized();
    const specs = renderEmission(
      adminRuntimeNodeEmission({
        [a]: { groupName: "literal:A" },
        [b]: { groupName: "literal:B" },
      }),
      {},
    );
    assertEquals(specs[0].componentType, "error");
    assertEquals(
      JSON.stringify(specs[0].def).includes(
        "RUNTIME_INTERACTION_TRIGGER_CONFLICT_AFTER_NORMALIZATION",
      ),
      true,
    );
  });
}

// ─── trim / whitespace-only / empty payloadFrom boundary unification
// (PR #599 review round 8): frontend and backend must accept/reject the
// SAME raw trigger key inputs, including leading/trailing whitespace, and
// an empty per-trigger payloadFrom ({}) must fail closed at every boundary
// (build/persistence/dispatch), not just dispatch time. ─────────────────

for (const trigger of [" click ", " onClick "]) {
  Deno.test(`renderEmission: dispatchPayloadFromByTrigger — whitespace-padded trigger key "${trigger}" trims and normalizes onto its canonical binding`, () => {
    ensureRuntimeComponentRegistryInitialized();
    const specs = renderEmission(
      adminRuntimeNodeEmission({
        [trigger]: { groupName: "node:name_input.value" },
      }),
      {},
    );
    assertExists(specs[0].runtimeSpec, trigger);
    const clickBinding = specs[0].runtimeSpec!.eventBinding["click"] as Record<
      string,
      unknown
    >;
    const clickRd = clickBinding.runtimeDispatch as Record<string, unknown>;
    assertEquals(
      clickRd.payloadFrom,
      { groupName: "node:name_input.value" },
      trigger,
    );
  });
}

Deno.test("renderEmission: dispatchPayloadFromByTrigger with a whitespace-only trigger key fails the node closed (RUNTIME_INTERACTION_TRIGGER_REQUIRED)", () => {
  ensureRuntimeComponentRegistryInitialized();
  const specs = renderEmission(
    adminRuntimeNodeEmission({ "   ": { groupName: "node:name_input.value" } }),
    {},
  );
  assertEquals(specs[0].componentType, "error");
  assertEquals(
    JSON.stringify(specs[0].def).includes(
      "RUNTIME_INTERACTION_TRIGGER_REQUIRED",
    ),
    true,
  );
});

for (
  const [a, b] of [
    ["click", " click "],
    ["onClick", " onClick "],
  ] as const
) {
  Deno.test(`renderEmission: dispatchPayloadFromByTrigger — "${a}" + "${b}" (whitespace-only difference) normalize to the SAME canonical trigger and fail the node closed (RUNTIME_INTERACTION_TRIGGER_CONFLICT_AFTER_NORMALIZATION), never last-wins`, () => {
    ensureRuntimeComponentRegistryInitialized();
    const specs = renderEmission(
      adminRuntimeNodeEmission({
        [a]: { groupName: "literal:A" },
        [b]: { groupName: "literal:B" },
      }),
      {},
    );
    assertEquals(specs[0].componentType, "error");
    assertEquals(
      JSON.stringify(specs[0].def).includes(
        "RUNTIME_INTERACTION_TRIGGER_CONFLICT_AFTER_NORMALIZATION",
      ),
      true,
    );
  });
}

Deno.test("renderEmission: dispatchPayloadFromByTrigger with an empty per-trigger payloadFrom ({}) fails the node closed (RUNTIME_INTERACTION_PAYLOAD_FROM_EMPTY), never silently degraded to binding-unspecified", () => {
  ensureRuntimeComponentRegistryInitialized();
  const specs = renderEmission(
    adminRuntimeNodeEmission({ click: {} }),
    {},
  );
  assertEquals(specs[0].componentType, "error");
  assertEquals(
    JSON.stringify(specs[0].def).includes(
      "RUNTIME_INTERACTION_PAYLOAD_FROM_EMPTY",
    ),
    true,
  );
});

Deno.test("renderEmission: absent dispatchPayloadFromByTrigger renders normally (no payloadFrom attached)", () => {
  ensureRuntimeComponentRegistryInitialized();
  const specs = renderEmission(adminRuntimeNodeEmission(undefined), {});
  assertExists(specs[0].runtimeSpec);
  const clickBinding = specs[0].runtimeSpec!.eventBinding["click"] as Record<
    string,
    unknown
  >;
  const clickRd = clickBinding.runtimeDispatch as Record<string, unknown>;
  assertEquals(clickRd.payloadFrom, undefined);
});
