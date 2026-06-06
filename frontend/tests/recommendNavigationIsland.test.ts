import { assert, assertEquals, assertStringIncludes } from "https://deno.land/std@0.224.0/assert/mod.ts";
import type { RecommendNavigationProjectionSpec } from "../api/dispatch.ts";

Deno.test("RecommendNavigationIsland contract: ProjectionShell renders recommend child island under main projection island", async () => {
  const shell = await Deno.readTextFile(new URL("../islands/ProjectionShell.tsx", import.meta.url));
  assertStringIncludes(shell, "RecommendNavigationIsland");
  assertStringIncludes(shell, 'data-projection-island="main_projection_island"');
  assertStringIncludes(shell, "data-primary-dom-projection");
  assertStringIncludes(shell, "emission?.recommendNavigationProjection");
});

Deno.test("RecommendNavigationIsland contract: hub-local lanes are distinct and SQL Attention is not mixed", async () => {
  const hubBlock = await Deno.readTextFile(new URL("../components/HubLocalRecommendationBlock.tsx", import.meta.url));
  const sqlBlock = await Deno.readTextFile(new URL("../components/SqlAttentionProjectionBlock.tsx", import.meta.url));
  assertStringIncludes(hubBlock, 'section.lane === "ui_pressure"');
  assertStringIncludes(hubBlock, 'section.lane === "state_pressure"');
  assert(!hubBlock.includes("sql_attention_projection"), "hub-local block must not render SQL Attention lane candidates");
  assertStringIncludes(sqlBlock, 'data-recommend-lane="sql_attention_projection"');
  assertStringIncludes(sqlBlock, "explicit unavailable");
  assertStringIncludes(sqlBlock, "!spec.sourceSetId");
});

Deno.test("RecommendNavigationProjectionSpec type carries backend-resolved child island and sourceSetId unavailable state", () => {
  const spec: RecommendNavigationProjectionSpec = {
    mainProjectionIslandId: "main_projection_island",
    childIslandId: "recommend_navigation_child_island",
    hubLocalSections: [
      {
        lane: "ui_pressure",
        candidateKind: "next_operation",
        title: "UI / operation pressure",
        status: "ok",
        candidates: [{ value: "Search", score: 0.9, evidence: ["transition"], lane: "ui_pressure" }],
      },
      {
        lane: "state_pressure",
        candidateKind: "next_enum_item",
        title: "State / enum pressure",
        status: "ok",
        candidates: [{ value: "Approved", score: 0.7, evidence: ["enum"], lane: "state_pressure" }],
      },
    ],
    sqlAttentionProjection: {
      lane: "sql_attention_projection",
      candidateKind: "next_hub_projection_candidate",
      status: "explicit_unavailable",
      statusDetail: "sourceSetId unresolved",
      sourceSetId: null,
    },
  };

  assertEquals(spec.hubLocalSections.map((section) => section.lane), ["ui_pressure", "state_pressure"]);
  assertEquals(spec.sqlAttentionProjection.status, "explicit_unavailable");
});
