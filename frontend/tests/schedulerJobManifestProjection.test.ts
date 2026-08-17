// frontend/tests/schedulerJobManifestProjection.test.ts
//
// Scheduler job settings projection — route entry-path production proof.
//
// REWRITTEN for the scheduler-settings subBundle (admin-surface-topology-seed-conversion,
// 2026-08-17). The previous content tested frontend/api/adminApi.ts's fetchSchedulerJobManifests /
// createSchedulerJob / editSchedulerJob / disableSchedulerJob helpers and the type surface of
// SchedulerJobManifestItem. All of those were removed with their only consumer,
// frontend/islands/SchedulerJobSettingsPanel.tsx: /admin/scheduler is now a thin ProjectionShell
// wrapper over the seeded scheduler.settings.projection manifest, so there is no hand-written
// per-action helper left to test, and the projection's field set is now bounded server-side by the
// owning SSOT's existing_schema_fields_allowed_for_projection / forbidden_projection_fields
// (asserted for real in backend/tests/Topolactor.Runtime.Tests/
// AdminRuntimeSchedulerSettingsMutationConfirmationTests.cs and, against real DB rows, in
// backend/tests/Topolactor.Integration.Tests/SchedulerSettingsHubRelationUiProjectionLiveDbTests.cs).
//
// What this file proves instead, mirroring frontend/tests/adminEnumsRouteEntry.test.ts's own shape:
//   1. mounting the REAL route module (frontend/routes/admin/scheduler.tsx, the module Fresh serves
//      at /admin/scheduler) with a default URL dispatches the scheduler.settings.projection manifest
//      by MANIFEST KEY -- never a hardcoded manifest UUID, the explicitly-closed anti-pattern.
//   2. an explicit ?manifest= URL selection still wins over the route-supplied default (real user
//      navigation always wins).
//   3. the route module's own source no longer imports the retired island, and the island file and
//      its removed adminApi helper surface are gone from disk/source (static regression guard against
//      reintroducing production reachability for the retired hardcoded surface).
//
// SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml
//   surface_axes.admin.surfaces.scheduler; docs/design/runtime-orchestration-ssot.yaml
//   frontend_routes.admin_route_retirement_matrix (/admin/scheduler: thin_projection_wrapper) and
//   dispatcher_contract.manifest_key_target_ref_resolution_contract.

