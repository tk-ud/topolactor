/**
 * projectionShellHubNavigationRenderProof.test.ts — real DOM rendering proof for
 * ProjectionShell's hub-navigation nav bar (resolveHubNavigationLinks / emission.navigationSequence).
 *
 * PR #602 round 9/10 audit finding: the only prior proof surface
 * (frontend/tests/projectionEntry.test.ts) covered resolveHubNavigationLinks as a pure data-layer
 * unit test, plus a source.includes(...) string grep against ProjectionShell.tsx — neither proves
 * the nav bar actually renders real <a> elements in a real DOM for a real dispatched emission.
 *
 * This file mounts the REAL frontend/islands/ProjectionShell.tsx production component (the same
 * real-mount pattern already established by projectionShellAdminRuntimeWritePayloadCapture.test.ts),
 * feeds it a real-shaped Emission carrying navigationSequence, and asserts the actual rendered DOM:
 *   - a resolvable link renders as a real <a href="?manifest=..."> anchor.
 *   - an unresolvable link (no TargetManifestId) renders as a real <span>, not a clickable anchor.
 *   - when navigationSequence is absent/empty, no <nav data-projection-hub-navigation> element
 *     renders at all — this is the Owner-confirmed intentional design (an isolated/orphan
 *     production projection is a data/authoring-time invariant, not something a frontend fallback
 *     UI element papers over — see .agent/tasks/todo.md admin-surface-topology-seed-conversion),
 *     so this file proves the CURRENT conditional behavior rather than asserting it is a defect.
 */
import { assert, assertEquals, assertExists } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { h, options, render } from "preact";
import { flushUpdates, setupDom } from "./test-dom-setup.ts";
import ProjectionShell from "../islands/ProjectionShell.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

function fakeJwt(): string {
  const header = btoa(JSON.stringify({ alg: "none" }));
  const payload = btoa(JSON.stringify({ realm: "user" }));
  return `${header}.${payload}.sig`;
}

/** EventSource stub — ProjectionShell calls receiver.connect() unconditionally after a
 * successful initial dispatch; happy-dom's Window does not implement EventSource. */
class FakeEventSource {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 2;
  readyState = FakeEventSource.OPEN;
  onmessage: ((e: MessageEvent) => void) | null = null;
  onerror: ((e: Event) => void) | null = null;
  addEventListener(): void {}
  close(): void {}
  constructor(public url: string) {}
}

function buildFetchMock(emissionData: Record<string, unknown>): typeof fetch {
  return ((url: string) => {
    const path = url.toString();
    if (path.startsWith("/api/auth/session") || path === "/api/auth/refresh") {
      return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
    }
    if (path === "/api/dispatch") {
      return Promise.resolve(
        new Response(
          JSON.stringify({ success: true, errors: [], emission: emissionData }),
          { status: 200 },
        ),
      );
    }
    return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
  }) as typeof fetch;
}

async function waitFor(predicate: () => boolean, maxIterations = 40): Promise<void> {
  for (let i = 0; i < maxIterations && !predicate(); i++) {
    await flushUpdates();
  }
}

Deno.test(
  "ProjectionShell (real mount): a real dispatched emission's navigationSequence renders real <a>/<span> hub-navigation DOM elements",
  async () => {
    const { container, cleanup } = setupDom();
    const originalEventSource = (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource = FakeEventSource;
    const originalFetch = globalThis.fetch;

    globalThis.fetch = buildFetchMock({
      manifestId: "00000000-0000-0000-0000-000000000092",
      layoutId: "layout-hub-nav-render-proof",
      layoutNodes: [],
      navigationSequence: [
        {
          hubRelationId: "11111111-1111-1111-1111-111111111111",
          topologyManifestId: "00000000-0000-0000-0000-000000000092",
          relatedHubId: "22222222-2222-2222-2222-222222222222",
          relatedHubLabel: "外部APIクレデンシャル",
          sequencePosition: 1,
          targetManifestId: "00000000-0000-0000-0000-0000000cd008",
        },
        {
          hubRelationId: "33333333-3333-3333-3333-333333333333",
          topologyManifestId: "00000000-0000-0000-0000-000000000092",
          relatedHubId: "44444444-4444-4444-4444-444444444444",
          relatedHubLabel: "未登録の複数候補",
          sequencePosition: 2,
          targetManifestId: null,
        },
      ],
    });

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());

      render(h(ProjectionShell, {}), container);

      let navEl: Element | null = null;
      await waitFor(() => {
        navEl = container.querySelector("[data-projection-hub-navigation]");
        return navEl !== null;
      });
      assertExists(navEl, "the hub-navigation nav bar must have rendered a real DOM element");

      const resolvableLink = container.querySelector("a[data-hub-navigation-resolvable]");
      assertExists(resolvableLink, "a resolvable navigationSequence item must render a real <a> element");
      assertEquals(
        resolvableLink!.getAttribute("href"),
        "?manifest=00000000-0000-0000-0000-0000000cd008",
      );
      assertEquals(resolvableLink!.textContent, "外部APIクレデンシャル");

      const unresolvableSpan = container.querySelector("span[data-hub-navigation-unresolvable]");
      assertExists(
        unresolvableSpan,
        "an unresolvable navigationSequence item (no targetManifestId) must render as a real <span>, not a clickable anchor",
      );
      assertEquals(unresolvableSpan!.textContent, "未登録の複数候補");
    } finally {
      globalThis.fetch = originalFetch;
      (globalThis as unknown as { EventSource: unknown }).EventSource = originalEventSource;
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "ProjectionShell (real mount): a real dispatched emission with no navigationSequence renders no hub-navigation nav bar at all (current Owner-confirmed conditional design)",
  async () => {
    const { container, cleanup } = setupDom();
    const originalEventSource = (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource = FakeEventSource;
    const originalFetch = globalThis.fetch;

    globalThis.fetch = buildFetchMock({
      manifestId: "00000000-0000-0000-0000-000000000001",
      layoutId: "layout-hub-nav-render-proof-orphan",
      layoutNodes: [],
    });

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());

      render(h(ProjectionShell, {}), container);

      // No nav bar to wait for — wait one real dispatch/render cycle instead, keyed on the
      // primary projection island having mounted at all.
      await waitFor(() => container.querySelector("[data-primary-dom-projection]") !== null);
      await flushUpdates();

      const navEl = container.querySelector("[data-projection-hub-navigation]");
      assert(
        navEl === null,
        "no hub-navigation nav bar element should render when the manifest has no active hub_relations",
      );
    } finally {
      globalThis.fetch = originalFetch;
      (globalThis as unknown as { EventSource: unknown }).EventSource = originalEventSource;
      render(null, container);
      cleanup();
    }
  },
);
