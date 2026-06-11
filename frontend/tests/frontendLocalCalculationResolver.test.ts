/**
 * frontendLocalCalculationResolver.test.ts
 *
 * Tests for the pure frontend-local calculation engine.
 * No backend dispatch, no fetch, no Preact.
 */
import {
  assertEquals,
  assertFalse,
  assert,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  selectRuleFromTable,
  evaluateCalcBinding,
  evaluateAllCalcBindings,
  validateCalcBinding,
  validateCalcTargetProp,
  CALC_TARGET_PROP_BY_COMPONENT_KIND,
  resolveAllowedTargetProps,
  type CalcBinding,
  type CalcContext,
} from "../runtime/frontendLocalCalculationResolver.ts";
import {
  parseVisualLayoutPatchJson,
  buildVisualLayoutPatchJson,
} from "../runtime/visualLayoutUtils.ts";

// ─── selectRuleFromTable ──────────────────────────────────────────────────────

Deno.test("selectRuleFromTable: transactionType match → taxRate selected", () => {
  const table = [
    { transactionType: "standard", taxRate: 10, enabled: true, priority: 1, effectiveFrom: "2020-01-01" },
    { transactionType: "reduced", taxRate: 8, enabled: true, priority: 1, effectiveFrom: "2020-01-01" },
  ];
  const conditions = [
    { field: "transactionType", valueFrom: { kind: "literal" as const, value: "standard" } },
  ];
  const result = selectRuleFromTable(table, conditions, "taxRate", {});
  assertEquals(result, { ok: true, value: 10 });
});

Deno.test("selectRuleFromTable: priority DESC selects higher priority row", () => {
  const table = [
    { type: "A", rate: 5, enabled: true, priority: 1, effectiveFrom: "2020-01-01" },
    { type: "A", rate: 10, enabled: true, priority: 2, effectiveFrom: "2020-01-01" },
  ];
  const conditions = [
    { field: "type", valueFrom: { kind: "literal" as const, value: "A" } },
  ];
  const result = selectRuleFromTable(table, conditions, "rate", {});
  assertEquals(result, { ok: true, value: 10 });
});

Deno.test("selectRuleFromTable: effectiveFrom DESC tiebreak", () => {
  const table = [
    { type: "B", rate: 5, enabled: true, priority: 1, effectiveFrom: "2021-01-01" },
    { type: "B", rate: 9, enabled: true, priority: 1, effectiveFrom: "2023-01-01" },
  ];
  const conditions = [
    { field: "type", valueFrom: { kind: "literal" as const, value: "B" } },
  ];
  const result = selectRuleFromTable(table, conditions, "rate", {});
  assertEquals(result, { ok: true, value: 9 });
});

Deno.test("selectRuleFromTable: enabled:false filtered out → CALC_RULE_UNRESOLVED", () => {
  const table = [
    { type: "C", rate: 7, enabled: false, priority: 1, effectiveFrom: "2020-01-01" },
  ];
  const conditions = [
    { field: "type", valueFrom: { kind: "literal" as const, value: "C" } },
  ];
  const result = selectRuleFromTable(table, conditions, "rate", {});
  assertEquals(result.ok, false);
  if (!result.ok) {
    assert(result.error.startsWith("CALC_RULE_UNRESOLVED"), `Expected CALC_RULE_UNRESOLVED but got: ${result.error}`);
  }
});

Deno.test("selectRuleFromTable: no matching rows → explicit CALC_RULE_UNRESOLVED error", () => {
  const table = [
    { type: "D", rate: 12, enabled: true, priority: 1, effectiveFrom: "2020-01-01" },
  ];
  const conditions = [
    { field: "type", valueFrom: { kind: "literal" as const, value: "nonexistent" } },
  ];
  const result = selectRuleFromTable(table, conditions, "rate", {});
  assertEquals(result.ok, false);
  if (!result.ok) {
    assert(result.error.startsWith("CALC_RULE_UNRESOLVED"), `Expected CALC_RULE_UNRESOLVED but got: ${result.error}`);
  }
});

// ─── evaluateCalcBinding ──────────────────────────────────────────────────────