import {
  assert,
  assertEquals,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { flushUpdates, setupDom } from "./test-dom-setup.ts";
import { ensureRuntimeComponentRegistryInitialized } from "../runtime/runtimeComponentRegistry.ts";
import { __testOnly as schedulerTestOnly } from "../runtime/frontendScheduler.ts";
import { SESSION_TOKEN_KEY } from "../lib/demoSession.ts";
import AdminSchedulerRoute from "../routes/admin/scheduler.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

const SCHEDULER_SETTINGS_MANIFEST_KEY = "scheduler.settings.projection";
const OTHER_MANIFEST_ID = "00000000-0000-0000-0000-000000000abc";

function fakeJwt(): string {
  const header = btoa(JSON.stringify({ alg: "none" }));
  const payload = btoa(JSON.stringify({ realm: "user" }));
  return `${header}.${payload}.sig`;
}

/** happy-dom's Window does not implement EventSource; ProjectionShell calls receiver.connect()
 * unconditionally after a successful initial dispatch. */
class FakeEventSource {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 2;
  readyState = FakeEventSource.OPEN;
  onmessage: ((e: MessageEvent) => void) | null = null;
  onerror: ((e: Event) => void) | null = null;
  constructor(public url: string) {}
  addEventListener() {}
  close() {
    this.readyState = FakeEventSource.CLOSED;
  }
}

/** Captures every /api/dispatch request body; answers auth session/refresh probes with success so
 * AdminAuthGate's own session check resolves "present" and actually mounts ProjectionShell. */
function buildRouteEntryScenario() {
  const capturedDispatchBodies: Record<string, unknown>[] = [];
  const mockFetch = ((url: string, init?: RequestInit) => {
    const path = url.toString();
    if (path.startsWith("/api/auth/session") || path === "/api/auth/refresh") {
      return Promise.resolve(
        new Response(JSON.stringify({ success: true }), { status: 200 }),
      );
    }
    if (path === "/api/dispatch") {
      const body = JSON.parse(String(init?.body ?? "{}")) as Record<string, unknown>;
      capturedDispatchBodies.push(body);
      return Promise.resolve(
        new Response(
          JSON.stringify({
            success: true,
            errors: [],
            emission: {
              manifestId: "00000000-0000-0000-0000-00000005c100",
              layoutId: "layout-route-entry-scenario",
              projectionDefinition: {
                constructorKey: "admin-scheduler-route-entry-test",
                packageIds: [],
                outputKind: "ui_projection",
              },
              layoutNodes: [],
            },
          }),
          { status: 200 },
        ),
      );
    }
    return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
  }) as typeof fetch;
  return { fetch: mockFetch, capturedDispatchBodies };
}

async function mountAdminSchedulerRoute(
  url: string,
): Promise<{ dispatchBodies: Record<string, unknown>[]; cleanup: () => void }> {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();

  const { container, cleanup: domCleanup } = setupDom(url);
  sessionStorage.setItem(SESSION_TOKEN_KEY, fakeJwt());
  const originalEventSource = (globalThis as unknown as { EventSource?: unknown }).EventSource;
  (globalThis as unknown as { EventSource: unknown }).EventSource = FakeEventSource;
  const originalFetch = globalThis.fetch;
  const scenario = buildRouteEntryScenario();
  globalThis.fetch = scenario.fetch;

  render(h(AdminSchedulerRoute, {}), container);
  for (let i = 0; i < 60 && scenario.capturedDispatchBodies.length === 0; i++) {
    await flushUpdates();
  }

  return {
    dispatchBodies: scenario.capturedDispatchBodies,
    cleanup: () => {
      globalThis.fetch = originalFetch;
      (globalThis as unknown as { EventSource: unknown }).EventSource = originalEventSource;
      render(null, container);
      domCleanup();
    },
  };
}

Deno.test(
  "AdminSchedulerRoute (real /admin/scheduler route mount): a default URL dispatches the scheduler.settings.projection manifest BY MANIFEST KEY, never a hardcoded manifest UUID",
  async () => {
    const { dispatchBodies, cleanup } = await mountAdminSchedulerRoute(
      "http://localhost/admin/scheduler",
    );
    try {
      assert(dispatchBodies.length > 0, "expected at least one /api/dispatch call");
      const payload = dispatchBodies[0].payload as Record<string, unknown>;
      assertEquals(typeof payload.target_ref, "string");
      assertEquals(
        payload.target_ref,
        `manifest_key:${SCHEDULER_SETTINGS_MANIFEST_KEY}:projection_entry`,
        "the route must pin its manifest by manifest_key, resolved backend-side by the generic manifest_key_target_ref_resolution_contract",
      );
      assert(
        !/^manifest:[0-9a-f-]{36}:/.test(payload.target_ref as string),
        "a raw manifest UUID target_ref is the explicitly-closed anti-pattern for this route",
      );
    } finally {
      cleanup();
    }
  },
);

Deno.test(
  "AdminSchedulerRoute (real /admin/scheduler route mount): an explicit ?manifest= query param overrides the route's manifestKey prop default",
  async () => {
    const { dispatchBodies, cleanup } = await mountAdminSchedulerRoute(
      `http://localhost/admin/scheduler?manifest=${OTHER_MANIFEST_ID}`,
    );
    try {
      assert(dispatchBodies.length > 0, "expected at least one /api/dispatch call");
      const payload = dispatchBodies[0].payload as Record<string, unknown>;
      assertEquals(typeof payload.target_ref, "string");
      assert(
        (payload.target_ref as string).startsWith(`manifest:${OTHER_MANIFEST_ID}:`),
        `expected explicit ?manifest= to win over the manifestKey prop default, got ${payload.target_ref}`,
      );
      assert(
        !(payload.target_ref as string).includes(SCHEDULER_SETTINGS_MANIFEST_KEY),
        "the manifest_key default must not be used once an explicit ?manifest= is present",
      );
    } finally {
      cleanup();
    }
  },
);

Deno.test(
  "frontend/routes/admin/scheduler.tsx no longer imports the retired SchedulerJobSettingsPanel island, and the island file itself is gone",
  async () => {
    const source = await Deno.readTextFile("frontend/routes/admin/scheduler.tsx");
    assert(
      !/^import .*SchedulerJobSettingsPanel/m.test(source),
      "routes/admin/scheduler.tsx must not import the retired/deleted SchedulerJobSettingsPanel island",
    );
    assert(source.includes("ProjectionShell"), "routes/admin/scheduler.tsx must mount ProjectionShell");
    assert(
      source.includes(`"${SCHEDULER_SETTINGS_MANIFEST_KEY}"`),
      "routes/admin/scheduler.tsx must pin the surface by manifest_key",
    );
    const islandStat = await Deno.stat("frontend/islands/SchedulerJobSettingsPanel.tsx").catch(() => null);
    assertEquals(islandStat, null, "frontend/islands/SchedulerJobSettingsPanel.tsx must not exist on disk");
  },
);

Deno.test(
  "frontend/api/adminApi.ts no longer exports the retired per-action scheduler helpers (their only consumer was the deleted island)",
  async () => {
    const source = await Deno.readTextFile("frontend/api/adminApi.ts");
    for (const removed of [
      "export async function fetchSchedulerJobManifests",
      "export async function createSchedulerJob",
      "export async function editSchedulerJob",
      "export async function disableSchedulerJob",
      "export type SchedulerJobManifestItem",
      "export type SchedulerJobDraftInput",
    ]) {
      assert(
        !source.includes(removed),
        `adminApi.ts must no longer declare '${removed}' -- scheduler settings dispatch goes through the generic projection runtime`,
      );
    }
  },
);

Deno.test(
  "the scheduler settings surface must not expose create/edit/step-chain or credential binding dispatch from its own route module",
  async () => {
    // scope_boundary.out_of_scope: create / edit / step_chain_authoring /
    // credential_or_external_port_binding. The route is a thin wrapper, so the only way any of these
    // could reappear here is a hand-written dispatch or island import being added back.
    const source = await Deno.readTextFile("frontend/routes/admin/scheduler.tsx");
    for (const forbidden of [
      "scheduler_jobs:create",
      "scheduler_jobs:edit",
      "credential_management:",
      "abstractFunctionKey",
      "credentialRequirementRef",
      "externalPortRef",
    ]) {
      assert(
        !source.includes(`${forbidden}"`) && !source.includes(`${forbidden}'`),
        `routes/admin/scheduler.tsx must not carry out-of-scope '${forbidden}' dispatch/authoring content`,
      );
    }
  },
);
