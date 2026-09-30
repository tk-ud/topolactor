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
 *   - a fixed "ホーム" link to /dashboard always renders inside the SAME nav bar, regardless of
 *     whether navigationSequence carries any hub_relations-derived items (Owner-confirmed 2026-08-17:
 *     a dashboard-style surface must be reachable from anywhere, so this is a generic ProjectionShell
 *     affordance — not a per-manifest hub_relations edge, and not team-dashboard-specific; it renders
 *     on every ProjectionShell mount).
 *   - when navigationSequence is absent/empty, the nav bar still renders (for the fixed home link)
 *     but carries no hub-navigation-resolvable/unresolvable elements — superseding the prior
 *     Owner-confirmed conditional-nav design (nav absent entirely with no navigationSequence), see
 *     .agent/tasks/todo.md admin-surface-topology-seed-conversion 2026-08-17 entry for the design
 *     change record.
 */
import { assert, assertEquals, assertExists } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { h, options, render } from "preact";
import { flushUpdates, setupDom } from "./test-dom-setup.ts";
import ProjectionShell from "../islands/ProjectionShell.tsx";
import type { HubNavigationSequenceItem } from "../api/dispatch.ts";
// Manifest 092's REAL Emission.navigationSequence, pinned by the live-DB proof
// backend/tests/Topolactor.Integration.Tests/HubRelationTargetManifestCanonicalMigrationLiveDbTests.cs
// (DispatchAsync_Manifest092_NavigationSequenceResolvesAe200_AndThatTargetDispatchesAe200Projection)
// against the canonically bootstrapped db/seed_empty.sql -- not a hand-written navigation shape.
import manifest0092NavigationSequence from "./fixtures/manifest_0092_navigation_sequence.json" with { type: "json" };

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

function buildFetchMock(
  emissionData: Record<string, unknown>,
  onDispatch?: (body: Record<string, unknown>) => void,
): typeof fetch {
  return ((url: string, init?: RequestInit) => {
    const path = url.toString();
    if (path.startsWith("/api/auth/session") || path === "/api/auth/refresh") {
      return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
    }
    if (path === "/api/dispatch") {
      onDispatch?.(JSON.parse(String(init?.body ?? "{}")) as Record<string, unknown>);
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

      const homeLink = container.querySelector("a[data-projection-home-link]");
      assertExists(homeLink, "the fixed home link must render alongside hub_relations-derived links");
      assertEquals(homeLink!.getAttribute("href"), "/dashboard");
    } finally {
      globalThis.fetch = originalFetch;
      (globalThis as unknown as { EventSource: unknown }).EventSource = originalEventSource;
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "ProjectionShell (real mount): a real dispatched emission with no navigationSequence still renders the fixed home link, with no hub-navigation-derived links",
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

      let navEl: Element | null = null;
      await waitFor(() => {
        navEl = container.querySelector("[data-projection-hub-navigation]");
        return navEl !== null;
      });
      assertExists(navEl, "the nav bar must still render (for the fixed home link) with no navigationSequence");

      const homeLink = container.querySelector("a[data-projection-home-link]");
      assertExists(homeLink, "the fixed home link must render even with no navigationSequence at all");
      assertEquals(homeLink!.getAttribute("href"), "/dashboard");

      assert(
        container.querySelector("[data-hub-navigation-resolvable]") === null &&
          container.querySelector("[data-hub-navigation-unresolvable]") === null,
        "no hub_relations-derived link should render when the manifest has no active hub_relations",
      );
    } finally {
      globalThis.fetch = originalFetch;
      (globalThis as unknown as { EventSource: unknown }).EventSource = originalEventSource;
      render(null, container);
      cleanup();
    }
  },
);

const ADMIN_CREDENTIAL_MANIFEST_ID = "00000000-0000-0000-0000-000000000092";
const ADMIN_ENUM_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae200";

function dispatchedTargetRef(body: Record<string, unknown>): unknown {
  const payload = body.payload as Record<string, unknown> | undefined;
  return payload?.target_ref;
}

Deno.test(
  "ProjectionShell (real mount): Admin Credential (092)'s real navigationSequence renders a clickable Admin Enum (ae200) link, and clicking it lands on a page whose dispatch targets ae200",
  async () => {
    const originalEventSource = (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource = FakeEventSource;
    const originalFetch = globalThis.fetch;
    let navigatedHref = "";

    // Page 1: /?manifest=092 -- the existing ProjectionShell nav bar over 092's own outbound
    // hub_relations (source-scoped, forward-only; no other navigation mechanism involved).
    {
      const { container, cleanup } = setupDom(`http://localhost/?manifest=${ADMIN_CREDENTIAL_MANIFEST_ID}`);
      const dispatched: Record<string, unknown>[] = [];
      globalThis.fetch = buildFetchMock(
        {
          manifestId: ADMIN_CREDENTIAL_MANIFEST_ID,
          layoutId: "layout-092-nav-proof",
          layoutNodes: [],
          navigationSequence: manifest0092NavigationSequence as HubNavigationSequenceItem[],
        },
        (body) => dispatched.push(body),
      );
      try {
        globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
        render(h(ProjectionShell, {}), container);
        await waitFor(() => container.querySelector("a[data-hub-navigation-resolvable]") !== null);

        assertEquals(
          dispatchedTargetRef(dispatched[0]),
          `manifest:${ADMIN_CREDENTIAL_MANIFEST_ID}:projection_entry`,
          "page 1 must be the 092 projection itself",
        );

        const enumLink = container.querySelector(
          `a[data-hub-navigation-resolvable][href="?manifest=${ADMIN_ENUM_MANIFEST_ID}"]`,
        ) as HTMLAnchorElement | null;
        assertExists(enumLink, "092's nav bar must render a real <a> to ae200");
        assertEquals(enumLink!.textContent, "Enum dictionary management");
        // 092's own self-referencing canonical_default_entry row stays a separate link.
        assertExists(
          container.querySelector(`a[data-hub-navigation-resolvable][href="?manifest=${ADMIN_CREDENTIAL_MANIFEST_ID}"]`),
        );

        const win = (globalThis as unknown as { window: { location: { href: string }; MouseEvent: typeof MouseEvent } }).window;
        enumLink!.dispatchEvent(new win.MouseEvent("click", { bubbles: true, cancelable: true }));
        await flushUpdates();
        navigatedHref = win.location.href;
      } finally {
        globalThis.fetch = originalFetch;
        render(null, container);
        cleanup();
      }
    }

    assertEquals(navigatedHref, `http://localhost/?manifest=${ADMIN_ENUM_MANIFEST_ID}`, "the click must navigate to ?manifest=<ae200>");

    // Page 2: the URL the click navigated to. The same ProjectionShell entry path must dispatch ae200.
    {
      const { container, cleanup } = setupDom(navigatedHref);
      const dispatched: Record<string, unknown>[] = [];
      globalThis.fetch = buildFetchMock(
        { manifestId: ADMIN_ENUM_MANIFEST_ID, layoutId: "layout-ae200-nav-proof", layoutNodes: [] },
        (body) => dispatched.push(body),
      );
      try {
        globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
        render(h(ProjectionShell, {}), container);
        await waitFor(() => dispatched.length > 0 && container.querySelector("[data-projection-hub-navigation]") !== null);
        assertEquals(dispatchedTargetRef(dispatched[0]), `manifest:${ADMIN_ENUM_MANIFEST_ID}:projection_entry`);
      } finally {
        globalThis.fetch = originalFetch;
        (globalThis as unknown as { EventSource: unknown }).EventSource = originalEventSource;
        render(null, container);
        cleanup();
      }
    }
  },
);
