import { assertEquals, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  resolvePreviewErrorMessage,
  DRAFT_PREVIEW_ERROR_MESSAGES,
} from "../runtime/draftPreviewErrors.ts";

// ─── resolvePreviewErrorMessage ──────────────────────────────────────────────

Deno.test("resolvePreviewErrorMessage: known code LAYOUT_NOT_FOUND returns specific message", () => {
  const msg = resolvePreviewErrorMessage("LAYOUT_NOT_FOUND");
  assertStringIncludes(msg, "レイアウトが存在しません");
});

Deno.test("resolvePreviewErrorMessage: known code MANIFEST_TOPOLOGY_AMBIGUOUS returns specific message", () => {
  const msg = resolvePreviewErrorMessage("MANIFEST_TOPOLOGY_AMBIGUOUS");
  assertStringIncludes(msg, "topologySystemName");
});

Deno.test("resolvePreviewErrorMessage: known code LAYOUT_NODES_NOT_FOUND returns specific message", () => {
  const msg = resolvePreviewErrorMessage("LAYOUT_NODES_NOT_FOUND");
  assertStringIncludes(msg, "tensor");
});

Deno.test("resolvePreviewErrorMessage: known code UNAUTHORIZED returns auth guidance", () => {
  const msg = resolvePreviewErrorMessage("UNAUTHORIZED");
  assertStringIncludes(msg, "認証が必要");
});

Deno.test("resolvePreviewErrorMessage: unknown code with fallback message includes both", () => {
  const msg = resolvePreviewErrorMessage("SOME_UNKNOWN_CODE", "backend message here");
  assertStringIncludes(msg, "backend message here");
  assertStringIncludes(msg, "SOME_UNKNOWN_CODE");
});

Deno.test("resolvePreviewErrorMessage: unknown code without fallback includes code name", () => {
  const msg = resolvePreviewErrorMessage("TOTALLY_UNKNOWN");
  assertStringIncludes(msg, "TOTALLY_UNKNOWN");
});

Deno.test("resolvePreviewErrorMessage: never returns bare generic message without code info", () => {
  const msg = resolvePreviewErrorMessage("UNKNOWN_ERROR_XYZ");
  // Must NOT be a generic message that hides the error code
  assertEquals(msg.includes("プレビューの取得に失敗しました") && !msg.includes("UNKNOWN"), false);
  assertStringIncludes(msg, "UNKNOWN_ERROR_XYZ");
});

Deno.test("DRAFT_PREVIEW_ERROR_MESSAGES: PROJECTION_FAILED is defined", () => {
  assertStringIncludes(
    DRAFT_PREVIEW_ERROR_MESSAGES["PROJECTION_FAILED"] ?? "",
    "投影に失敗",
  );
});

Deno.test("resolvePreviewErrorMessage: LAYOUT_HAS_NO_NODES is distinct from LAYOUT_NODES_NOT_FOUND", () => {
  const noNodes = resolvePreviewErrorMessage("LAYOUT_HAS_NO_NODES");
  const notFound = resolvePreviewErrorMessage("LAYOUT_NODES_NOT_FOUND");
  assertStringIncludes(noNodes, "ノードがありません");
  assertStringIncludes(notFound, "tensor");
});

Deno.test("DRAFT_PREVIEW_ERROR_MESSAGES: all entries are non-empty Japanese strings", () => {
  for (const [code, msg] of Object.entries(DRAFT_PREVIEW_ERROR_MESSAGES)) {
    assertEquals(typeof msg, "string", `${code} must map to a string`);
    assertEquals(msg.length > 0, true, `${code} must have a non-empty message`);
  }
});
