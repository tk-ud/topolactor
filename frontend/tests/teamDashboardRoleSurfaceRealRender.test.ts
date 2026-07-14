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

// ─── full route-composition chain: mount -> search -> expand -> operate -> real dispatch ─────────
//
// Proves the route-composition component contract end-to-end (not by rendering SavedViewOperationPanel
// in isolation, as savedViewOperationPanel.test.ts does): TeamDashboardRoleSurface -> TeamMarkdownAuthoring
// -> a real search result card -> MdViewer's Refresh action -> SavedViewOperationPanel -> a real
// confirmed dispatch, all through the actual mounted composition. The backend independently
// re-verifies JWT role authority on every team_markdown mutation (AdminRuntime.TeamMarkdown
// ExecuteTeamMarkdownAsync via OperationVector.AuthenticatedRole) regardless of what this client-side
// composition renders — this test proves the client reaches the real action, not that the client-side
// gate is itself sufficient authority.

function buildFixtureSeed() {
  return {
    seed_version: "v1",
    template_ref: {},
    source_ref: {},
    binding_ref: {},
    render_ref: {
      rendered_markdown_hash: "hash-1",
      rendered_at: new Date().toISOString(),
      renderer_version: "v1",
    },
    adjustment_ref: {},
    dashboard_ref: {},
    lineage_ref: {},
  };
}

const fixtureSavedViewId = "00000000-0000-0000-0000-0000000000ee";

