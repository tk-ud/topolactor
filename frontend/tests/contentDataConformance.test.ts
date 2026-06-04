import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  diagnoseContentDataRows,
  importRecordToRowValues,
} from "../lib/contentDataConformance.ts";
import type { ContentDataRowDraft } from "../lib/manifestScreenDesign.ts";

Deno.test("diagnoseContentDataRows flags type mismatch as non-blocking warning", () => {
  const rows: ContentDataRowDraft[] = [{
    values: { age: "not-a-number" },
    lineage: { source: "manual" },
  }];
  const warnings = diagnoseContentDataRows(rows, [
    { key: "age", dataType: "integer", nullable: true },
  ]);
  assertEquals(warnings.length, 1);
  assertEquals(warnings[0]!.code, "TYPE_MISMATCH");
  assertEquals(warnings[0]!.severity, "warning");
});

Deno.test("diagnoseContentDataRows flags nullable violation and unknown column", () => {
  const rows: ContentDataRowDraft[] = [{
    values: { name: "", extra: "x" },
    lineage: { source: "import_preview", snapshotId: "snap-1", rowIndex: 0 },
  }];
  const warnings = diagnoseContentDataRows(rows, [
    { key: "name", dataType: "text", nullable: false },
  ]);
  const codes = warnings.map((w) => w.code).sort();
  assertEquals(codes, ["NULLABLE_VIOLATION", "UNKNOWN_COLUMN"]);
});

Deno.test("diagnoseContentDataRows applies same rules to manual and import rows", () => {
  const spec = [{ key: "id", dataType: "uuid", nullable: false }];
  const manual = diagnoseContentDataRows([{
    values: { id: "bad" },
    lineage: { source: "manual" },
  }], spec);
  const imported = diagnoseContentDataRows([{
    values: { id: "bad" },
    lineage: { source: "import_applied", snapshotId: "s", rowIndex: 1 },
  }], spec);
  assertEquals(manual[0]!.code, imported[0]!.code);
  assertEquals(manual[0]!.message, imported[0]!.message);
});

Deno.test("importRecordToRowValues maps preview record to qualified keys", () => {
  const values = importRecordToRowValues(
    { name: "Alice", age: 30 },
    ["employees.name"],
  );
  assertEquals(values["employees.name"], "");
  assertEquals(values.name, "Alice");
  assertEquals(values.age, "30");
});