Deno.test("evaluateCalcBinding: hours × laborRate → labor cost (ruleTable source)", () => {
  const binding: CalcBinding = {
    calculationId: "labor-cost-calc",
    variables: {
      hours: { kind: "node", nodeId: "hours-input", propKey: "value" },
      rate: {
        kind: "ruleTable",
        tablePath: "emission.data.laborRates",
        matchConditions: [
          { field: "workType", valueFrom: { kind: "literal", value: "engineer" } },
        ],
        priorityOrder: "desc",
        effectiveDateHandling: "latest_effective",
        selectedField: "hourlyRate",
      },
    },
    operation: { op: "multiply", a: "hours", b: "rate" },
    targetNodeId: "labor-cost-output",
    targetProp: "value",
  };
  const ctx: CalcContext = {
    nodeValues: { "hours-input": { value: "8" } },
    emissionData: {
      laborRates: [
        { workType: "engineer", hourlyRate: 5000, enabled: true, priority: 1, effectiveFrom: "2020-01-01" },
      ],
    },
  };
  const result = evaluateCalcBinding(binding, ctx);
  assertEquals(result, { ok: true, value: 40000 });
});

Deno.test("evaluateCalcBinding: price × taxRate → tax included price (ruleTable with node valueFrom)", () => {
  const binding: CalcBinding = {
    calculationId: "tax-included-calc",
    variables: {
      price: { kind: "node", nodeId: "price-input", propKey: "value" },
      taxRate: {
        kind: "ruleTable",
        tablePath: "emission.data.taxRates",
        matchConditions: [
          {
            field: "transactionType",
            valueFrom: { kind: "node", nodeId: "tx-select", propKey: "value" },
          },
        ],
        priorityOrder: "desc",
        effectiveDateHandling: "latest_effective",
        selectedField: "taxRate",
      },
    },
    operation: { op: "taxIncluded", base: "price", rate: "taxRate" },
    targetNodeId: "price-with-tax-output",
    targetProp: "value",
    roundingPolicy: "round",
  };
  const ctx: CalcContext = {
    nodeValues: {
      "price-input": { value: "1000" },
      "tx-select": { value: "standard" },
    },
    emissionData: {
      taxRates: [
        { transactionType: "standard", taxRate: 10, enabled: true, priority: 1, effectiveFrom: "2020-01-01" },
      ],
    },
  };
  const result = evaluateCalcBinding(binding, ctx);
  assertEquals(result, { ok: true, value: 1100 });
});

Deno.test("evaluateCalcBinding: unresolved emission path → explicit CALC_EMISSION_PATH_UNRESOLVED error", () => {
  const binding: CalcBinding = {
    calculationId: "unresolved-emission",
    variables: {
      rate: { kind: "emission", path: "emission.data.nonexistent.path" },
    },
    operation: { op: "multiply", a: "rate", b: "rate" },
    targetNodeId: "output",
    targetProp: "value",
  };
  const ctx: CalcContext = {
    nodeValues: {},
    emissionData: {},
  };
  const result = evaluateCalcBinding(binding, ctx);
  assertEquals(result.ok, false);
  if (!result.ok) {
    assert(
      result.error.includes("CALC_EMISSION_PATH_UNRESOLVED"),
      `Expected CALC_EMISSION_PATH_UNRESOLVED but got: ${result.error}`,
    );
  }
});

Deno.test("evaluateCalcBinding: unresolved ruleTable (no matching row) → explicit CALC_RULE_UNRESOLVED error", () => {
  const binding: CalcBinding = {
    calculationId: "unresolved-rule",
    variables: {
      rate: {
        kind: "ruleTable",
        tablePath: "emission.data.taxRates",
        matchConditions: [
          { field: "transactionType", valueFrom: { kind: "literal", value: "nonexistent_type" } },
        ],
        priorityOrder: "desc",
        effectiveDateHandling: "latest_effective",
        selectedField: "taxRate",
      },
    },
    operation: { op: "multiply", a: "rate", b: "rate" },
    targetNodeId: "output",
    targetProp: "value",
  };
  const ctx: CalcContext = {
    nodeValues: {},
    emissionData: {
      taxRates: [
        { transactionType: "standard", taxRate: 10, enabled: true, priority: 1, effectiveFrom: "2020-01-01" },
      ],
    },
  };
  const result = evaluateCalcBinding(binding, ctx);
  assertEquals(result.ok, false);
  if (!result.ok) {
    assert(
      result.error.includes("CALC_RULE_UNRESOLVED"),
      `Expected CALC_RULE_UNRESOLVED but got: ${result.error}`,
    );
  }
});

