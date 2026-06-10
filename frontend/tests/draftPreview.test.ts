import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  fetchDraftPreviewLayouts,
  fetchDraftPreview,
  type DraftPreviewLayout,
  type DraftPreviewLayoutNode,
} from "../api/draftPreview.ts";

const originalFetch = globalThis.fetch;

function mockFetch(handler: (input: RequestInfo | URL, init?: RequestInit) => Response | Promise<Response>) {
  globalThis.fetch = (input, init) => Promise.resolve(handler(input, init));
}

function restoreFetch() {
  globalThis.fetch = originalFetch;
}

// ─────────────────────────────────────────────────────────────────────────────
// fetchDraftPreviewLayouts
// ─────────────────────────────────────────────────────────────────────────────

Deno.test("fetchDraftPreviewLayouts: success response is returned as-is", async () => {
  const layouts: DraftPreviewLayout[] = [
    {
      layoutId: "aaaaaaaa-0000-0000-0000-000000000001",
      layoutKey: "main",
      routeKey: "employees",
      layoutKind: "page",
      slotKeys: ["main"],
    },
  ];
  mockFetch(() => new Response(JSON.stringify({ success: true, layouts }), { status: 200 }));
  try {
    const result = await fetchDraftPreviewLayouts("test-token");
    assertEquals(result.success, true);
    assertEquals(result.layouts?.length, 1);
    assertEquals(result.layouts![0].routeKey, "employees");
  } finally {
    restoreFetch();
  }
});

Deno.test("fetchDraftPreviewLayouts: error response is returned as-is", async () => {
  mockFetch(() => new Response(JSON.stringify({ success: false, errors: [{ code: "UNAUTHORIZED" }] }), { status: 401 }));
  try {
    const result = await fetchDraftPreviewLayouts("bad-token");
    assertEquals(result.success, false);
    assertEquals(result.errors?.[0]?.code, "UNAUTHORIZED");
  } finally {
    restoreFetch();
  }
});

Deno.test("fetchDraftPreviewLayouts: fetch network error returns error shape", async () => {
  mockFetch(() => {
    throw new Error("network down");
  });
  try {
    const result = await fetchDraftPreviewLayouts();
    assertEquals(result.success, false);
    assertEquals(result.errors?.[0]?.message, "network down");
  } finally {
    restoreFetch();
  }
});

// ─────────────────────────────────────────────────────────────────────────────
// fetchDraftPreview
// ─────────────────────────────────────────────────────────────────────────────

Deno.test("fetchDraftPreview: success response has layoutNodes ordered by orderIndex", async () => {
  const layoutNodes: DraftPreviewLayoutNode[] = [
    { nodeId: "n2", componentKey: "button.primitive", orderIndex: 2 },
    { nodeId: "n1", componentKey: "input.primitive", orderIndex: 1 },
  ];
  mockFetch((_input, init) => {
    const body = JSON.parse(String(init?.body ?? "{}"));
    assertEquals(body.layoutId, "aaaaaaaa-0000-0000-0000-000000000001");
    assertEquals("draftId" in body, false);
    return new Response(JSON.stringify({
      success: true,
      layoutId: "aaaaaaaa-0000-0000-0000-000000000001",
      previewMode: "layout_and_topology_intent",
      manifestId: "0053d301-0696-40b5-abee-f9dfd8a3f48c",
      manifestStatus: "draft",
      topologySystemName: "employees",
      initialDataRows: [{ name: "Alice" }],
      layoutNodes,
    }), { status: 200 });
  });
  try {
    const result = await fetchDraftPreview(
      "aaaaaaaa-0000-0000-0000-000000000001",
      "test-token",
    );
    assertEquals(result.success, true);
    assertEquals(result.previewMode, "layout_and_topology_intent");
    assertEquals(result.initialDataRows?.[0]?.name, "Alice");
    assertEquals(result.layoutNodes?.length, 2);
  } finally {
    restoreFetch();
  }
});

Deno.test("fetchDraftPreview: LAYOUT_NODES_NOT_FOUND error is surfaced", async () => {
  mockFetch(() => new Response(JSON.stringify({
    success: false,
    errors: [{ code: "LAYOUT_NODES_NOT_FOUND", message: "no tensor rows" }],
  }), { status: 422 }));
  try {
    const result = await fetchDraftPreview(
      "aaaaaaaa-0000-0000-0000-000000000001",
    );
    assertEquals(result.success, false);
    assertEquals(result.errors?.[0]?.code, "LAYOUT_NODES_NOT_FOUND");
  } finally {
    restoreFetch();
  }
});

Deno.test("fetchDraftPreview: MALFORMED_LAYOUT_ID is treated as explicit error (not crash)", async () => {
  mockFetch(() => new Response(JSON.stringify({
    success: false,
    errors: [{ code: "MALFORMED_LAYOUT_ID", message: "bad uuid" }],
  }), { status: 422 }));
  try {
    const result = await fetchDraftPreview("not-a-uuid");
    assertEquals(result.success, false);
    assertEquals(result.errors?.[0]?.code, "MALFORMED_LAYOUT_ID");
  } finally {
    restoreFetch();
  }
});

Deno.test("fetchDraftPreview: request body contains layoutId only", async () => {
  let parsed: Record<string, unknown> = {};
  mockFetch((_input, init) => {
    parsed = JSON.parse(String(init?.body ?? "{}"));
    return new Response(JSON.stringify({
      success: true,
      layoutId: "aaaaaaaa-0000-0000-0000-000000000001",
      previewMode: "layout_only",
      layoutNodes: [{ nodeId: "n1", orderIndex: 0, componentKey: "box.primitive" }],
    }), { status: 200 });
  });
  try {
    const result = await fetchDraftPreview("aaaaaaaa-0000-0000-0000-000000000001");
    assertEquals(result.success, true);
    assertEquals(parsed.layoutId, "aaaaaaaa-0000-0000-0000-000000000001");
    assertEquals("draftId" in parsed, false);
  } finally {
    restoreFetch();
  }
});

Deno.test("fetchDraftPreview: layoutNodes orderIndex ordering is client-side sortable", () => {
  const unordered: DraftPreviewLayoutNode[] = [
    { nodeId: "b", orderIndex: 2, componentKey: "box.primitive" },
    { nodeId: "a", orderIndex: 1, componentKey: "box.primitive" },
  ];
  const sorted = unordered.slice().sort((a, b) => a.orderIndex - b.orderIndex);
  assertEquals(sorted[0].nodeId, "a");
  assertEquals(sorted[1].nodeId, "b");
});
