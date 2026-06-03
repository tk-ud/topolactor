import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  normalizeAggregationMeasures,
} from "../lib/aggregationMeasures.ts";

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
