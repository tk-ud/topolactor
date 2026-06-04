import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { buildAssignPayloadForStep } from "../lib/contentsAssign.ts";
import { emptyManifestScreenDesign } from "../lib/manifestScreenDesign.ts";

Deno.test("buildAssignPayloadForStep step3 preserves initialDataRows lineage in assign payload", () => {
  const design = emptyManifestScreenDesign();
  design.initialDataRows = [{
    values: { age: "not-numeric" },
    lineage: { source: "import_preview", snapshotId: "snap-abc", rowIndex: 0 },
  }];
  const payload = buildAssignPayloadForStep(3, "manifest-1", design, null);
  const row = payload.initialDataRows?.[0] as Record<string, unknown>;
  assertEquals(row?.values, { age: "not-numeric" });
  assertEquals(
    (row?.lineage as Record<string, unknown>)?.source,
    "import_preview",
  );
});
