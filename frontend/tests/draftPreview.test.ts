import { assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  fetchDraftPreviewLayouts,
  fetchDraftPreviewDrafts,
  fetchDraftPreview,
  type DraftPreviewLayout,
  type DraftPreviewDraft,
  type DraftPreviewLayoutNode,
} from "../api/draftPreview.ts";

// ---------------------------------------------------------------------------
// Unit tests for draftPreview API client (fetch mocking via globalThis.fetch)
// ---------------------------------------------------------------------------

function mockFetch(response: unknown, status = 200) {
  const original = globalThis.fetch;
  (globalThis as unknown as Record<string, unknown>).fetch = (_url: unknown, _init?: unknown) =>
    Promise.resolve({
      ok: status >= 200 && status < 300,
      status,
      json: () => Promise.resolve(response),
    } as Response);
  return () => {
    (globalThis as unknown as Record<string, unknown>).fetch = original;
  };
}

// ---------------------------------------------------------------------------
// fetchDraftPreviewLayouts
// ---------------------------------------------------------------------------

Deno.test("fetchDraftPreviewLayouts: success response is returned as-is", async () => {
  const layouts: DraftPreviewLayout[] = [
    {
      layoutId: "aaaaaaaa-0000-0000-0000-000000000001",
      layoutKey: "/admin/ui-builder:panel.main:layout",
      routeKey: "/admin/ui-builder",
      layoutKind: "panel/main",
      slotKeys: ["header", "body"],
    },
  ];
  const restore = mockFetch({ success: true, layouts });
  try {
    const result = await fetchDraftPreviewLayouts("test-token");
    assertEquals(result.success, true);
    assertExists(result.layouts);
    assertEquals(result.layouts!.length, 1);
    assertEquals(result.layouts![0].layoutId, "aaaaaaaa-0000-0000-0000-000000000001");
    assertEquals(result.layouts![0].slotKeys, ["header", "body"]);
  } finally {
    restore();
  }
});

Deno.test("fetchDraftPreviewLayouts: error response is returned as-is", async () => {
  const restore = mockFetch({ success: false, errors: [{ code: "UNAUTHORIZED", message: "JWT invalid" }] }, 401);
  try {
    const result = await fetchDraftPreviewLayouts("bad-token");
    assertEquals(result.success, false);
    assertExists(result.errors);
    assertEquals(result.errors![0].code, "UNAUTHORIZED");
  } finally {
    restore();
  }
});

Deno.test("fetchDraftPreviewLayouts: fetch network error returns error shape", async () => {
  const original = globalThis.fetch;
  (globalThis as unknown as Record<string, unknown>).fetch = () => Promise.reject(new Error("network fail"));
  try {
    const result = await fetchDraftPreviewLayouts();
    assertEquals(result.success, false);
    assertExists(result.errors);
    assertEquals(result.errors![0].message, "network fail");
  } finally {
    (globalThis as unknown as Record<string, unknown>).fetch = original;
  }
});

// ---------------------------------------------------------------------------
// fetchDraftPreviewDrafts
// ---------------------------------------------------------------------------

Deno.test("fetchDraftPreviewDrafts: success response returns draft list", async () => {
  const drafts: DraftPreviewDraft[] = [
    {
      draftId: "bbbbbbbb-0000-0000-0000-000000000001",
      label: "Test Draft",
      hubId: "cccccccc-0000-0000-0000-000000000001",
      status: "draft",
      createdAt: "2026-06-01T00:00:00Z",
    },
  ];
  const restore = mockFetch({ success: true, drafts });
  try {
    const result = await fetchDraftPreviewDrafts("test-token");
    assertEquals(result.success, true);
    assertExists(result.drafts);
    assertEquals(result.drafts!.length, 1);
    assertEquals(result.drafts![0].draftId, "bbbbbbbb-0000-0000-0000-000000000001");
    assertEquals(result.drafts![0].label, "Test Draft");
  } finally {
    restore();
  }
});

Deno.test("fetchDraftPreviewDrafts: empty drafts list is success", async () => {
  const restore = mockFetch({ success: true, drafts: [] });
  try {
    const result = await fetchDraftPreviewDrafts();
    assertEquals(result.success, true);
    assertEquals(result.drafts, []);
  } finally {
    restore();
  }
});

// ---------------------------------------------------------------------------
// fetchDraftPreview
// ---------------------------------------------------------------------------

