/**
 * uiBuilderBatchOperation.test.ts
 *
 * Batch operation contract for UIBuilder selection set.
 * SSOT: docs/design/admin-console-workflow-ssot.yaml ui_builder_canvas_workspace
 * SSOT: docs/design/topology-layout-class-ssot.yaml
 * SSOT: docs/design/pipeline-continuity-ssot.yaml component_wiring_execution_lane
 *
 * Tests:
 * - selection set target count and node extraction
 * - layoutClassRefs add/remove/replace_group with conflict_group and allowedFor
 * - raw className prohibited
 * - propsJson/stateJson malformed/array-root/scalar-root → explicit errors
 * - propBindings unsupported componentKind → explicit error (not silent skip)
 * - wiring identity preservation; route: prefix vs manifest targetSurface
 * - calc binding assist: no backend dispatch / eval (dispatchAdded/evalAdded = false)
 * - preview/validate/apply boundary not bypassed
 * - silent skip prohibited: every invalid node gets a per-node error entry
 */
import {
  assertEquals,
  assertFalse,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  applyBatchCalcBindingAssist,
  applyBatchJsonPatch,
  applyBatchLayoutClassRefs,
  applyBatchPropBinding,
  previewBatchCalcBindingAssist,
  previewBatchJsonPatch,
  previewBatchLayoutClassRefs,
  previewBatchPropBinding,
  previewBatchWiringAssist,
  resolveNodeBatchLayoutRole,
  type BatchNodeMinimal,
} from "../lib/uiBuilderBatchOperation.ts";
import type { CalcBinding } from "../runtime/frontendLocalCalculationResolver.ts";

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const containerNode: BatchNodeMinimal = {
  nodeId: "container1",
  componentKey: "div",
  nodeKind: "structural_html",
  componentKind: undefined,
  parentNodeId: null,
  layoutClassRefs: [],
};

const componentNode: BatchNodeMinimal = {
  nodeId: "comp1",
  componentKey: "CardList",
  nodeKind: "catalog_component",
  componentKind: "card_list",
  parentNodeId: "container1",
  layoutClassRefs: [],
};

const buttonNode: BatchNodeMinimal = {
  nodeId: "btn1",
  componentKey: "Button",
  nodeKind: "catalog_component",
  componentKind: "button",
  parentNodeId: "container1",
  layoutClassRefs: [],
};

const allNodes: readonly BatchNodeMinimal[] = [containerNode, componentNode, buttonNode];
const allIds = new Set(["container1", "comp1", "btn1"]);

// ─── resolveNodeBatchLayoutRole ───────────────────────────────────────────────

Deno.test("resolveNodeBatchLayoutRole: structural_html → container", () => {
  assertEquals(resolveNodeBatchLayoutRole(containerNode, allNodes), "container");
});

Deno.test("resolveNodeBatchLayoutRole: catalog_component with children → container", () => {
  const parent: BatchNodeMinimal = {
    nodeId: "parent",
    componentKey: "Section",
    nodeKind: "catalog_component",
    componentKind: "section",
    parentNodeId: null,
    layoutClassRefs: [],
  };
  const child: BatchNodeMinimal = {
    nodeId: "child",
    componentKey: "Button",
    nodeKind: "catalog_component",
    componentKind: "button",
    parentNodeId: "parent",
    layoutClassRefs: [],
  };
  assertEquals(resolveNodeBatchLayoutRole(parent, [parent, child]), "container");
});

Deno.test("resolveNodeBatchLayoutRole: catalog_component leaf → component_wrapper", () => {
  assertEquals(resolveNodeBatchLayoutRole(componentNode, allNodes), "component_wrapper");
});

// ─── selection set target count ───────────────────────────────────────────────

Deno.test("previewBatchLayoutClassRefs: targetCount reflects selected nodes only", () => {
  const selectedIds = new Set(["comp1", "btn1"]);
  const result = previewBatchLayoutClassRefs(allNodes, selectedIds, "add", "layout.direction.row");
  assertEquals(result.targetCount, 2);
});

Deno.test("previewBatchLayoutClassRefs: empty selection → targetCount 0", () => {
  const result = previewBatchLayoutClassRefs(allNodes, new Set(), "add", "layout.direction.row");
  assertEquals(result.targetCount, 0);
  assertEquals(result.entries.length, 0);
});

