import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  parseCsvContent,
  previewInitialDataImportLocal,
} from "../lib/initialDataImportPreview.ts";
import { uxImportSchemaOptionLabel } from "../content/adminUxTerms.ts";

Deno.test("parseCsvContent: header and rows", () => {
  const parsed = parseCsvContent("employees.name,note\nAlice,ok\n");
  assertEquals(parsed.ok, true);
  if (parsed.ok) {
    assertEquals(parsed.rows.length, 1);
    assertEquals(parsed.rows[0]!["employees.name"], "Alice");
  }
});

Deno.test("previewInitialDataImportLocal: validates against Step 2 column specs", () => {
  const preview = previewInitialDataImportLocal({
    sourceType: "csv",
    fileName: "rows.csv",
    manifestId: "m1",
    content: "employees.name\nAlice\n",
    columns: [{ key: "employees.name", dataType: "text", nullable: false }],
  });
  assertEquals(preview.ok, true);
  if (preview.ok) {
    assertEquals(preview.validCount, 1);
    assertEquals(preview.schemaId, "step2_logical_columns");
  }
});

Deno.test("previewInitialDataImportLocal: required empty field is invalid", () => {
  const preview = previewInitialDataImportLocal({
    sourceType: "csv",
    fileName: "rows.csv",
    manifestId: "m1",
    content: "employees.name\n\n",
    columns: [{ key: "employees.name", dataType: "text", nullable: false }],
  });
  assertEquals(preview.ok, true);
  if (preview.ok) {
    assertEquals(preview.validCount, 0);
    assertEquals(preview.invalidCount, 1);
  }
});

Deno.test("uxImportSchemaOptionLabel: registry names have Japanese descriptions", () => {
  assertEquals(
    uxImportSchemaOptionLabel("default_schema").includes("汎用"),
    true,
  );
  assertEquals(
    uxImportSchemaOptionLabel("demo_entity_schema").includes("デモ"),
    true,
  );
});
