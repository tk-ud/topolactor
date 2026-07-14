// teamDashboardRoleSurfaceRealRender.test.ts
//
// Real component-render proof (live DOM via happy-dom, not a source-string check) that
// TeamDashboardRoleSurface actually mounts TeamMarkdownAuthoring only for admin-role sessions and
// never for Normal-role sessions. frontend/tests/roleBasedSurfaceSeparation.test.ts's existing test
// of the same invariant reads TeamDashboardRoleSurface.tsx's source text for the string
// "isAdmin && <TeamMarkdownAuthoring" — real, but insufficient on its own per the review: it proves
// the JSX expression exists, not that it actually renders (or doesn't render) the component. This
// file exercises the real render path: useCurrentSession's effect resolves a role from a decoded
// JWT + a mocked /api/auth/session probe, and we assert on the resulting DOM.

import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import { SESSION_TOKEN_KEY } from "../lib/demoSession.ts";
import TeamDashboardRoleSurface from "../islands/TeamDashboardRoleSurface.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

function base64Url(json: Record<string, unknown>): string {
  const raw = btoa(JSON.stringify(json));
  return raw.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

/** Unsigned test JWT — getTokenClaims only base64-decodes the payload, no signature check client-side. */
function buildClientToken(sub: string, role: string): string {
  return `${base64Url({ alg: "none" })}.${base64Url({ sub, role })}.sig`;
}

const originalFetch = globalThis.fetch;

function mockSessionProbe(role: string, sub: string): typeof globalThis.fetch {
  return (input: string | URL | Request) => {
    const url = typeof input === "string" ? input : input instanceof Request ? input.url : input.href;
    if (url.startsWith("/api/auth/session")) {
      return Promise.resolve(
        new Response(JSON.stringify({ success: true, subject: sub, role }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );
    }
    // TeamMarkdownViewer/Authoring both fire their own search-on-mount dispatch/viewer calls;
    // resolve them to an empty result so the effect chain settles without throwing.
    if (url === "/api/dispatch") {
      return Promise.resolve(
        new Response(JSON.stringify({ success: true, emission: { data: { savedViews: [] } } }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );
    }
    if (url.startsWith("/api/team-markdown/saved-views")) {
      return Promise.resolve(
        new Response(JSON.stringify({ success: true, savedViews: [] }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );
    }
    return Promise.resolve(new Response(JSON.stringify({ success: false }), { status: 404 }));
  };
}

Deno.test("TeamDashboardRoleSurface real render: Normal-role session sees the viewer but never mounts TeamMarkdownAuthoring", async () => {
  const { container, cleanup } = setupDom();
  try {
    sessionStorage.setItem(SESSION_TOKEN_KEY, buildClientToken("normal_user", "user"));
    globalThis.fetch = mockSessionProbe("user", "normal_user");

    render(h(TeamDashboardRoleSurface, {}), container);
    await flushUpdates();
    await flushUpdates();

    const html = container.innerHTML;
    assert(html.includes("Team Markdown Dashboard"), "viewer heading must render for a Normal-role session");
    assertEquals(
      html.includes("Team Markdown Authoring"),
      false,
      "the real DOM must never contain the authoring surface for a Normal-role session, not just 'the source has a gate for it'",
    );
    assertEquals(html.includes("Authoring — admin only"), false);
  } finally {
    render(null, container);
    cleanup();
    globalThis.fetch = originalFetch;
  }
});

Deno.test("TeamDashboardRoleSurface real render: admin-role session actually mounts TeamMarkdownAuthoring", async () => {
  const { container, cleanup } = setupDom();
  try {
    sessionStorage.setItem(SESSION_TOKEN_KEY, buildClientToken("demo_admin", "admin"));
    globalThis.fetch = mockSessionProbe("admin", "demo_admin");

    render(h(TeamDashboardRoleSurface, {}), container);
    await flushUpdates();
    await flushUpdates();

    const html = container.innerHTML;
    assert(html.includes("Team Markdown Dashboard"), "viewer heading must also render for an admin-role session");
    assert(html.includes("Team Markdown Authoring"), "the real DOM must contain the authoring surface for an admin-role session");
  } finally {
    render(null, container);
    cleanup();
    globalThis.fetch = originalFetch;
  }
});