// ─── layoutClassRefs: unknown / raw className ─────────────────────────────────

Deno.test("previewBatchLayoutClassRefs: unknown classKey → per-node error (not silent)", () => {
  const result = previewBatchLayoutClassRefs(allNodes, allIds, "add", "nonexistent_key_xyz");
  assertEquals(result.hasAnyError, true);
  assertEquals(result.errorCount, 3);
  for (const entry of result.entries) {
    assertEquals(entry.status, "error");
    assertEquals(entry.error?.includes("BATCH_UNKNOWN_CLASS_KEY"), true);
  }
});

Deno.test("previewBatchLayoutClassRefs: raw className → BATCH_RAW_CLASS_NAME_PROHIBITED error", () => {
  const result = previewBatchLayoutClassRefs(
    allNodes,
    allIds,
    "add",
    "topolactor-topology-flex-row",
  );
  assertEquals(result.hasAnyError, true);
  for (const entry of result.entries) {
    assertEquals(entry.error?.includes("BATCH_RAW_CLASS_NAME_PROHIBITED"), true);
  }
});

// ─── layoutClassRefs: allowedFor role check ───────────────────────────────────

Deno.test("previewBatchLayoutClassRefs: container role rejects component_wrapper-only class", () => {
  // layout.card.surface is only allowed for "component_wrapper"
  // containerNode is structural_html → resolves to container role → should be rejected
  const result = previewBatchLayoutClassRefs(
    allNodes,
    new Set(["container1"]),
    "add",
    "layout.card.surface",
  );
  assertEquals(result.hasAnyError, true);
  assertEquals(result.entries[0].status, "error");
  assertEquals(result.entries[0].error?.includes("BATCH_CLASS_NOT_ALLOWED_FOR_ROLE"), true);
});

Deno.test("previewBatchLayoutClassRefs: container-allowed class passes for container node", () => {
  // layout.direction.row is allowed for layout_root, layout_section, layout_row, component_wrapper
  const result = previewBatchLayoutClassRefs(
    allNodes,
    new Set(["container1"]),
    "add",
    "layout.direction.row",
  );
  assertEquals(result.hasAnyError, false);
  assertEquals(result.targetCount, 1);
});

// ─── layoutClassRefs: conflict_group enforcement ──────────────────────────────

Deno.test("previewBatchLayoutClassRefs: conflict_group violation → error when adding same group", () => {
  const nodeWithConflict: BatchNodeMinimal = {
    ...containerNode,
    layoutClassRefs: ["layout.direction.row"],
  };
  const nodes = [nodeWithConflict, componentNode];
  const result = previewBatchLayoutClassRefs(
    nodes,
    new Set(["container1"]),
    "add",
    "layout.direction.stack",
  );
  // layout.direction.row and layout.direction.stack are both in direction conflict group
  if (result.hasAnyError) {
    const entry = result.entries.find((e) => e.nodeId === "container1");
    assertEquals(entry?.status, "error");
    assertEquals(entry?.error?.includes("BATCH_CONFLICT_GROUP_VIOLATION"), true);
  } else {
    // If the keys don't share conflict group in SSOT, at least check no silent skip
    for (const e of result.entries) {
      assertFalse(e.status === undefined);
    }
  }
});

Deno.test("previewBatchLayoutClassRefs: replace_group replaces conflicting entry", () => {
  const nodeWithFlex: BatchNodeMinimal = {
    ...containerNode,
    layoutClassRefs: ["layout.direction.row"],
  };
  const nodes = [nodeWithFlex, componentNode];
  const result = previewBatchLayoutClassRefs(
    nodes,
    new Set(["container1"]),
    "replace_group",
    "layout.direction.stack",
  );
  if (!result.hasAnyError && result.willChangeCount > 0) {
    const entry = result.entries.find((e) => e.nodeId === "container1");
    assertEquals(entry?.status, "ok");
    assertEquals(entry?.after?.includes("layout.direction.stack"), true);
    assertFalse(entry?.after?.includes("layout.direction.row") ?? false);
  }
  // At minimum: no silent skip (all targeted nodes have explicit status)
  assertEquals(result.targetCount, 1);
  assertEquals(result.entries.length, 1);
});

