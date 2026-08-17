import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";

/**
 * Role-based-surface-impl bundle tests.
 *
 * Covers the task's explicit NG-axis test requirements that are expressible as static/behavioral
 * contract checks without a live backend: self-credential API never accepting a foreign account
 * identifier, and component catalog coverage for the new/updated surfaces.
 *
 * 2026-07-15 gate0 audit: removed the authenticatedGateHandler and TeamDashboardRoleSurface tests
 * — both frontend/lib/authenticatedGate.ts and frontend/islands/TeamDashboardRoleSurface.tsx were
 * deleted (see .agent/tasks/todo.md). /admin/team-dashboard is the canonical admin-only route again.
 */

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

// scheduler_job_settings.admin_operation was removed from this list with its catalog entry and
// island (scheduler-settings subBundle, admin-surface-topology-seed-conversion): /admin/scheduler is
// now a thin ProjectionShell wrapper over the seeded scheduler.settings.projection manifest, so there
// is no composite admin_operation island left to catalog -- the same retirement AdminEnumsRoster's
// own entry already went through. The route's own seed-driven behavior is proven by
// frontend/tests/schedulerJobManifestProjection.test.ts and
// backend/tests/Topolactor.Integration.Tests/SchedulerSettingsHubRelationUiProjectionLiveDbTests.cs.
const EXPECTED_NEW_OR_UPDATED_ENTRIES: Array<{ componentKey: string; sourcePath: string }> = [
  { componentKey: "saved_view_adjustment_authoring.authoring", sourcePath: "frontend/components/SavedViewAdjustmentAuthoringPanel.tsx" },
  { componentKey: "credential_management.admin_operation", sourcePath: "frontend/islands/AdminUsersRoster.tsx" },
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
