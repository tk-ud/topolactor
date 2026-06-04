import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import {
  assignSearchConditionsFromDesign,
  buildScreenReadQueryWiringCandidates,
  literalValueSource,
} from "../lib/screenReadQueryWiring.ts";
import { emptyManifestScreenDesign } from "../lib/manifestScreenDesign.ts";

Deno.test("literalValueSource: separates preview sample from runtime binding", () => {
  const src = literalValueSource("preview-only");
  assertEquals(src.kind, "literal");
  assertEquals(src.sampleValue, "preview-only");
});

Deno.test("assignSearchConditionsFromDesign: attaches literal valueSource per condition", () => {
  const design = emptyManifestScreenDesign();
  design.searchConditions = [{ column: "col_a", operator: "=", value: "x" }];
  const out = assignSearchConditionsFromDesign(design);
  assertEquals(out.length, 1);
  assertEquals(out[0].valueSource?.kind, "literal");
  assertEquals(out[0].valueSource?.sampleValue, "x");
});

Deno.test("buildScreenReadQueryWiringCandidates: extracts wiring keys from topology", () => {
  const topology = JSON.stringify([
    {
      type: "screen_data_shape",
      screenReadQueryWiring: {
        searchConditions: {
          wiringKey: "screen.searchConditions",
          bindings: [{ wiringKey: "screen.searchConditions[0]", column: "col_a", operator: "=" }],
        },
        displayColumnMode: { wiringKey: "screen.displayColumnMode", mode: "selected" },
      },
    },
  ]);
  const candidates = buildScreenReadQueryWiringCandidates(topology);
  assertEquals(candidates.some((c) => c.wiringKey === "screen.searchConditions[0]"), true);
  assertEquals(candidates.some((c) => c.wiringKey === "screen.displayColumnMode"), true);
});