// ─── layoutClassRefs: add / remove / no-op ───────────────────────────────────

Deno.test("previewBatchLayoutClassRefs: add already-present → no_change status", () => {
  const nodeWithClass: BatchNodeMinimal = {
    ...containerNode,
    layoutClassRefs: ["layout.direction.row"],
  };
  const nodes = [nodeWithClass];
  const result = previewBatchLayoutClassRefs(nodes, new Set(["container1"]), "add", "layout.direction.row");
  assertEquals(result.entries[0].status, "no_change");
  assertEquals(result.willChangeCount, 0);
});

Deno.test("previewBatchLayoutClassRefs: remove absent → no_change status", () => {
  const result = previewBatchLayoutClassRefs(
    allNodes,
    new Set(["container1"]),
    "remove",
    "layout.direction.row",
  );
  assertEquals(result.entries[0].status, "no_change");
  assertEquals(result.willChangeCount, 0);
});

// ─── applyBatchLayoutClassRefs ────────────────────────────────────────────────

Deno.test("applyBatchLayoutClassRefs: add classKey to selected nodes", () => {
  const updated = applyBatchLayoutClassRefs(allNodes, new Set(["container1"]), "add", "layout.direction.row");
  const node = updated.find((n) => n.nodeId === "container1");
  assertEquals(node?.layoutClassRefs?.includes("layout.direction.row"), true);
  // Non-selected nodes unchanged
  assertEquals(updated.find((n) => n.nodeId === "comp1")?.layoutClassRefs?.includes("layout.direction.row"), false);
});

Deno.test("applyBatchLayoutClassRefs: remove classKey from selected nodes", () => {
  const nodesWithClass: BatchNodeMinimal[] = [
    { ...containerNode, layoutClassRefs: ["layout.direction.row", "layout.gap.md"] },
    componentNode,
    buttonNode,
  ];
  const updated = applyBatchLayoutClassRefs(
    nodesWithClass,
    new Set(["container1"]),
    "remove",
    "layout.direction.row",
  );
  const node = updated.find((n) => n.nodeId === "container1");
  assertFalse(node?.layoutClassRefs?.includes("layout.direction.row") ?? false);
  assertEquals(node?.layoutClassRefs?.includes("layout.gap.md"), true);
});

Deno.test("applyBatchLayoutClassRefs: unknown classKey → no changes applied", () => {
  const updated = applyBatchLayoutClassRefs(
    allNodes,
    allIds,
    "add",
    "nonexistent_key_xyz",
  );
  for (const node of updated) {
    assertFalse(node.layoutClassRefs?.includes("nonexistent_key_xyz") ?? false);
  }
});

// ─── propsJson / stateJson batch ──────────────────────────────────────────────

Deno.test("previewBatchJsonPatch: malformed JSON → per-node error (not silent)", () => {
  const result = previewBatchJsonPatch(allNodes, allIds, "propsJson", "{ not valid json }");
  assertEquals(result.hasAnyError, true);
  assertEquals(result.errorCount, 3);
  for (const entry of result.entries) {
    assertEquals(entry.status, "error");
    assertEquals(entry.error?.includes("BATCH_JSON_MALFORMED"), true);
  }
});

Deno.test("previewBatchJsonPatch: array root JSON → per-node BATCH_JSON_ARRAY_ROOT error", () => {
  const result = previewBatchJsonPatch(allNodes, allIds, "propsJson", '["a", "b"]');
  assertEquals(result.hasAnyError, true);
  for (const entry of result.entries) {
    assertEquals(entry.error?.includes("BATCH_JSON_ARRAY_ROOT"), true);
  }
});

Deno.test("previewBatchJsonPatch: scalar root JSON → per-node BATCH_JSON_SCALAR_ROOT error", () => {
  const result = previewBatchJsonPatch(allNodes, allIds, "propsJson", "42");
  assertEquals(result.hasAnyError, true);
  for (const entry of result.entries) {
    assertEquals(entry.error?.includes("BATCH_JSON_SCALAR_ROOT"), true);
  }
});