Deno.test("evaluateAllCalcBindings: does NOT call fetch / dispatch / queueAdminClientCommand", () => {
  // Spy on globalThis.fetch to verify it is not called
  let fetchCalled = false;
  const originalFetch = (globalThis as Record<string, unknown>).fetch;
  (globalThis as Record<string, unknown>).fetch = (..._args: unknown[]) => {
    fetchCalled = true;
    return Promise.reject(new Error("fetch must not be called in local calculation"));
  };

  try {
    const binding: CalcBinding = {
      calculationId: "no-dispatch-test",
      variables: {
        x: { kind: "literal", value: 42 },
      },
      operation: { op: "multiply", a: "x", b: "x" },
      targetNodeId: "output",
      targetProp: "value",
    };
    const ctx: CalcContext = {
      nodeValues: {},
      emissionData: {},
    };
    evaluateAllCalcBindings([binding], ctx);
    assertFalse(fetchCalled, "fetch must not be called during evaluateAllCalcBindings");
    // Verify there is no queueAdminClientCommand property on globalThis
    assertFalse(
      "queueAdminClientCommand" in globalThis,
      "globalThis must not have queueAdminClientCommand after evaluation",
    );
  } finally {
    (globalThis as Record<string, unknown>).fetch = originalFetch;
  }
});

Deno.test("evaluateCalcBinding: rounding floor with decimals 2", () => {
  // We use literal sources to produce a known result, then apply floor rounding
  // 1234.567 floor to 2 decimals → 1234.56
  // Use: base=1000, rate=23.4567 → taxIncluded = 1000*(1+23.4567/100) = 1234.567
  const binding: CalcBinding = {
    calculationId: "floor-rounding-test",
    variables: {
      base: { kind: "literal", value: 1000 },
      rate: { kind: "literal", value: 23.4567 },
    },
    operation: { op: "taxIncluded", base: "base", rate: "rate" },
    targetNodeId: "output",
    targetProp: "value",
    roundingPolicy: "floor",
    roundingDecimals: 2,
  };
  const ctx: CalcContext = {
    nodeValues: {},
    emissionData: {},
  };
  const result = evaluateCalcBinding(binding, ctx);
  assertEquals(result, { ok: true, value: 1234.56 });
});

// ─── validateCalcBinding ─────────────────────────────────────────────────────

Deno.test("validateCalcBinding: valid binding passes with empty errors array", () => {
  const binding: CalcBinding = {
    calculationId: "valid-calc-1",
    variables: {
      price: { kind: "node", nodeId: "price-input", propKey: "value" },
    },
    operation: { op: "multiply", a: "price", b: "price" },
    targetNodeId: "output-node",
    targetProp: "value",
  };
  const errors = validateCalcBinding(binding);
  assertEquals(errors, []);
});

Deno.test("validateCalcBinding: missing targetNodeId returns error", () => {
  const binding = {
    calculationId: "calc-missing-target",
    variables: {
      x: { kind: "literal", value: 5 },
    },
    operation: { op: "multiply", a: "x", b: "x" },
    targetNodeId: "",
    targetProp: "value",
  };
  const errors = validateCalcBinding(binding);
  assert(errors.length > 0, "should have at least one error");
  assert(
    errors.some((e) => e.includes("targetNodeId")),
    `errors should mention targetNodeId: ${JSON.stringify(errors)}`,
  );
});

// ─── layout_patch_json round-trip test ───────────────────────────────────────

