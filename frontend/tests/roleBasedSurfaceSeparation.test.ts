import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import type { FreshContext } from "$fresh/server.ts";
import { authenticatedGateHandler } from "../lib/authenticatedGate.ts";
import { SESSION_TOKEN_KEY } from "../lib/demoSession.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";

/**
 * Role-based-surface-impl bundle tests.
 *
 * Covers the task's explicit NG-axis test requirements that are expressible as static/behavioral
 * contract checks without a live backend: authenticated (not admin-only) gate behavior for any
 * valid role, unauthenticated rejection, self-credential API never accepting a foreign account
 * identifier, admin-only visibility wiring in the Team Dashboard role composition, and component
 * catalog coverage for the new/updated surfaces.
 */

const originalFetch = globalThis.fetch;
const originalBackendUrl = Deno.env.get("DEMO_BACKEND_URL");

function mockRouteContext(nextBody = "ok"): FreshContext {
  return {
    destination: "route",
    next: async () => new Response(nextBody, { status: 200 }),
  } as FreshContext;
}

function restoreFetch(): void {
  globalThis.fetch = originalFetch;
  if (originalBackendUrl === undefined) Deno.env.delete("DEMO_BACKEND_URL");
  else Deno.env.set("DEMO_BACKEND_URL", originalBackendUrl);
}

// ─── authenticatedGateHandler: unauthenticated rejection ───────────────────────

Deno.test("authenticated gate: missing cookie redirects to /auth with redirect param (unauthenticated cannot enter)", async () => {
  const req = new Request("https://example.com/dashboard/team?x=1");
  const res = await authenticatedGateHandler(req, mockRouteContext());
  assertEquals(res.status, 302);
  assertEquals(
    res.headers.get("location"),
    "https://example.com/auth?redirect=%2Fdashboard%2Fteam%3Fx%3D1",
  );
});

Deno.test("authenticated gate: invalid/unverifiable token clears session and redirects", async () => {
  Deno.env.set("DEMO_BACKEND_URL", "http://backend.test");
  globalThis.fetch = () =>
    Promise.resolve(new Response(JSON.stringify({ success: false, errors: [] }), { status: 401 }));
  try {
    const req = new Request("https://example.com/dashboard/team", {
      headers: { cookie: `${SESSION_TOKEN_KEY}=not-validated` },
    });
    const res = await authenticatedGateHandler(req, mockRouteContext());
    assertEquals(res.status, 302);
    assertEquals(res.headers.get("set-cookie")?.includes("Max-Age=0"), true);
  } finally {
    restoreFetch();
  }
});

// ─── authenticatedGateHandler: any valid role passes (Normal JWT admitted) ──────

Deno.test("authenticated gate: valid Normal-role JWT passes through (no admin requirement)", async () => {
  Deno.env.set("DEMO_BACKEND_URL", "http://backend.test");
  globalThis.fetch = (input) => {
    const url = typeof input === "string" ? input : input instanceof Request ? input.url : input.href;
    // authenticatedGateHandler must probe WITHOUT ?expected=admin — any valid role passes.
    if (url === "http://backend.test/auth/session") {
      return Promise.resolve(
        new Response(JSON.stringify({ success: true, subject: "normal_user", role: "user" }), { status: 200 }),
      );
    }
    throw new Error(`unexpected fetch to ${url}`);
  };
  try {
    const req = new Request("https://example.com/dashboard/team", {
      headers: { cookie: `${SESSION_TOKEN_KEY}=${encodeURIComponent("eyJ.normal.token")}` },
    });
    const res = await authenticatedGateHandler(req, mockRouteContext("route-ok"));
    assertEquals(res.status, 200);
    assertEquals(await res.text(), "route-ok");
  } finally {
    restoreFetch();
  }
});

Deno.test("authenticated gate: valid admin-role JWT also passes through", async () => {
  Deno.env.set("DEMO_BACKEND_URL", "http://backend.test");
  globalThis.fetch = () =>
    Promise.resolve(
      new Response(JSON.stringify({ success: true, subject: "demo_admin", role: "admin" }), { status: 200 }),
    );
  try {
    const req = new Request("https://example.com/dashboard/team", {
      headers: { cookie: `${SESSION_TOKEN_KEY}=${encodeURIComponent("eyJ.admin.token")}` },
    });
    const res = await authenticatedGateHandler(req, mockRouteContext("route-ok"));
    assertEquals(res.status, 200);
  } finally {
    restoreFetch();
  }
});