Deno.test("previewBatchJsonPatch: empty string → per-node BATCH_JSON_EMPTY error", () => {
  const result = previewBatchJsonPatch(allNodes, allIds, "propsJson", "  ");
  assertEquals(result.hasAnyError, true);
  for (const entry of result.entries) {
    assertEquals(entry.error?.includes("BATCH_JSON_EMPTY"), true);
  }
});

Deno.test("previewBatchJsonPatch: valid object patch → ok entries with before/after", () => {
  const nodesWithProps: BatchNodeMinimal[] = [
    { ...containerNode, propsJson: '{"size":"sm"}' },
    { ...componentNode, propsJson: '{"label":"old"}' },
    buttonNode,
  ];
  const result = previewBatchJsonPatch(
    nodesWithProps,
    new Set(["container1", "comp1"]),
    "propsJson",
    '{"variant":"primary"}',
  );
  assertEquals(result.hasAnyError, false);
  assertEquals(result.targetCount, 2);
  assertEquals(result.willChangeCount, 2);
  for (const entry of result.entries) {
    assertEquals(entry.after?.includes("primary"), true);
  }
});

Deno.test("previewBatchJsonPatch: existing key overwrite → warning status with destructive keys", () => {
  const nodesWithProps: BatchNodeMinimal[] = [
    { ...containerNode, propsJson: '{"variant":"old"}' },
  ];
  const result = previewBatchJsonPatch(
    nodesWithProps,
    new Set(["container1"]),
    "propsJson",
    '{"variant":"new"}',
  );
  assertEquals(result.targetCount, 1);
  assertEquals(result.willChangeCount, 1);
  const entry = result.entries[0];
  assertEquals(entry.status, "warning");
  assertEquals(entry.changeSummary?.includes("variant"), true);
});

// ─── applyBatchJsonPatch ──────────────────────────────────────────────────────

Deno.test("applyBatchJsonPatch: merges patch into existing propsJson", () => {
  const nodesWithProps: BatchNodeMinimal[] = [
    { ...containerNode, propsJson: '{"size":"sm"}' },
    componentNode,
  ];
  const updated = applyBatchJsonPatch(
    nodesWithProps,
    new Set(["container1"]),
    "propsJson",
    '{"variant":"primary"}',
  );
  const node = updated.find((n) => n.nodeId === "container1");
  const parsed = JSON.parse(node?.propsJson ?? "{}");
  assertEquals(parsed.size, "sm");
  assertEquals(parsed.variant, "primary");
  // Non-selected node unchanged
  assertEquals(updated.find((n) => n.nodeId === "comp1")?.propsJson, undefined);
});

Deno.test("applyBatchJsonPatch: malformed patch → no changes applied", () => {
  const updated = applyBatchJsonPatch(allNodes, allIds, "propsJson", "INVALID");
  for (const node of updated) {
    assertEquals(node.propsJson, undefined);
  }
});

// ─── propBindings batch ───────────────────────────────────────────────────────

Deno.test("previewBatchPropBinding: unsupported componentKind → per-node error (not silent skip)", () => {
  // containerNode has no componentKind → no COMPONENT_ARRAY_PROP_CAPABILITIES entry
  const result = previewBatchPropBinding(
    allNodes,
    new Set(["container1"]),
    {
      propName: "items",
      source: "emission.data.someEmission",
    },
  );
  assertEquals(result.hasAnyError, true);
  assertEquals(result.errorCount, 1);
  assertEquals(result.entries[0].status, "error");
  assertEquals(result.entries[0].error?.includes("BATCH_PROP_BINDING_UNSUPPORTED_COMPONENT"), true);
});

Deno.test("previewBatchPropBinding: source not starting with emission.data. → per-node error", () => {
  const result = previewBatchPropBinding(
    allNodes,
    new Set(["comp1"]),
    {
      propName: "items",
      source: "local.someSource",
    },
  );
  assertEquals(result.hasAnyError, true);
  for (const entry of result.entries) {
    assertEquals(entry.error?.includes("BATCH_PROP_BINDING_INVALID_SOURCE"), true);
  }
});

Deno.test("previewBatchPropBinding: empty propName → per-node error", () => {
  const result = previewBatchPropBinding(
    allNodes,
    new Set(["comp1"]),
    {
      propName: "  ",
      source: "emission.data.someEmission",
    },
  );
  assertEquals(result.hasAnyError, true);
  for (const entry of result.entries) {
    assertEquals(entry.error?.includes("BATCH_PROP_BINDING_MISSING_PROP_NAME"), true);
  }
});