Deno.test("layout_patch_json round-trip: calculationBindings field preserved through parseVisualLayoutPatchJson / buildVisualLayoutPatchJson", () => {
  const calcBindings = [
    {
      calculationId: "test-1",
      targetNodeId: "n1",
      targetProp: "value",
    },
  ];

  const patchJson = JSON.stringify({
    nodes: [{
      nodeId: "n1",
      nodeKind: "catalog_component",
      componentKey: "form_input/text",
      parentNodeId: null,
      slotKey: "main",
      orderIndex: 0,
      propsJson: JSON.stringify({ calculationBindings: calcBindings }),
    }],
  });

  const parsed = parseVisualLayoutPatchJson(patchJson);
  assert(parsed.ok, "should parse successfully");
  if (!parsed.ok) return;

  const node = parsed.value.nodes.find((n) => n.nodeId === "n1");
  assert(node !== undefined, "node n1 should exist");

  const propsObj = JSON.parse(node!.propsJson ?? "{}");
  assert(Array.isArray(propsObj.calculationBindings), "calculationBindings should be an array");
  assertEquals(propsObj.calculationBindings[0].calculationId, "test-1");
});

// ─── applyCalcBindingsToSpecs via renderEmission path ───────────────────────

import {
  applyCalcBindingsToSpecs,
} from "../runtime/renderEmission.ts";
import type { ComponentSpec } from "../runtime/renderEmission.ts";
import {
  buildLayoutPreviewPlaceholderProps,
} from "../runtime/layoutComponentPreview.ts";

Deno.test("applyCalcBindingsToSpecs: injects calculated value into runtimeSpec.props.value", () => {
  const specs: ComponentSpec[] = [
    {
      nodeId: "target-node",
      componentType: "form_input/text",
      def: {},
      runtimeSpec: { componentId: "c1", componentType: "form_input/text", packageId: null, layoutId: null, wiringId: null, props: { data: { label: "工費" } }, eventBinding: {} },
    },
  ];
  const bindings: import("../runtime/frontendLocalCalculationResolver.ts").CalcBinding[] = [
    {
      calculationId: "labor-cost-render",
      variables: {
        hours: { kind: "literal", value: 8 },
        rate: { kind: "literal", value: 5000 },
      },
      operation: { op: "multiply", a: "hours", b: "rate" },
      targetNodeId: "target-node",
      targetProp: "value",
    },
  ];
  const result = applyCalcBindingsToSpecs(specs, bindings, {}, {});
  assertEquals(result.length, 1);
  assertEquals((result[0].runtimeSpec?.props as Record<string, unknown>)?.value, 40000);
});

Deno.test("applyCalcBindingsToSpecs: unresolved calc error is recorded in def.calc_errors, not thrown", () => {
  const specs: ComponentSpec[] = [
    { nodeId: "n1", componentType: "form_input/text", def: {}, runtimeSpec: { componentId: "c1", componentType: "form_input/text", packageId: null, layoutId: null, wiringId: null, props: {}, eventBinding: {} } },
  ];
  const bindings: import("../runtime/frontendLocalCalculationResolver.ts").CalcBinding[] = [
    {
      calculationId: "broken",
      variables: {
        x: { kind: "emission", path: "emission.data.missing.path" },
      },
      operation: { op: "multiply", a: "x", b: "x" },
      targetNodeId: "n1",
      targetProp: "value",
    },
  ];
  const result = applyCalcBindingsToSpecs(specs, bindings, {}, {});
  assertEquals(result.length, 1);
  const calcErrors = (result[0].def as Record<string, unknown>).calc_errors;
  assert(Array.isArray(calcErrors) && calcErrors.length > 0);
  assert(String(calcErrors[0]).startsWith("CALC_EMISSION_PATH_UNRESOLVED"));
});

Deno.test("applyCalcBindingsToSpecs: does not call fetch or dispatch", () => {
  const fetchCalled = { value: false };
  const originalFetch = (globalThis as Record<string, unknown>).fetch;
  (globalThis as Record<string, unknown>).fetch = () => {
    fetchCalled.value = true;
    return Promise.resolve(new Response("{}"));
  };

  const specs: ComponentSpec[] = [
    { nodeId: "n2", componentType: "form_input/text", def: {}, runtimeSpec: { componentId: "c2", componentType: "form_input/text", packageId: null, layoutId: null, wiringId: null, props: {}, eventBinding: {} } },
  ];
  const bindings: import("../runtime/frontendLocalCalculationResolver.ts").CalcBinding[] = [
    {
      calculationId: "no-dispatch",
      variables: { x: { kind: "literal", value: 5 }, y: { kind: "literal", value: 3 } },
      operation: { op: "add", a: "x", b: "y" },
      targetNodeId: "n2",
      targetProp: "value",
    },
  ];
  applyCalcBindingsToSpecs(specs, bindings, {}, {});

  (globalThis as Record<string, unknown>).fetch = originalFetch;
  assertFalse(fetchCalled.value, "fetch must not be called during calc binding evaluation");
  assertEquals(typeof (globalThis as Record<string, unknown>).queueAdminClientCommand, "undefined");
});

