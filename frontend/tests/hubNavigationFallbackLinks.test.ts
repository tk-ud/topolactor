// hubNavigationFallbackLinks.test.ts
//
// Real component-render proof (not source-string checks) for the backend relation/hub-relation
// link-list fallback: NormalDashboardHome's RelatedHubLinksPanel actually renders the links GET
// /api/hub-navigation/relations returns, shows an explicit empty state when the backend returns
// links:[], and surfaces (not swallows) a backend error — using a live DOM (happy-dom) so the
// useEffect fetch -> setState -> re-render cycle actually runs, not an SSR string snapshot.

import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options } from "preact";
import { render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import { RelatedHubLinksPanel } from "../islands/NormalDashboardHome.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

function mockFetchOnce(status: number, body: unknown): typeof globalThis.fetch {
  return () =>
    Promise.resolve(
      new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } }),
    );
}

const originalFetch = globalThis.fetch;

Deno.test("RelatedHubLinksPanel: renders returned links from GET /api/hub-navigation/relations", async () => {
  globalThis.fetch = mockFetchOnce(200, {
    success: true,
    links: [
      { relatedHubId: "hub-1", relatedHubLabel: "Fixture Hub One", sequencePosition: 1, targetManifestId: "m-1" },
      { relatedHubId: "hub-2", relatedHubLabel: "Fixture Hub Two", sequencePosition: 2, targetManifestId: null },
    ],
  });
  const { container, cleanup } = setupDom();
  try {
    render(h(RelatedHubLinksPanel, { token: "tok" }), container);
    await flushUpdates();
    const html = container.innerHTML;
    assert(html.includes("Fixture Hub One"), "first hub relation label must render");
    assert(html.includes("Fixture Hub Two"), "second hub relation label must render");
    assert(html.includes("直接遷移不可"), "hub relation with no resolvable targetManifestId must be marked non-navigable");
  } finally {
    render(null, container);
    cleanup();
    globalThis.fetch = originalFetch;
  }
});

Deno.test("RelatedHubLinksPanel: backend links:[] renders an explicit empty state, not a blank/hidden panel", async () => {
  globalThis.fetch = mockFetchOnce(200, { success: true, links: [] });
  const { container, cleanup } = setupDom();
  try {
    render(h(RelatedHubLinksPanel, { token: "tok" }), container);
    await flushUpdates();
    const html = container.innerHTML;
    assert(html.includes("現在、遷移可能な関連ハブはありません"), "explicit empty state text must render when links is []");
    assertEquals(html.includes("読み込み中"), false, "loading indicator must not still be showing once resolved");
  } finally {
    render(null, container);
    cleanup();
    globalThis.fetch = originalFetch;
  }
});

Deno.test("RelatedHubLinksPanel: backend error response is surfaced, not silently swallowed", async () => {
  globalThis.fetch = mockFetchOnce(401, {
    success: false,
    links: [],
    errors: [{ code: "AUTH_SESSION_REVOKED", message: "Session has been revoked." }],
  });
  const { container, cleanup } = setupDom();
  try {
    render(h(RelatedHubLinksPanel, { token: "tok" }), container);
    await flushUpdates();
    const html = container.innerHTML;
    assert(html.includes("Session has been revoked."), "backend error message must be surfaced to the user");
  } finally {
    render(null, container);
    cleanup();
    globalThis.fetch = originalFetch;
  }
});
