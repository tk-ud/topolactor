import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  applySearchConditions,
  parseComparableScalar,
} from "../lib/searchConditionEval.ts";

Deno.test("applySearchConditions: between uses date comparison for date columns", () => {
  const rows = [
    { "events.occurred_at": "2024-06-01" },
    { "events.occurred_at": "2024-06-15" },
    { "events.occurred_at": "2024-07-01" },
  ];
  const filtered = applySearchConditions(rows, [{
    column: "events.occurred_at",
    operator: "between",
    value: "2024-06-10",
    valueTo: "2024-06-20",
  }], { "events.occurred_at": "date" });
  assertEquals(filtered.length, 1);
  assertEquals(filtered[0]!["events.occurred_at"], "2024-06-15");
});

Deno.test("parseComparableScalar: parses ISO date strings", () => {
  const ms = parseComparableScalar("2024-01-15", "date");
  assertEquals(typeof ms, "number");
  assertEquals(Number.isNaN(ms), false);
});
