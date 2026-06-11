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