function mockAdminAuthoringChain(capturedDispatchBodies: unknown[]): typeof globalThis.fetch {
  return (input: string | URL | Request, init?: RequestInit) => {
    const url = typeof input === "string" ? input : input instanceof Request ? input.url : input.href;

    if (url.startsWith("/api/auth/session")) {
      return Promise.resolve(
        new Response(JSON.stringify({ success: true, subject: "demo_admin", role: "admin" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );
    }

    if (url === "/api/dispatch" && init?.body) {
      const body = JSON.parse(init.body as string) as { action?: string };
      capturedDispatchBodies.push(body);

      if (body.action === "template:list") {
        return Promise.resolve(
          new Response(JSON.stringify({ success: true, emission: { data: { ok: true, templates: [] } } }), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          }),
        );
      }
      if (body.action === "saved_view:search") {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              success: true,
              emission: {
                data: {
                  ok: true,
                  savedViews: [{
                    savedViewId: fixtureSavedViewId,
                    title: "Route-composition fixture view",
                    templateKey: "fixture_template",
                    sourceTableRef: "topology.source",
                    sourceRecordRef: "record-1",
                    status: "active",
                    updatedAt: new Date().toISOString(),
                    cardMetadataJson: {},
                  }],
                },
              },
            }),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      }
      if (body.action === "saved_view:get") {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              success: true,
              emission: {
                data: {
                  ok: true,
                  seedValid: true,
                  savedView: {
                    savedViewId: fixtureSavedViewId,
                    title: "Route-composition fixture view",
                    templateKey: "fixture_template",
                    sourceTableRef: "topology.source",
                    sourceRecordRef: "record-1",
                    status: "active",
                    updatedAt: new Date().toISOString(),
                    cardMetadataJson: {},
                    templateId: "00000000-0000-0000-0000-0000000000bb",
                    bindingJson: { placeholder_bindings: [] },
                    completedPresetSeedJson: buildFixtureSeed(),
                    renderedMarkdown: "# Fixture",
                    userAdjustmentPatchJson: {},
                    searchIndexText: "fixture",
                    createdAt: new Date().toISOString(),
                  },
                },
              },
            }),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      }
      if (body.action === "saved_view:refresh") {
        return Promise.resolve(
          new Response(
            JSON.stringify({ success: true, emission: { data: { ok: true, savedViewId: fixtureSavedViewId } } }),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      }
      return Promise.resolve(new Response(JSON.stringify({ success: false }), { status: 404 }));
    }

    if (url.startsWith("/api/team-markdown/saved-views")) {
      return Promise.resolve(
        new Response(JSON.stringify({ success: true, savedViews: [] }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );
    }
    if (url.startsWith("/api/team-markdown/templates")) {
      return Promise.resolve(
        new Response(JSON.stringify({ success: true, templates: [] }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );
    }
    return Promise.resolve(new Response(JSON.stringify({ success: false }), { status: 404 }));
  };
}

async function clickByRoleOrText(container: Element, selector: string, text?: string): Promise<void> {
  const candidates = Array.from(container.querySelectorAll(selector));
  const target = text ? candidates.find((el) => el.textContent?.includes(text)) : candidates[0];
  if (!target) throw new Error(`element not found: ${selector}${text ? ` with text "${text}"` : ""}`);
  target.dispatchEvent(new (globalThis as unknown as { Event: typeof Event }).Event("click", { bubbles: true }));
  await flushUpdates();
}

Deno.test("TeamDashboardRoleSurface real render: admin session reaches Refresh's real confirmed dispatch through the full route-composition chain (mount -> search -> expand -> operate)", async () => {
  const capturedDispatchBodies: unknown[] = [];
  const { container, cleanup } = setupDom();
  try {
    sessionStorage.setItem(SESSION_TOKEN_KEY, buildClientToken("demo_admin", "admin"));
    globalThis.fetch = mockAdminAuthoringChain(capturedDispatchBodies);

    render(h(TeamDashboardRoleSurface, {}), container);
    await flushUpdates();
    await flushUpdates();
    await flushUpdates();

    assert(container.innerHTML.includes("Route-composition fixture view"), "search result card must actually render from the real dispatch response");

    // Click the search result card to expand it (drives handleExpand -> saved_view:get).
    await clickByRoleOrText(container, "article.md-dashboard-card");
    await flushUpdates();
    assert(container.innerHTML.includes("Refresh"), "expanded MdViewer must show the admin-only Refresh action");

    // Click "Refresh" on the expanded MdViewer -> opens the real SavedViewOperationPanel.
    await clickByRoleOrText(container, "button", "Refresh");
    assert(container.innerHTML.includes("Refresh saved view from source"), "SavedViewOperationPanel must actually mount for the refresh operation");

    // Scope subsequent interactions to SavedViewOperationPanel's own root — the container also
    // still has MdTranslationAuthoringSeedSurface's unrelated textareas/buttons mounted (both
    // components happen to share the "md-dashboard-authoring-surface" class, so the more specific
    // aria-label SavedViewOperationPanel sets — "Saved view refresh" — disambiguates them).
    const operationPanelRoot = container.querySelector('[aria-label="Saved view refresh"]');
    if (!operationPanelRoot) throw new Error("SavedViewOperationPanel root not found");
    const textareas = Array.from(operationPanelRoot.querySelectorAll("textarea")) as HTMLTextAreaElement[];
    const sourceRecordTextarea = textareas[1];
    sourceRecordTextarea.value = JSON.stringify({ field_one: "route-composition-value" });
    sourceRecordTextarea.dispatchEvent(new (globalThis as unknown as { Event: typeof Event }).Event("input", { bubbles: true }));
    await flushUpdates();

    await clickByRoleOrText(operationPanelRoot, "button", "Preview");
    await clickByRoleOrText(container, "button", "Confirm refresh");
    await clickByRoleOrText(container, "button", "Write");

    const writeCalls = (capturedDispatchBodies as { action?: string }[]).filter((b) => b.action === "saved_view:refresh");
    assertEquals(writeCalls.length, 1, "exactly one real saved_view:refresh dispatch must reach the backend through the full mounted composition");
    const writeBody = writeCalls[0] as { idOrHubId?: string; payload?: Record<string, unknown> };
    assertEquals(writeBody.idOrHubId, fixtureSavedViewId);
    assertEquals(writeBody.payload?.confirmed, true);
    assertEquals(
      (writeBody.payload as { sourceRecordJson?: Record<string, unknown> } | undefined)?.sourceRecordJson,
      { field_one: "route-composition-value" },
    );
  } finally {
    render(null, container);
    cleanup();
    globalThis.fetch = originalFetch;
  }
});
