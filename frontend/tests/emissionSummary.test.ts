import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { summarizeEmission } from "../runtime/emissionSummary.ts";
import type { Emission } from "../api/dispatch.ts";

Deno.test("summarizeEmission: success path counts components and recommendation", () => {
  const emission: Emission = {
    structureMapId: "sm-1",
    packageId: "pkg-1",
    schemaId: "schema-1",
    componentIds: ["c1", "c2"],
    contextRouteRecommendation: {
      status: "ok",
      statusDetail: "ready",
      nextOperations: [],
      nextTokens: [],
      nextEnumItems: [],
      nearestPrefixSessionIds: [],
      contributingTokens: [],
    },
  };
  const summary = summarizeEmission(emission);
  assertEquals(summary.ok, true);
  assertEquals(summary.componentCount, 2);
  assertEquals(summary.recommendationStatus, "ok");
  assertEquals(summary.recommendationDetail, "ready");
});

Deno.test("summarizeEmission: errors mark not ok", () => {
  const summary = summarizeEmission({
    errors: [{ Code: "AUTH_TOKEN_MISSING", Message: "missing" }],
  });
  assertEquals(summary.ok, false);
  assertEquals(summary.errorMessages.length, 1);
});