Deno.test("applyCalcBindingsToSpecs: no-op when calculationBindings is empty", () => {
  const specs: ComponentSpec[] = [
    { nodeId: "n3", componentType: "form_input/text", def: {} },
  ];
  const result = applyCalcBindingsToSpecs(specs, [], {}, {});
  assertEquals(result, specs);
});

Deno.test("buildVisualLayoutPatchJson / parseVisualLayoutPatchJson: calculationBindings round-trip at root level", () => {
  const bindings: import("../runtime/frontendLocalCalculationResolver.ts").CalcBinding[] = [
    {
      calculationId: "round-trip-test",
      variables: {
        hours: { kind: "node", nodeId: "hours-node", propKey: "value" },
        rate: { kind: "ruleTable", tablePath: "emission.data.laborRates", matchConditions: [], selectedField: "hourlyRate", priorityOrder: "desc", effectiveDateHandling: "latest_effective" },
      },
      operation: { op: "multiply", a: "hours", b: "rate" },
      targetNodeId: "cost-node",
      targetProp: "value",
      roundingPolicy: "round",
    },
  ];
  const json = buildVisualLayoutPatchJson([], [], bindings);
  const parsed = parseVisualLayoutPatchJson(json);
  assert(parsed.ok, "should parse OK");
  assert(Array.isArray(parsed.value.calculationBindings), "calculationBindings should be an array");
  assertEquals(parsed.value.calculationBindings!.length, 1);
  assertEquals(parsed.value.calculationBindings![0].calculationId, "round-trip-test");
  assertEquals(parsed.value.calculationBindings![0].targetNodeId, "cost-node");
  assertEquals(parsed.value.calculationBindings![0].roundingPolicy, "round");
});

Deno.test("buildVisualLayoutPatchJson: no calculationBindings key when array is empty", () => {
  const json = buildVisualLayoutPatchJson([], []);
  const obj = JSON.parse(json) as Record<string, unknown>;
  assertFalse("calculationBindings" in obj, "should not include calculationBindings key when empty");
});

// ─── buildLayoutPreviewPlaceholderProps: calcValueOverrides injection ─────────

Deno.test("buildLayoutPreviewPlaceholderProps: calcValueOverrides.value injects into form_input/input data.value", () => {
  const props = buildLayoutPreviewPlaceholderProps(
    "form_input/input",
    "form_input/input",
    {},
    { value: 15000 },
  );
  const data = (props as Record<string, unknown>).data as Record<string, unknown>;
  assertEquals(data.value, "15000");
});

Deno.test("buildLayoutPreviewPlaceholderProps: no calcValueOverrides → value is empty string for form_input/input", () => {
  const props = buildLayoutPreviewPlaceholderProps("form_input/input", "form_input/input", {});
  const data = (props as Record<string, unknown>).data as Record<string, unknown>;
  assertEquals(data.value, "");
});

Deno.test("buildLayoutPreviewPlaceholderProps: calcValueOverrides.value injected for action/button label", () => {
  const props = buildLayoutPreviewPlaceholderProps(
    "action/button",
    "action/button",
    {},
    { label: "計算結果ボタン" },
  );
  const data = (props as Record<string, unknown>).data as Record<string, unknown>;
  assertEquals(data.label, "計算結果ボタン");
});

