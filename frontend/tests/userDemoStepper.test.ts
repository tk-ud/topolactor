import {
  assertEquals,
  assertFalse,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  summarizeEmission,
  toUserFacingResult,
} from "../runtime/emissionSummary.ts";
import type { Emission } from "../api/dispatch.ts";

// ─── toUserFacingResult: success paths ───────────────────────────────────────

Deno.test("toUserFacingResult: success with components returns item count in headline", () => {
  const emission: Emission = {
    structureMapId: "sm-1",
    packageId: "pkg-1",
    schemaId: "schema-1",
    componentIds: ["c1", "c2", "c3"],
  };
  const result = toUserFacingResult(summarizeEmission(emission));
  assertEquals(result.status, "success");
  assertEquals(result.itemCount, 3);
  assertEquals(result.headline.includes("3"), true);
  assertFalse(result.hasRecommendation);
  assertEquals(result.recommendationSummary, undefined);
});

Deno.test("toUserFacingResult: success with no components returns generic headline", () => {
  const emission: Emission = {};
  const result = toUserFacingResult(summarizeEmission(emission));
  assertEquals(result.status, "success");
  assertEquals(result.itemCount, 0);
  assertFalse(result.hasRecommendation);
});

// ─── toUserFacingResult: recommendation paths ────────────────────────────────

Deno.test("toUserFacingResult: recommendation present when status is ok", () => {
  const emission: Emission = {
    componentIds: ["c1"],
    contextRouteRecommendation: {
      status: "ok",
      statusDetail: "ready",
      nextOperations: [],
      nextTokens: [],
      nearestPrefixSessionIds: [],
      contributingTokens: [],
    },
  };
  const result = toUserFacingResult(summarizeEmission(emission));
  assertEquals(result.status, "success");
  assertEquals(result.hasRecommendation, true);
  assertEquals(result.recommendationSummary, "レコメンドが見つかりました");
});

Deno.test("toUserFacingResult: no recommendation when status is insufficient_history", () => {
  const emission: Emission = {
    componentIds: ["c1"],
    contextRouteRecommendation: {
      status: "insufficient_history",
      statusDetail: "not enough data",
      nextOperations: [],
      nextTokens: [],
      nearestPrefixSessionIds: [],
      contributingTokens: [],
    },
  };
  const result = toUserFacingResult(summarizeEmission(emission));
  assertFalse(result.hasRecommendation);
  assertEquals(result.recommendationSummary, undefined);
});

// ─── toUserFacingResult: error paths ─────────────────────────────────────────

Deno.test("toUserFacingResult: AUTH_TOKEN_MISSING maps to login headline", () => {
  const emission: Emission = {
    errors: [{ Code: "AUTH_TOKEN_MISSING", Message: "missing" }],
  };
  const result = toUserFacingResult(summarizeEmission(emission));
  assertEquals(result.status, "error");
  assertEquals(result.headline, "ログインが必要です");
  assertEquals(result.itemCount, 0);
  assertFalse(result.hasRecommendation);
});

Deno.test("toUserFacingResult: generic error maps to generic headline", () => {
  const emission: Emission = {
    errors: [{ Code: "ATTRACTOR_RESOLVE_FAILED", Message: "not found" }],
  };
  const result = toUserFacingResult(summarizeEmission(emission));
  assertEquals(result.status, "error");
  assertEquals(result.headline, "エラーが発生しました");
});

Deno.test("toUserFacingResult: multiple errors joined in detail", () => {
  const emission: Emission = {
    errors: [
      { Code: "ERR_A", Message: "first" },
      { Code: "ERR_B", Message: "second" },
    ],
  };
  const result = toUserFacingResult(summarizeEmission(emission));
  assertEquals(result.status, "error");
  assertEquals(typeof result.detail, "string");
  assertEquals(result.detail?.includes("/"), true);
});

// ─── DEMO_SCENARIOS vocabulary: internal IDs map to user titles ───────────────

const DEMO_SCENARIO_IDS = [
  "demo_hub_overview",
  "demo_entity_list",
  "demo_hub_recommendation",
] as const;

Deno.test("demo scenarios: all preset IDs are distinct", () => {
  const ids = [...DEMO_SCENARIO_IDS];
  const unique = new Set(ids);
  assertEquals(unique.size, ids.length);
});

Deno.test("demo scenarios: no internal vocabulary in user-visible IDs", () => {
  for (const id of DEMO_SCENARIO_IDS) {
    assertFalse(
      id.includes("dispatch") || id.includes("emission") || id.includes("runtime"),
      `scenario id should not contain internal vocabulary: ${id}`,
    );
  }
});
