import {
  assertEquals,
  assertFalse,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  summarizeEmission,
  toUserFacingResult,
} from "../runtime/emissionSummary.ts";
import { demoPreviewOptions, presetsForGroups } from "../runtime/operationPresets.ts";
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

Deno.test("toUserFacingResult: explicit_error recommendation does not expose raw status", () => {
  const emission: Emission = {
    componentIds: ["c1"],
    contextRouteRecommendation: {
      status: "explicit_error",
      statusDetail: "internal error detail",
      nextOperations: [],
      nextTokens: [],
      nearestPrefixSessionIds: [],
      contributingTokens: [],
    },
  };
  const result = toUserFacingResult(summarizeEmission(emission));
  // emission itself succeeded — no error status
  assertEquals(result.status, "success");
  // raw recommendation status/detail not surfaced
  assertFalse(result.hasRecommendation);
  assertEquals(result.recommendationSummary, undefined);
});

// ─── toUserFacingResult: error paths — raw code suppression ──────────────────

Deno.test("toUserFacingResult: AUTH_TOKEN_MISSING maps to login headline without raw code", () => {
  const emission: Emission = {
    errors: [{ Code: "AUTH_TOKEN_MISSING", Message: "missing" }],
  };
  const result = toUserFacingResult(summarizeEmission(emission));
  assertEquals(result.status, "error");
  assertEquals(result.headline, "ログインが必要です");
  assertEquals(result.itemCount, 0);
  assertFalse(result.hasRecommendation);
  // detail must not expose raw backend code
  assertFalse(
    (result.detail ?? "").includes("AUTH_TOKEN_MISSING"),
    "raw backend code must not appear in user-facing detail",
  );
  assertFalse(
    (result.detail ?? "").includes("[AUTH_TOKEN_MISSING]"),
    "bracketed raw code must not appear in user-facing detail",
  );
});

Deno.test("toUserFacingResult: generic error detail does not contain raw code", () => {
  const emission: Emission = {
    errors: [{ Code: "ATTRACTOR_RESOLVE_FAILED", Message: "not found" }],
  };
  const result = toUserFacingResult(summarizeEmission(emission));
  assertEquals(result.status, "error");
  assertEquals(result.headline, "エラーが発生しました");
  assertFalse(
    (result.detail ?? "").includes("ATTRACTOR_RESOLVE_FAILED"),
    "raw backend code must not appear in user-facing detail",
  );
});

Deno.test("toUserFacingResult: multiple errors produce user-facing detail without raw codes", () => {
  const emission: Emission = {
    errors: [
      { Code: "ERR_A", Message: "first" },
      { Code: "ERR_B", Message: "second" },
    ],
  };
  const result = toUserFacingResult(summarizeEmission(emission));
  assertEquals(result.status, "error");
  assertEquals(typeof result.detail, "string");
  // Must not contain raw error codes
  assertFalse((result.detail ?? "").includes("ERR_A"));
  assertFalse((result.detail ?? "").includes("ERR_B"));
  assertFalse((result.detail ?? "").includes("["));
});

// ─── demo preview options: derived from operationPresets, not island-defined ──

Deno.test("demoPreviewOptions: returns options for all demo group presets", () => {
  const demoPresets = presetsForGroups(["demo"]);
  const options = demoPreviewOptions();
  // every demo preset with a previewLabel must appear
  const optionIds = new Set(options.map((o) => o.id));
  for (const p of demoPresets) {
    if (p.previewLabel !== undefined) {
      assertEquals(optionIds.has(p.id), true, `preset ${p.id} must appear in demoPreviewOptions`);
    }
  }
});

Deno.test("demoPreviewOptions: all options have non-empty previewLabel and previewDescription", () => {
  for (const opt of demoPreviewOptions()) {
    assertFalse(opt.previewLabel === "", `previewLabel must not be empty for ${opt.id}`);
    assertFalse(opt.previewDescription === "", `previewDescription must not be empty for ${opt.id}`);
  }
});

Deno.test("demoPreviewOptions: user-facing labels contain no construction authority vocabulary", () => {
  const constructionVocab = ["builder", "construct", "manifest edit", "topology apply", "admin edit", "作成する", "構築する", "編集する", "登録する"];
  for (const opt of demoPreviewOptions()) {
    for (const term of constructionVocab) {
      assertFalse(
        opt.previewLabel.includes(term),
        `previewLabel "${opt.previewLabel}" must not contain construction vocabulary: "${term}"`,
      );
      assertFalse(
        opt.previewDescription.includes(term),
        `previewDescription "${opt.previewDescription}" must not contain construction vocabulary: "${term}"`,
      );
    }
  }
});

Deno.test("demoPreviewOptions: all scenario IDs are distinct", () => {
  const ids = demoPreviewOptions().map((o) => o.id);
  const unique = new Set(ids);
  assertEquals(unique.size, ids.length);
});

Deno.test("demoPreviewOptions: scenario IDs are operationPresets demo group IDs", () => {
  const demoPresetIds = new Set(presetsForGroups(["demo"]).map((p) => p.id));
  for (const opt of demoPreviewOptions()) {
    assertEquals(demoPresetIds.has(opt.id), true, `option ${opt.id} must be a demo group preset`);
  }
});