Deno.test("unsupported targetProp in ruleTable: literal warning note preserved in binding", () => {
  const binding: import("../runtime/frontendLocalCalculationResolver.ts").CalcBinding = {
    calculationId: "literal-note-test",
    variables: {
      rate: { kind: "literal", value: 0.1, note: "テスト用。業務基準値には literal を使わないでください" },
    },
    operation: { op: "multiply", a: "rate", b: "rate" },
    targetNodeId: "n1",
    targetProp: "value",
  };
  const errors = validateCalcBinding(binding);
  assertEquals(errors.length, 0, "valid binding should have no validation errors");
  const rateSrc = binding.variables.rate as { kind: "literal"; note?: string };
  assert(rateSrc.note?.includes("業務基準値"), "note should warn about business rates");
});

// ─── CALC_TARGET_PROP_BY_COMPONENT_KIND / validateCalcTargetProp ─────────────

Deno.test("CALC_TARGET_PROP_BY_COMPONENT_KIND: form_input/input allows only value", () => {
  assertEquals(CALC_TARGET_PROP_BY_COMPONENT_KIND["form_input/input"], ["value"]);
});

Deno.test("CALC_TARGET_PROP_BY_COMPONENT_KIND: calc_topology/calculation_preview_panel allows only result", () => {
  assertEquals(CALC_TARGET_PROP_BY_COMPONENT_KIND["calc_topology/calculation_preview_panel"], ["result"]);
});

Deno.test("validateCalcTargetProp: form_input/input + value → valid (no errors)", () => {
  const errors = validateCalcTargetProp("form_input/input", "value");
  assertEquals(errors, []);
});

Deno.test("validateCalcTargetProp: form_input/input + result → error (unsupported targetProp)", () => {
  const errors = validateCalcTargetProp("form_input/input", "result");
  assert(errors.length > 0, "should have validation error");
  assert(errors[0].includes("CALC_TARGET_PROP_UNSUPPORTED"), `error code should be CALC_TARGET_PROP_UNSUPPORTED: ${errors[0]}`);
});

Deno.test("validateCalcTargetProp: calc_topology/calculation_preview_panel + result → valid", () => {
  const errors = validateCalcTargetProp("calc_topology/calculation_preview_panel", "result");
  assertEquals(errors, []);
});

Deno.test("validateCalcTargetProp: calc_topology/calculation_preview_panel + value → error", () => {
  const errors = validateCalcTargetProp("calc_topology/calculation_preview_panel", "value");
  assert(errors.length > 0);
  assert(errors[0].includes("CALC_TARGET_PROP_UNSUPPORTED"));
});

Deno.test("validateCalcTargetProp: unknown componentKind → no error (unknown kinds not constrained)", () => {
  const errors = validateCalcTargetProp("unknown/component_kind", "anything");
  assertEquals(errors, []);
});

Deno.test("resolveAllowedTargetProps: returns null for unknown componentKind", () => {
  assertEquals(resolveAllowedTargetProps("not/a/real/kind"), null);
});

Deno.test("resolveAllowedTargetProps: returns allowed props for calc_topology/computed_field_preview", () => {
  const props = resolveAllowedTargetProps("calc_topology/computed_field_preview");
  assert(Array.isArray(props));
  assert(props!.includes("preview"));
  assert(props!.includes("value"));
});

// ─── selectRuleFromTable: node-based matchConditions ─────────────────────────

Deno.test("selectRuleFromTable: transactionType = node value (taxRates.transactionType=node.value)", () => {
  const table = [
    { transactionType: "standard", taxRate: 10, enabled: true, priority: 1, effectiveFrom: "2020-01-01" },
    { transactionType: "reduced", taxRate: 8, enabled: true, priority: 1, effectiveFrom: "2020-01-01" },
  ];
  const conditions = [
    {
      field: "transactionType",
      valueFrom: { kind: "node" as const, nodeId: "typeSelect", propKey: "value" },
    },
  ];
  // Simulate: typeSelect.value = "standard"
  const resolvedNodeValues = { typeSelect: { value: "standard" } };
  const result = selectRuleFromTable(table, conditions, "taxRate", resolvedNodeValues);
  assertEquals(result, { ok: true, value: 10 });
});