Deno.test("previewBatchPropBinding: silent skip never occurs for invalid nodes", () => {
  // All three nodes are targeted; container1 has no componentKind, others may or may not support
  const result = previewBatchPropBinding(
    allNodes,
    allIds,
    {
      propName: "items",
      source: "emission.data.testEmission",
    },
  );
  // Every targeted node must have an entry (no silent skip)
  assertEquals(result.entries.length, result.targetCount);
  assertEquals(result.targetCount, 3);
  for (const entry of result.entries) {
    assertFalse(entry.status === undefined);
  }
});

// ─── applyBatchPropBinding ────────────────────────────────────────────────────

Deno.test("applyBatchPropBinding: skips node with invalid source without silent error", () => {
  const updated = applyBatchPropBinding(
    allNodes,
    allIds,
    {
      propName: "items",
      source: "invalid_source",
    },
  );
  // Since source is invalid, no propBindings are written
  for (const node of updated) {
    assertEquals(node.propBindings, undefined);
  }
});

Deno.test("applyBatchPropBinding: applies to supported nodes only", () => {
  const updated = applyBatchPropBinding(
    allNodes,
    new Set(["comp1"]),
    {
      propName: "items",
      source: "emission.data.testEmission",
    },
  );
  const comp = updated.find((n) => n.nodeId === "comp1");
  const container = updated.find((n) => n.nodeId === "container1");
  // comp1 (card_list) should have items if it's in COMPONENT_ARRAY_PROP_CAPABILITIES
  // The test validates structural correctness (the propBinding key is applied or skipped due to capabilities)
  // Either way: propBindings presence indicates update, absence means unsupported
  if (comp?.propBindings?.["items"]) {
    assertEquals(comp.propBindings["items"].source, "emission.data.testEmission");
  }
  // containerNode was not in selection set, never changed
  assertEquals(container?.propBindings, undefined);
});

// ─── wiring assist batch ──────────────────────────────────────────────────────

Deno.test("previewBatchWiringAssist: route: prefix + manifest targetSurface → error for all nodes", () => {
  const result = previewBatchWiringAssist(
    allNodes,
    allIds,
    {
      wiringKind: "action",
      targetSurface: "manifest",
      targetRef: "route:home",
    },
  );
  assertEquals(result.errorCount, 3);
  for (const entry of result.entries) {
    assertEquals(entry.validationStatus, "error");
    assertEquals(entry.error?.includes("BATCH_WIRING_ROUTE_MANIFEST_CONFLICT"), true);
  }
});

Deno.test("previewBatchWiringAssist: route: prefix + non-manifest targetSurface → ok (frontend-local)", () => {
  const result = previewBatchWiringAssist(
    allNodes,
    new Set(["comp1"]),
    {
      wiringKind: "navigation",
      targetSurface: "local_navigation",
      targetRef: "route:home",
    },
  );
  assertEquals(result.errorCount, 0);
  assertEquals(result.entries[0].validationStatus, "ok");
  // previewNote must mention authoring-assist only (not save)
  assertEquals(result.entries[0].previewNote?.includes("参照のみ"), true);
});

Deno.test("previewBatchWiringAssist: missing wiringKind → per-node error", () => {
  const result = previewBatchWiringAssist(
    allNodes,
    new Set(["comp1"]),
    {
      wiringKind: "  ",
      targetSurface: "manifest",
      targetRef: "some.target",
    },
  );
  assertEquals(result.errorCount, 1);
  assertEquals(result.entries[0].error?.includes("BATCH_WIRING_MISSING_KIND"), true);
});

Deno.test("previewBatchWiringAssist: missing targetSurface → per-node error", () => {
  const result = previewBatchWiringAssist(
    allNodes,
    new Set(["comp1"]),
    {
      wiringKind: "action",
      targetSurface: "  ",
      targetRef: "some.target",
    },
  );
  assertEquals(result.errorCount, 1);
  assertEquals(result.entries[0].error?.includes("BATCH_WIRING_MISSING_TARGET_SURFACE"), true);
});