// ─── Team Dashboard role composition: admin-only authoring visibility ──────────

Deno.test("TeamDashboardRoleSurface: TeamMarkdownAuthoring is mounted only when role==='admin'", async () => {
  const source = await Deno.readTextFile("frontend/islands/TeamDashboardRoleSurface.tsx");
  // Viewer is unconditional; authoring is gated behind an explicit admin-role check.
  assertEquals(source.includes("<TeamMarkdownViewer"), true);
  assertEquals(/isAdmin\s*&&\s*<TeamMarkdownAuthoring/.test(source), true);
  assertEquals(source.includes('session.role === "admin"'), true);
});

// ─── self-credential API: no request ever carries a foreign account identifier ─

Deno.test("authApi self-credential functions never send a userId/username in the request body", async () => {
  const source = await Deno.readTextFile("frontend/api/authApi.ts");
  const selfSectionStart = source.indexOf("Self-service credential/session lifecycle");
  const selfSectionEnd = source.indexOf("fetchUserLoginManifest");
  const selfSection = source.slice(selfSectionStart, selfSectionEnd);
  // The only identifiers sent in bodies within this section are sessionId (own-session revoke) and
  // password fields — never userId/username, which would let a caller name a different account.
  assertEquals(/JSON\.stringify\(\{[^}]*userId/.test(selfSection), false);
  assertEquals(/JSON\.stringify\(\{[^}]*username/.test(selfSection), false);
});

Deno.test("admin credential/session revoke endpoints resolve target account only from the route path, never request body", async () => {
  const source = await Deno.readTextFile("frontend/api/adminApi.ts");
  assertEquals(source.includes("/api/admin/auth/users/${userId}/sessions/revoke"), true);
  assertEquals(source.includes("/api/admin/auth/users/${userId}/credential/revoke"), true);
  // adminRevokeUserCredential must never accept or forward a password/newPassword parameter.
  const fnStart = source.indexOf("export async function adminRevokeUserCredential");
  const fnBody = source.slice(fnStart, fnStart + 400);
  assertEquals(/password/i.test(fnBody), false);
});

// ─── component catalog coverage for new/updated surfaces ───────────────────────

const EXPECTED_NEW_OR_UPDATED_ENTRIES: Array<{ componentKey: string; sourcePath: string }> = [
  { componentKey: "team_markdown_dashboard.viewer", sourcePath: "frontend/islands/TeamMarkdownDashboard.tsx" },
  { componentKey: "saved_view_adjustment_authoring.authoring", sourcePath: "frontend/components/SavedViewAdjustmentAuthoringPanel.tsx" },
  { componentKey: "personal_page.projection", sourcePath: "frontend/islands/PersonalPage.tsx" },
  { componentKey: "normal_dashboard_home.projection", sourcePath: "frontend/islands/NormalDashboardHome.tsx" },
  { componentKey: "credential_management.admin_operation", sourcePath: "frontend/islands/AdminUsersRoster.tsx" },
  { componentKey: "admin_enum_roster.admin_operation", sourcePath: "frontend/islands/AdminEnumsRoster.tsx" },
  { componentKey: "scheduler_job_settings.admin_operation", sourcePath: "frontend/islands/SchedulerJobSettingsPanel.tsx" },
  { componentKey: "hub_navigation_admin.admin_operation", sourcePath: "frontend/islands/HubNavigationAdmin.tsx" },
];

for (const expected of EXPECTED_NEW_OR_UPDATED_ENTRIES) {
  Deno.test(`component catalog: ${expected.componentKey} entry exists and sourcePath file is real`, async () => {
    const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === expected.componentKey);
    if (!entry) throw new Error(`missing catalog entry: ${expected.componentKey}`);
    assertEquals(entry.sourcePath, expected.sourcePath);
    const stat = await Deno.stat(expected.sourcePath).catch(() => null);
    if (!stat) throw new Error(`sourcePath does not exist on disk: ${expected.sourcePath}`);
  });
}

Deno.test("component catalog: no entry claims dashboard_placement_candidate without runtimeConnected", () => {
  const violations = COMPONENT_CATALOG_ENTRIES.filter(
    (e) => e.capabilityTags.includes("dashboard_placement_candidate") && !e.runtimeConnected,
  );
  assertEquals(violations.map((e) => e.componentKey), []);
});