Deno.test("fetchDraftPreview: success response has layoutNodes ordered by orderIndex", async () => {
  const layoutNodes: DraftPreviewLayoutNode[] = [
    { slotKey: "slot_b", orderIndex: 0 },
    { slotKey: "slot_a", orderIndex: 1 },
  ];
  const restore = mockFetch({
    success: true,
    layoutId: "aaaaaaaa-0000-0000-0000-000000000001",
    draftId: "bbbbbbbb-0000-0000-0000-000000000001",
    layoutNodes,
    draftEntityJson: { label: "entity", type: "node" },
    draftStatus: "draft",
  });
  try {
    const result = await fetchDraftPreview(
      "aaaaaaaa-0000-0000-0000-000000000001",
      "bbbbbbbb-0000-0000-0000-000000000001",
      "test-token",
    );
    assertEquals(result.success, true);
    assertExists(result.layoutNodes);
    assertEquals(result.layoutNodes!.length, 2);
    // slot_b has orderIndex=0 → first in response
    assertEquals(result.layoutNodes![0].slotKey, "slot_b");
    assertEquals(result.layoutNodes![0].orderIndex, 0);
    // slot_a has orderIndex=1 → second in response
    assertEquals(result.layoutNodes![1].slotKey, "slot_a");
    assertEquals(result.layoutNodes![1].orderIndex, 1);
    assertExists(result.draftEntityJson);
    assertEquals((result.draftEntityJson as Record<string, unknown>)["label"], "entity");
  } finally {
    restore();
  }
});

Deno.test("fetchDraftPreview: LAYOUT_NODES_NOT_FOUND error is surfaced", async () => {
  const restore = mockFetch({
    success: false,
    errors: [{
      code: "LAYOUT_NODES_NOT_FOUND",
      message: "layout_id has no tensor rows in ui_topology_tensor. Broken layout configuration — no fallback.",
    }],
  }, 422);
  try {
    const result = await fetchDraftPreview(
      "aaaaaaaa-0000-0000-0000-000000000001",
      "bbbbbbbb-0000-0000-0000-000000000001",
    );
    assertEquals(result.success, false);
    assertExists(result.errors);
    assertEquals(result.errors![0].code, "LAYOUT_NODES_NOT_FOUND");
  } finally {
    restore();
  }
});

Deno.test("fetchDraftPreview: DRAFT_NOT_FOUND error is surfaced", async () => {
  const restore = mockFetch({
    success: false,
    errors: [{ code: "DRAFT_NOT_FOUND", message: "draft_id not found in content_entity_drafts." }],
  }, 404);
  try {
    const result = await fetchDraftPreview(
      "aaaaaaaa-0000-0000-0000-000000000001",
      "dddddddd-0000-0000-0000-000000000099",
    );
    assertEquals(result.success, false);
    assertExists(result.errors);
    assertEquals(result.errors![0].code, "DRAFT_NOT_FOUND");
  } finally {
    restore();
  }
});

Deno.test("fetchDraftPreview: MALFORMED_LAYOUT_ID is treated as explicit error (not crash)", async () => {
  const restore = mockFetch({
    success: false,
    errors: [{ code: "MALFORMED_LAYOUT_ID", message: "layoutId must be a non-empty valid UUID." }],
  }, 422);
  try {
    const result = await fetchDraftPreview("not-a-uuid", "bbbbbbbb-0000-0000-0000-000000000001");
    assertEquals(result.success, false);
    assertEquals(result.errors![0].code, "MALFORMED_LAYOUT_ID");
  } finally {
    restore();
  }
});

Deno.test("fetchDraftPreview: layoutNodes orderIndex ordering is client-side sortable", () => {
  // Verifies that a consumer sorting layoutNodes by orderIndex gets the correct ordering
  // when backend returns nodes in arbitrary order.
  const unordered: DraftPreviewLayoutNode[] = [
    { slotKey: "slot_a", orderIndex: 1 },
    { slotKey: "slot_b", orderIndex: 0 },
    { slotKey: "slot_c", orderIndex: 2 },
  ];
  const sorted = [...unordered].sort((a, b) => a.orderIndex - b.orderIndex);
  assertEquals(sorted[0].slotKey, "slot_b");
  assertEquals(sorted[1].slotKey, "slot_a");
  assertEquals(sorted[2].slotKey, "slot_c");
});