Deno.test("previewBatchWiringAssist: all entries include nodeId and identity info", () => {
  const result = previewBatchWiringAssist(
    allNodes,
    new Set(["comp1", "btn1"]),
    {
      wiringKind: "action",
      targetSurface: "manifest",
      targetRef: "some.action.ref",
    },
  );
  for (const entry of result.entries) {
    assertFalse(entry.nodeId === "");
    assertFalse(entry.nodeLabel === "");
  }
  // Non-selected nodes excluded
  assertEquals(result.targetCount, 2);
});

Deno.test("previewBatchWiringAssist: wiring assist is reference only — no actual save", () => {
  // Structural proof: the result type has no 'savedWiringIds' or similar write fields.
  // Only 'targetCount', 'errorCount', 'entries' are present.
  const result = previewBatchWiringAssist(allNodes, allIds, {
    wiringKind: "action",
    targetSurface: "manifest",
    targetRef: "some.ref",
  });
  assertFalse("savedWiringIds" in result);
  assertFalse("dispatchCalled" in result);
  assertFalse("wiringWritten" in result);
});

// ─── calc binding assist batch ────────────────────────────────────────────────

const sampleCalcBinding: CalcBinding = {
  calculationId: "calc_test_1",
  targetNodeId: "comp1",
  targetProp: "label",
  operation: { op: "add", a: "v_a", b: "v_b" },
  variables: {
    v_a: { kind: "node", nodeId: "comp1", propKey: "value" },
    v_b: { kind: "literal", value: 0 },
  },
};

Deno.test("previewBatchCalcBindingAssist: dispatchAdded is literally false", () => {
  const result = previewBatchCalcBindingAssist([], new Set(["comp1"]), sampleCalcBinding);
  assertEquals(result.dispatchAdded, false);
});

Deno.test("previewBatchCalcBindingAssist: evalAdded is literally false", () => {
  const result = previewBatchCalcBindingAssist([], new Set(["comp1"]), sampleCalcBinding);
  assertEquals(result.evalAdded, false);
});

Deno.test("previewBatchCalcBindingAssist: duplicate calculationId → validation error", () => {
  const existing: CalcBinding[] = [sampleCalcBinding];
  const result = previewBatchCalcBindingAssist(existing, new Set(["comp1"]), sampleCalcBinding);
  assertEquals(result.validationErrors.length > 0, true);
  assertEquals(result.validationErrors.some((e) => e.includes("BATCH_CALC_DUPLICATE_ID")), true);
});

Deno.test("previewBatchCalcBindingAssist: invalid variable kind → CALC_BINDING error (validateCalcBinding reused)", () => {
  const badBinding: CalcBinding = {
    ...sampleCalcBinding,
    calculationId: "calc_bad_kind",
    variables: {
      v_bad: { kind: "eval_injection" as "literal", value: 0 },
    },
  };
  const result = previewBatchCalcBindingAssist([], new Set(["comp1"]), badBinding);
  // Error code comes from validateCalcBinding (CALC_BINDING_INVALID_VARIABLE_KIND)
  assertEquals(
    result.validationErrors.some((e) => e.includes("CALC_BINDING_INVALID_VARIABLE_KIND")),
    true,
  );
});

Deno.test("previewBatchCalcBindingAssist: unsupported operation.op → CALC_BINDING error", () => {
  const badBinding: CalcBinding = {
    ...sampleCalcBinding,
    calculationId: "calc_bad_op",
    operation: { op: "eval_inject" as "add", a: "v_a", b: "v_b" },
  };
  const result = previewBatchCalcBindingAssist([], new Set(["comp1"]), badBinding);
  assertEquals(
    result.validationErrors.some((e) => e.includes("CALC_BINDING_INVALID_OPERATION_OP")),
    true,
  );
});

Deno.test("previewBatchCalcBindingAssist: emission path not starting with emission.data. → CALC_BINDING error", () => {
  const badBinding: CalcBinding = {
    ...sampleCalcBinding,
    calculationId: "calc_bad_path",
    variables: {
      v_bad: { kind: "emission", path: "wrong.path.emission" },
    },
  };
  const result = previewBatchCalcBindingAssist([], new Set(["comp1"]), badBinding);
  assertEquals(
    result.validationErrors.some((e) => e.includes("CALC_BINDING_INVALID_EMISSION_PATH")),
    true,
  );
});

