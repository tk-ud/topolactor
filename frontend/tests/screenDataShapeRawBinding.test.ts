import { assertEquals } from "jsr:@std/assert";
import type { AggregationBlock } from "../lib/aggregationMeasures.ts";
import {
  formatAggregationSpecFromBlocks,
  formatSearchTargetsRaw,
  parseAggregationSpecToBlocks,
  parseSearchTargetsToColumns,
} from "../lib/screenDataShapeRawBinding.ts";

Deno.test("formatSearchTargetsRaw round-trips column list", () => {
  const cols = ["employees.name", "employees.status"];
  assertEquals(formatSearchTargetsRaw(cols), "employees.name, employees.status");
  assertEquals(parseSearchTargetsToColumns("employees.name, employees.status"), cols);
});

Deno.test("formatAggregationSpecFromBlocks emits SQL-shaped text", () => {
  const raw = formatAggregationSpecFromBlocks([{
    sourceRef: "employees",
    aggregationKey: "employees.dept",
    measures: [{ column: "employees.salary", function: "sum" }],
    searchConditions: [{
      column: "employees.status",
      operator: "=",
      value: "active",
    }],
    havingConditions: [{
      column: "employees.salary",
      function: "sum",
      operator: ">",
      value: "100000",
    }],
  }]);
  assertEquals(raw.includes("FROM employees"), true);
  assertEquals(raw.includes("GROUP BY employees.dept"), true);
  assertEquals(raw.includes("sum(employees.salary)"), true);
  assertEquals(raw.includes("WHERE employees.status = 'active'"), true);
  assertEquals(raw.includes("HAVING sum(employees.salary) > 100000"), true);
});

Deno.test("parseAggregationSpecToBlocks round-trips structured block", () => {
  const block: AggregationBlock = {
    sourceRef: "employees",
    aggregationKey: "employees.dept",
    measures: [
      { column: "employees.salary", function: "sum" },
      { column: "employees.age", function: "max" },
    ],
    searchConditions: [{
      column: "employees.status",
      operator: "=",
      value: "active",
      logicalConnector: "and",
    }, {
      column: "employees.dept",
      operator: "like" as const,
      value: "eng%",
    }],
    havingConditions: [{
      column: "employees.salary",
      function: "sum",
      operator: ">" as const,
      value: "100000",
    }],
  };
  const formatted = formatAggregationSpecFromBlocks([block]);
  const parsed = parseAggregationSpecToBlocks(formatted, "employees");
  assertEquals(parsed.ok, true);
  if (!parsed.ok) return;
  assertEquals(parsed.blocks.length, 1);
  assertEquals(parsed.blocks[0].sourceRef, "employees");
  assertEquals(parsed.blocks[0].aggregationKey, "employees.dept");
  assertEquals(parsed.blocks[0].measures.length, 2);
  assertEquals(parsed.blocks[0].searchConditions.length, 2);
  assertEquals(parsed.blocks[0].havingConditions.length, 1);
});

Deno.test("parseAggregationSpecToBlocks supports legacy one-line fragment", () => {
  const parsed = parseAggregationSpecToBlocks(
    "sum(employees.salary) GROUP BY employees.dept",
    "employees",
  );
  assertEquals(parsed.ok, true);
  if (!parsed.ok) return;
  assertEquals(parsed.blocks[0].aggregationKey, "employees.dept");
  assertEquals(parsed.blocks[0].measures[0].function, "sum");
});
