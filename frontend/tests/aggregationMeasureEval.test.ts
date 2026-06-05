import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { evaluateMeasureOnRows } from "../lib/aggregationMeasureEval.ts";

Deno.test("evaluateMeasureOnRows: aggregates across qualified columns from multiple tables", () => {
  const rows = [
    { "orders.amount": "100", "orders.status": "open" },
    { "orders.amount": "250", "orders.status": "closed" },
    { "orders.amount": "", "orders.status": "open" },
  ];
  assertEquals(
    evaluateMeasureOnRows(rows, { column: "orders.amount", function: "sum" }),
    350,
  );
  assertEquals(
    evaluateMeasureOnRows(rows, { column: "orders.status", function: "count" }),
    3,
  );
});
