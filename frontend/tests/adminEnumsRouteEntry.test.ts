// frontend/tests/adminEnumsRouteEntry.test.ts
//
// Route entry-path production proof (admin-enum subBundle closure round 36). Prior rounds'
// DOM proofs (projectionShellAdminRuntimeWritePayloadCapture.test.ts) mount
// frontend/islands/ProjectionShell.tsx DIRECTLY with an explicit manifestId prop -- that proves
// ProjectionShell's own mechanism, but NOT that frontend/routes/admin/enums.tsx (the actual Fresh
// route Deno serves at /admin/enums) wires manifestId=ae200 as its own DEFAULT, or that an
// explicit ?manifest= URL selection still wins over that default the way production navigation
// requires. This file instead imports and mounts the REAL route module's default export
// (AdminEnumsRoute), proving:
//   1. a default URL (no ?route=/?manifest=/?package=) dispatches the ae200 manifest via the
//      route's own manifestId prop.
//   2. an explicit ?manifest=<uuid> query param overrides that default (real user navigation
//      always wins over a route-supplied default).
//   3. the route module's own source no longer imports AdminEnumsRoster (deleted this round --
//      static regression guard against reintroducing production reachability for the retired
//      island).
//
// SSOT: docs/design/admin-master-roster-management-ssot.yaml canonical_routes.admin_enums,
// docs/design/runtime-orchestration-ssot.yaml route_retirement records.

import {
  assert,
  assertEquals,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { flushUpdates, setupDom } from "./test-dom-setup.ts";
import { ensureRuntimeComponentRegistryInitialized } from "../runtime/runtimeComponentRegistry.ts";
import { __testOnly as schedulerTestOnly } from "../runtime/frontendScheduler.ts";
import { SESSION_TOKEN_KEY } from "../lib/demoSession.ts";
import AdminEnumsRoute from "../routes/admin/enums.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

const ADMIN_ENUM_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae200";
const OTHER_MANIFEST_ID = "00000000-0000-0000-0000-000000000abc";

function fakeJwt(): string {
  const header = btoa(JSON.stringify({ alg: "none" }));
  const payload = btoa(JSON.stringify({ realm: "user" }));
  return `${header}.${payload}.sig`;
}

/** happy-dom's Window does not implement EventSource; ProjectionShell calls
 * receiver.connect() unconditionally after a successful initial dispatch. */
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

/** Captures every /api/dispatch request body; answers auth session/refresh probes with
 * success so AdminAuthGate's own session check (independent of ProjectionShell's) resolves
 * "present" and actually mounts ProjectionShell's children. */
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
      const payload = body.payload as Record<string, unknown> | undefined;
      const targetRef = typeof payload?.target_ref === "string" ? payload.target_ref : "";
      const match = /^manifest:([^:]+):/.exec(targetRef);
      const manifestId = match ? match[1] : ADMIN_ENUM_MANIFEST_ID;
      return Promise.resolve(
        new Response(
          JSON.stringify({
            success: true,
            errors: [],
            emission: {
              manifestId,
              layoutId: "layout-route-entry-scenario",
              projectionDefinition: {
                constructorKey: "admin-enums-route-entry-test",
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

async function mountAdminEnumsRoute(
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

  render(h(AdminEnumsRoute, {}), container);
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
  "AdminEnumsRoute (real /admin/enums route mount): a default URL with no explicit ?route=/?manifest=/?package= selection dispatches the ae200 manifest via the route's own manifestId prop",
  async () => {
    const { dispatchBodies, cleanup } = await mountAdminEnumsRoute(
      "http://localhost/admin/enums",
    );
    try {
      assert(dispatchBodies.length > 0, "expected at least one /api/dispatch call");
      const payload = dispatchBodies[0].payload as Record<string, unknown>;
      assertEquals(typeof payload.target_ref, "string");
      assert(
        (payload.target_ref as string).startsWith(`manifest:${ADMIN_ENUM_MANIFEST_ID}:`),
        `expected target_ref to target ae200, got ${payload.target_ref}`,
      );
    } finally {
      cleanup();
    }
  },
);

Deno.test(
  "AdminEnumsRoute (real /admin/enums route mount): an explicit ?manifest= query param overrides the route's manifestId prop default",
  async () => {
    const { dispatchBodies, cleanup } = await mountAdminEnumsRoute(
      `http://localhost/admin/enums?manifest=${OTHER_MANIFEST_ID}`,
    );
    try {
      assert(dispatchBodies.length > 0, "expected at least one /api/dispatch call");
      const payload = dispatchBodies[0].payload as Record<string, unknown>;
      assertEquals(typeof payload.target_ref, "string");
      assert(
        (payload.target_ref as string).startsWith(`manifest:${OTHER_MANIFEST_ID}:`),
        `expected explicit ?manifest= to win over the manifestId prop default, got ${payload.target_ref}`,
      );
      assert(
        !(payload.target_ref as string).startsWith(`manifest:${ADMIN_ENUM_MANIFEST_ID}:`),
        "the ae200 default must not be used once an explicit ?manifest= is present",
      );
    } finally {
      cleanup();
    }
  },
);

Deno.test(
  "frontend/routes/admin/enums.tsx no longer imports the retired AdminEnumsRoster island (deleted this round), and the island file itself is gone",
  async () => {
    const source = await Deno.readTextFile("frontend/routes/admin/enums.tsx");
    assert(
      !/^import .*AdminEnumsRoster/m.test(source),
      "routes/admin/enums.tsx must not import the retired/deleted AdminEnumsRoster island",
    );
    assert(source.includes("ProjectionShell"), "routes/admin/enums.tsx must mount ProjectionShell");
    const islandStat = await Deno.stat("frontend/islands/AdminEnumsRoster.tsx").catch(() => null);
    assertEquals(islandStat, null, "frontend/islands/AdminEnumsRoster.tsx must not exist on disk");
  },
);