Deno.test("selectRuleFromTable: workType = node value (laborRates.workType=node.value)", () => {
  const table = [
    { workType: "electrical", rate: 3500, enabled: true, priority: 1, effectiveFrom: "2023-01-01" },
    { workType: "plumbing", rate: 4200, enabled: true, priority: 1, effectiveFrom: "2023-01-01" },
  ];
  const conditions = [
    {
      field: "workType",
      valueFrom: { kind: "node" as const, nodeId: "workTypeSelect", propKey: "value" },
    },
  ];
  const resolvedNodeValues = { workTypeSelect: { value: "plumbing" } };
  const result = selectRuleFromTable(table, conditions, "rate", resolvedNodeValues);
  assertEquals(result, { ok: true, value: 4200 });
});

// ─── applyCalcBindingsToSpecs: deep merge into props.data ────────────────────

Deno.test("applyCalcBindingsToSpecs: injects value into props.data.value for form_input/input (deep merge)", () => {
  const specs: ComponentSpec[] = [
    {
      nodeId: "cost-input",
      componentType: "form_input/input",
      def: {},
      runtimeSpec: {
        componentId: "c1",
        componentType: "form_input/input",
        packageId: null,
        layoutId: null,
        wiringId: null,
        props: { data: { value: "", placeholder: "工費", disabled: false } },
        eventBinding: {},
      },
    },
  ];
  const bindings: CalcBinding[] = [
    {
      calculationId: "labor-calc",
      variables: {
        hours: { kind: "literal", value: 8 },
        rate: { kind: "literal", value: 5000 },
      },
      operation: { op: "multiply", a: "hours", b: "rate" },
      targetNodeId: "cost-input",
      targetProp: "value",
    },
  ];
  const result = applyCalcBindingsToSpecs(specs, bindings, {}, {});
  const data = (result[0].runtimeSpec?.props as Record<string, unknown>)?.data as Record<string, unknown>;
  assertEquals(data?.value, 40000, "data.value should be 40000 from 8 × 5000");
  // Top-level injection also present
  assertEquals((result[0].runtimeSpec?.props as Record<string, unknown>)?.value, 40000);
});

Deno.test("applyCalcBindingsToSpecs: injects result into props.data.result for calculation_preview_panel", () => {
  const specs: ComponentSpec[] = [
    {
      nodeId: "preview-panel",
      componentType: "calc_topology/calculation_preview_panel",
      def: {},
      runtimeSpec: {
        componentId: "c2",
        componentType: "calc_topology/calculation_preview_panel",
        packageId: null,
        layoutId: null,
        wiringId: null,
        props: { data: { result: null, title: "税込計算" } },
        eventBinding: {},
      },
    },
  ];
  const bindings: CalcBinding[] = [
    {
      calculationId: "tax-calc",
      variables: {
        price: { kind: "literal", value: 1000 },
        rate: { kind: "literal", value: 10 },
      },
      operation: { op: "taxIncluded", base: "price", rate: "rate" },
      targetNodeId: "preview-panel",
      targetProp: "result",
    },
  ];
  const result = applyCalcBindingsToSpecs(specs, bindings, {}, {});
  const data = (result[0].runtimeSpec?.props as Record<string, unknown>)?.data as Record<string, unknown>;
  assertEquals(data?.result, 1100, "data.result should be 1100 from 1000 × (1 + 10/100)");
});

Deno.test("applyCalcBindingsToSpecs: no fetch/dispatch during deep merge evaluation", () => {
  let fetchCalled = false;
  const orig = (globalThis as Record<string, unknown>).fetch;
  (globalThis as Record<string, unknown>).fetch = () => { fetchCalled = true; return Promise.resolve(new Response("{}")); };
  const specs: ComponentSpec[] = [
    { nodeId: "n", componentType: "form_input/input", def: {}, runtimeSpec: { componentId: "c", componentType: "form_input/input", packageId: null, layoutId: null, wiringId: null, props: { data: { value: "" } }, eventBinding: {} } },
  ];
  applyCalcBindingsToSpecs(specs, [{ calculationId: "x", variables: { a: { kind: "literal", value: 5 } }, operation: { op: "multiply", a: "a", b: "a" }, targetNodeId: "n", targetProp: "value" }], {}, {});
  (globalThis as Record<string, unknown>).fetch = orig;
  assertFalse(fetchCalled);
  assertFalse("queueAdminClientCommand" in globalThis);
});