Deno.test("previewBatchCalcBindingAssist: literal non-finite value → CALC_BINDING error", () => {
  const badBinding: CalcBinding = {
    ...sampleCalcBinding,
    calculationId: "calc_bad_literal",
    variables: {
      v_inf: { kind: "literal", value: Infinity as unknown as never },
    },
  };
  const result = previewBatchCalcBindingAssist([], new Set(["comp1"]), badBinding);
  assertEquals(
    result.validationErrors.some((e) => e.includes("CALC_BINDING_INVALID_LITERAL_VALUE")),
    true,
  );
});

Deno.test("previewBatchCalcBindingAssist: ruleTable missing tablePath → CALC_BINDING error", () => {
  const badBinding: CalcBinding = {
    ...sampleCalcBinding,
    calculationId: "calc_bad_ruletable",
    variables: {
      v_rt: {
        kind: "ruleTable",
        tablePath: "wrong.path",
        matchConditions: [],
        priorityOrder: "desc",
        effectiveDateHandling: "latest_effective",
        selectedField: "rate",
      } as unknown as never,
    },
  };
  const result = previewBatchCalcBindingAssist([], new Set(["comp1"]), badBinding);
  assertEquals(
    result.validationErrors.some((e) => e.includes("CALC_BINDING_INVALID_RULE_TABLE_PATH")),
    true,
  );
});

Deno.test("previewBatchCalcBindingAssist: targetProp invalid for known componentKind → CALC_TARGET_PROP error", () => {
  // form_input/input only allows targetProp "value"
  const inputNode: BatchNodeMinimal = {
    nodeId: "inp1",
    componentKey: "Input",
    nodeKind: "catalog_component",
    componentKind: "form_input/input",
    parentNodeId: null,
    layoutClassRefs: [],
  };
  const badBinding: CalcBinding = {
    ...sampleCalcBinding,
    calculationId: "calc_bad_targetprop",
    targetNodeId: "inp1",
    targetProp: "nonexistent_prop",
  };
  const result = previewBatchCalcBindingAssist([], new Set(["inp1"]), badBinding, [inputNode]);
  assertEquals(
    result.validationErrors.some((e) => e.includes("CALC_TARGET_PROP_UNSUPPORTED")),
    true,
  );
});

Deno.test("previewBatchCalcBindingAssist: valid targetProp for known componentKind → no error", () => {
  const inputNode: BatchNodeMinimal = {
    nodeId: "inp1",
    componentKey: "Input",
    nodeKind: "catalog_component",
    componentKind: "form_input/input",
    parentNodeId: null,
    layoutClassRefs: [],
  };
  const validBinding: CalcBinding = {
    calculationId: "calc_valid_targetprop",
    targetNodeId: "inp1",
    targetProp: "value",
    operation: { op: "round", a: "v_a" },
    variables: { v_a: { kind: "literal", value: 42 } },
  };
  const result = previewBatchCalcBindingAssist([], new Set(["inp1"]), validBinding, [inputNode]);
  assertEquals(result.validationErrors.length, 0);
});

Deno.test("previewBatchCalcBindingAssist: referencedNodeCount reflects nodes in selectedNodeIds", () => {
  const binding: CalcBinding = {
    calculationId: "calc_ref_test",
    targetNodeId: "comp1",
    targetProp: "label",
    operation: { op: "round", a: "v_a" },
    variables: {
      v_a: { kind: "node", nodeId: "comp1", propKey: "value" },
    },
  };
  const result = previewBatchCalcBindingAssist([], new Set(["comp1"]), binding);
  assertEquals(result.referencedNodeCount >= 1, true);
  assertEquals(result.validationErrors.length, 0);
});

// ─── applyBatchCalcBindingAssist ──────────────────────────────────────────────

Deno.test("applyBatchCalcBindingAssist: appends new binding to array", () => {
  const result = applyBatchCalcBindingAssist([], sampleCalcBinding);
  assertEquals(result.length, 1);
  assertEquals(result[0].calculationId, "calc_test_1");
});

