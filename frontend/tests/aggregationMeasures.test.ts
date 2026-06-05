import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  flattenAggregationBlocks,
  hydrateAggregationBlockConditions,
  normalizeAggregationBlocks,
  normalizeAggregationMeasures,
} from "../lib/aggregationMeasures.ts";
import { patchAggregationBlocks } from "../lib/manifestScreenDesign.ts";

Deno.test("normalizeAggregationMeasures: prefers aggregationMeasures array", () => {
  const m = normalizeAggregationMeasures({
    aggregationMeasures: [{ column: "salary", function: "sum" }],
    aggregationFunction: "max",
    aggregationColumns: ["other"],
  });
  assertEquals(m, [{ column: "salary", function: "sum" }]);
});

Deno.test("normalizeAggregationMeasures: migrates legacy function + columns", () => {
  const m = normalizeAggregationMeasures({
    aggregationFunction: "avg",
    aggregationColumns: ["salary", "bonus"],
  });
  assertEquals(m.length, 2);
  assertEquals(m[0], { column: "salary", function: "avg" });
});

Deno.test("normalizeAggregationBlocks: migrates legacy flat measures into one block", () => {
  const blocks = normalizeAggregationBlocks({
    aggregationKey: "employees.status",
    aggregationMeasures: [
      { column: "employees.salary", function: "sum" },
      { column: "auth.user.id", function: "count" },
    ],
    primarySourceRef: "employees",
  });
  assertEquals(blocks.length, 1);
  assertEquals(blocks[0]!.sourceRef, "employees");
  assertEquals(blocks[0]!.measures.length, 2);
});

Deno.test("patchAggregationBlocks: keeps in-progress measure row after 集計式を追加", () => {
  const patched = patchAggregationBlocks([
    {
      sourceRef: "employees",
      aggregationKey: "",
      measures: [
        { column: "employees.salary", function: "sum" },
        { column: "", function: "" },
      ],
      searchConditions: [],
      havingConditions: [],
    },
  ]);
  assertEquals(patched.aggregationBlocks[0]!.measures.length, 2);
  assertEquals(patched.aggregationMeasures.length, 1);
  assertEquals(patched.searchConditions?.length ?? 0, 0);
});

Deno.test("hydrateAggregationBlockConditions: moves legacy global search into first block", () => {
  const blocks = hydrateAggregationBlockConditions(
    [{ sourceRef: "employees", aggregationKey: "", measures: [], searchConditions: [], havingConditions: [] }],
    [{ column: "employees.status", operator: "=", value: "active" }],
    [],
  );
  assertEquals(blocks[0]!.searchConditions.length, 1);
  assertEquals(blocks[0]!.searchConditions[0].value, "active");
});

Deno.test("flattenAggregationBlocks: preserves all measures for legacy runtime fields", () => {
  const flat = flattenAggregationBlocks([
    {
      sourceRef: "employees",
      aggregationKey: "employees.status",
      measures: [{ column: "employees.salary", function: "sum" }],
      searchConditions: [],
      havingConditions: [],
    },
    {
      sourceRef: "auth.user",
      aggregationKey: "",
      measures: [{ column: "auth.user.id", function: "count" }],
      searchConditions: [],
      havingConditions: [],
    },
  ]);
  assertEquals(flat.aggregationMeasures.length, 2);
  assertEquals(flat.aggregationKey, "employees.status");
});
