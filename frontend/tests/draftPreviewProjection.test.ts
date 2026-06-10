import { assertEquals, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildDesignDraftMapFromPreview,
  draftPreviewNodesToAuditInputs,
  parseDraftPreviewQueryParams,
} from "../runtime/draftPreviewProjection.ts";

Deno.test("parseDraftPreviewQueryParams: reads layoutId from search", () => {
  const q = parseDraftPreviewQueryParams(
    "?layoutId=aaaaaaaa-0000-0000-0000-000000000001&draftId=ignored-legacy",
  );
  assertEquals(q.layoutId, "aaaaaaaa-0000-0000-0000-000000000001");
});

Deno.test("buildDesignDraftMapFromPreview: maps cssTokenRefs and inlineText", () => {
  const map = buildDesignDraftMapFromPreview({
    "node-1": {
      inlineText: "Hello",
      cssTokenRefs: ["color.primary"],
      linkHref: "/x",
    },
  });
  assertEquals(map.get("node-1")?.inlineText, "Hello");
  assertEquals(map.get("node-1")?.cssTokenRefs, ["color.primary"]);
  assertEquals(map.get("node-1")?.linkHref, "/x");
});

Deno.test("draftPreviewNodesToAuditInputs: preserves layoutClassRefs and design inlineText", () => {
  const nodes = draftPreviewNodesToAuditInputs([
    {
      nodeId: "n1",
      componentKey: "card.primitive",
      orderIndex: 0,
      layoutClassRefs: ["layout.card"],
      design: { inlineText: "Title", cssTokenRefs: ["color.primary"] },
    },
  ]);
  assertEquals(nodes.length, 1);
  assertEquals(nodes[0].layoutClassRefs, ["layout.card"]);
  assertEquals(nodes[0].inlineText, "Title");
});

Deno.test("draftPreviewNodesToAuditInputs: preset widthMode avoids 160x72 audit default", () => {
  const nodes = draftPreviewNodesToAuditInputs([
    {
      nodeId: "n-input",
      componentKey: "input.primitive",
      orderIndex: 1,
      widthMode: "preset",
      heightMode: "auto",
      layoutClassRefs: ["layout.width.full"],
    },
  ]);
  assertEquals(nodes[0].widthMode, "preset");
  assertEquals(nodes[0].width, "auto");
  assertEquals(nodes[0].layoutClassRefs, ["layout.width.full"]);
});

Deno.test("draftPreviewProjection: demo handoff link includes layoutId query", () => {
  const layoutId = "aaaaaaaa-0000-0000-0000-000000000001";
  const href = `/demo?layoutId=${encodeURIComponent(layoutId)}`;
  assertStringIncludes(href, "layoutId=aaaaaaaa");
});