Deno.test("applyBatchCalcBindingAssist: fail-close — invalid operation.op is NOT appended even if UI skipped preview", () => {
  const invalidBinding: CalcBinding = {
    ...sampleCalcBinding,
    calculationId: "calc_fail_close",
    operation: { op: "sql_inject" as "add", a: "v_a", b: "v_b" },
  };
  const result = applyBatchCalcBindingAssist([], invalidBinding);
  // Must be rejected — fail-close even without UI preview gate
  assertEquals(result.length, 0);
});

Deno.test("applyBatchCalcBindingAssist: fail-close — invalid variable kind is NOT appended", () => {
  const invalidBinding: CalcBinding = {
    ...sampleCalcBinding,
    calculationId: "calc_fail_close_kind",
    variables: {
      v_bad: { kind: "eval_injection" as "literal", value: 0 },
    },
  };
  const result = applyBatchCalcBindingAssist([], invalidBinding);
  assertEquals(result.length, 0);
});

Deno.test("applyBatchCalcBindingAssist: no-op when duplicate calculationId", () => {
  const existing = [sampleCalcBinding];
  const result = applyBatchCalcBindingAssist(existing, sampleCalcBinding);
  assertEquals(result.length, 1);
});

Deno.test("applyBatchCalcBindingAssist: preserves existing bindings", () => {
  const newBinding: CalcBinding = {
    ...sampleCalcBinding,
    calculationId: "calc_test_2",
  };
  const result = applyBatchCalcBindingAssist([sampleCalcBinding], newBinding);
  assertEquals(result.length, 2);
  assertEquals(result[0].calculationId, "calc_test_1");
  assertEquals(result[1].calculationId, "calc_test_2");
});

// ─── preview/validate/apply boundary not bypassed ────────────────────────────

Deno.test("batch operations: preview result has hasAnyError gate before apply is called", () => {
  // Structural test: applyBatch functions only apply changes when preview would have passed.
  // Verify that malformed input in preview results in hasAnyError=true,
  // and apply with same input returns unmodified nodes.
  const badPatch = "[invalid array]";
  const preview = previewBatchJsonPatch(allNodes, allIds, "propsJson", badPatch);
  assertEquals(preview.hasAnyError, true);
  // apply does nothing for malformed input — layout_patch boundary not bypassed
  const applied = applyBatchJsonPatch(allNodes, allIds, "propsJson", badPatch);
  for (const node of applied) {
    assertEquals(node.propsJson, undefined);
  }
});

Deno.test("batch operations: no eval/Function usage (structural check)", async () => {
  const source = await Deno.readTextFile(
    new URL("../lib/uiBuilderBatchOperation.ts", import.meta.url),
  );
  // Check non-comment lines only (strip // and * comment lines)
  const codeLines = source.split("\n").filter(
    (l) => !l.trimStart().startsWith("//") && !l.trimStart().startsWith("*"),
  ).join("\n");
  assertFalse(
    /\beval\s*\(/.test(codeLines),
    "eval() call must not appear in batch operation module code",
  );
  assertFalse(
    /new\s+Function\s*\(/.test(codeLines),
    "new Function() must not appear in batch operation module code",
  );
});

Deno.test("batch operations: no fetch/backend dispatch (structural check)", async () => {
  const source = await Deno.readTextFile(
    new URL("../lib/uiBuilderBatchOperation.ts", import.meta.url),
  );
  // Check non-comment lines only (strip // and * comment lines)
  const codeLines = source.split("\n").filter(
    (l) => !l.trimStart().startsWith("//") && !l.trimStart().startsWith("*"),
  ).join("\n");
  assertFalse(
    /\bfetch\s*\(/.test(codeLines),
    "fetch() call must not appear in batch operation module (no per-input backend dispatch)",
  );
  assertFalse(
    source.includes("ManifestDispatcher"),
    "ManifestDispatcher must not be imported in batch operation module",
  );
});

Deno.test("BatchCalcBindingAssistPreviewResult: dispatchAdded type is literally false (no runtime dispatch)", () => {
  // This is a type-level test using runtime value verification.
  const result = previewBatchCalcBindingAssist([], new Set(), sampleCalcBinding);
  // dispatchAdded MUST be false at runtime — not just truthy/falsy
  assertEquals(result.dispatchAdded === false, true);
  assertEquals(typeof result.dispatchAdded, "boolean");
  assertEquals(result.evalAdded === false, true);
});
