// frontend/tests/projectionShellAdminRuntimeWritePayloadCapture.test.ts
//
// Production-path scenario proof (PR #599 review): the mechanism tests in
// liveNodeValueTrackerAndAdminRuntimePayloadFrom.test.ts exercise renderEmission()/
// emitBoundEvent() directly against hand-built RuntimeComponentSpec objects. This file
// instead mounts the REAL frontend/islands/ProjectionShell.tsx production component,
// simulates a genuine DOM `input` event on a rendered text input and a genuine DOM
// `click` on a rendered button, and asserts the resulting /api/dispatch request body —
// proving the live node value tracker + Lane 2 payloadFrom resolution wiring inside
// ProjectionShell itself, not a test-only substitute for it.
//
// SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
// lane_storage_boundary.known_gaps.remaining_write_payload_capture_gap.

import {
  assertEquals,
  assertExists,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import { ensureRuntimeComponentRegistryInitialized } from "../runtime/runtimeComponentRegistry.ts";
import { __testOnly as schedulerTestOnly } from "../runtime/frontendScheduler.ts";
import ProjectionShell from "../islands/ProjectionShell.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

const ADMIN_ENUM_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae200";

function fakeJwt(): string {
  const header = btoa(JSON.stringify({ alg: "none" }));
  const payload = btoa(JSON.stringify({ realm: "user" }));
  return `${header}.${payload}.sig`;
}

/** Minimal EventSource stub — happy-dom's Window does not implement it, and
 * ProjectionShell calls receiver.connect() unconditionally after a successful
 * initial dispatch (see sseReceiver.ts). No real connection is attempted. */
class FakeEventSource {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 2;
  readyState = FakeEventSource.OPEN;
  onmessage: ((e: MessageEvent) => void) | null = null;
  onerror: ((e: Event) => void) | null = null;
  constructor(public url: string) {}
  addEventListener(_type: string, _cb: (e: MessageEvent) => void) {}
  close() {
    this.readyState = FakeEventSource.CLOSED;
  }
}

Deno.test(
  "ProjectionShell (real mount): typing into a rendered input then clicking a rendered admin_runtime button resolves live value tracking + Lane 2 payloadFrom into the /api/dispatch request body",
  async () => {
    ensureRuntimeComponentRegistryInitialized();
    schedulerTestOnly.resetCommandQueue();

    const { container, cleanup } = setupDom();
    const originalEventSource = (globalThis as unknown as { EventSource?: unknown })
      .EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource =
      FakeEventSource;
    const originalFetch = globalThis.fetch;

    const capturedDispatchBodies: Record<string, unknown>[] = [];

    globalThis.fetch = ((url: string, init?: RequestInit) => {
      const path = url.toString();
      if (path.startsWith("/api/auth/session")) {
        return Promise.resolve(
          new Response(JSON.stringify({ success: true }), { status: 200 }),
        );
      }
      if (path === "/api/auth/refresh") {
        return Promise.resolve(
          new Response(JSON.stringify({ success: true }), { status: 200 }),
        );
      }
      if (path === "/api/dispatch") {
        const body = JSON.parse(String(init?.body ?? "{}")) as Record<
          string,
          unknown
        >;
        capturedDispatchBodies.push(body);
        if (capturedDispatchBodies.length === 1) {
          // Initial mount dispatch — returns the production-shaped emission:
          // a plain text input (no wiring of its own) and an admin_runtime
          // button carrying a bindRuntimeDispatchPayload entry that binds the
          // input's live value to the create_group payload.
          return Promise.resolve(
            new Response(
              JSON.stringify({
                success: true,
                emission: {
                  manifestId: ADMIN_ENUM_MANIFEST_ID,
                  layoutId: "layout-projection-shell-scenario",
                  layoutNodes: [
                    {
                      nodeId: "node-name-input",
                      nodeKind: "catalog_component",
                      componentId: "comp-name-input-001",
                      componentKind: "form_input/input",
                      componentKey: "text_input.primitive",
                      orderIndex: 0,
                      // A harmless UI状態更新 targeting the OTHER node (predeclared
                      // automatically from the current node list —
                      // uiEventEffectRunner.ts predeclareProjectionState; targeting
                      // itself would trip side_effect_cycle_policy's direct-self-loop
                      // guard, an unrelated policy this test does not exercise)
                      // satisfies inputFactory's requireBinding(spec,"change")
                      // without establishing any backend dispatch of its own. This is
                      // orthogonal to (and does not interfere with) the
                      // node-value-tracking lane under test, which fires
                      // unconditionally before binding dispatch.
                      runtimeInteractions: [
                        {
                          trigger: "change",
                          actionType: "setState",
                          targetNodeId: "node-submit-button",
                          statePath: "input_touched",
                          value: true,
                        },
                      ],
                    },
                    {
                      nodeId: "node-submit-button",
                      nodeKind: "catalog_component",
                      componentId: "comp-submit-button-001",
                      componentKind: "action/button",
                      componentKey: "button.primitive",
                      orderIndex: 1,
                      wiringKind: "admin_runtime",
                      targetSurface: "manifest",
                      targetRef:
                        `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:create_group`,
                      runtimeInteractions: [
                        {
                          trigger: "click",
                          actionType: "bindRuntimeDispatchPayload",
                          payloadFrom: {
                            groupName: "node:node-name-input.value",
                          },
                        },
                      ],
                    },
                  ],
                },
              }),
              { status: 200 },
            ),
          );
        }
        // Lane 2 write dispatch triggered by the simulated button click.
        return Promise.resolve(
          new Response(JSON.stringify({ success: true, errors: [] }), {
            status: 200,
          }),
        );
      }
      return Promise.resolve(
        new Response(JSON.stringify({ success: true }), { status: 200 }),
      );
    }) as typeof fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());

      render(h(ProjectionShell, {}), container);

      // Drain the mount effect's async chain: probe -> refresh -> initial
      // dispatch -> setSpecs -> render. Poll until the button DOM node exists
      // rather than a fixed number of flushes (fetch mock resolves
      // asynchronously across several microtask/macrotask hops).
      let buttonEl: HTMLButtonElement | null = null;
      for (let i = 0; i < 40 && buttonEl === null; i++) {
        await flushUpdates();
        buttonEl = container.querySelector("button");
      }
      assertExists(
        buttonEl,
        "the admin_runtime button must have rendered from the initial dispatch's emission",
      );
      const inputEl = container.querySelector("input");
      assertExists(inputEl, "the text input must have rendered");

      // Simulate a genuine DOM input event — the SAME event path a real user
      // typing into this field produces (Input.tsx wires native onInput).
      (inputEl as HTMLInputElement).value = "Status";
      const inputEvent = new (globalThis as unknown as { Event: typeof Event })
        .Event("input", { bubbles: true });
      inputEl!.dispatchEvent(inputEvent);
      await flushUpdates();

      // Simulate a genuine DOM click on the admin_runtime button.
      const clickEvent = new (globalThis as unknown as { Event: typeof Event })
        .Event("click", { bubbles: true });
      buttonEl!.dispatchEvent(clickEvent);

      // The click's Lane 2 dispatch is fire-and-forget (enqueueRuntimeComponentCommand);
      // poll until the second /api/dispatch call lands.
      for (let i = 0; i < 40 && capturedDispatchBodies.length < 2; i++) {
        await flushUpdates();
      }

      assertEquals(
        capturedDispatchBodies.length >= 2,
        true,
        "expected an initial dispatch plus the button click's Lane 2 dispatch",
      );
      const writeDispatchBody = capturedDispatchBodies[1];
      assertEquals(writeDispatchBody.layer, "enum_dictionary");
      assertEquals(writeDispatchBody.action, "create_group");
      const payload = writeDispatchBody.payload as Record<string, unknown>;
      // The typed input value, captured via the real DOM input event through
      // ProjectionShell's live node value tracker (not a test-injected map),
      // resolved by Lane 2's payloadFrom authority.
      assertEquals(payload.groupName, "Status");
    } finally {
      globalThis.fetch = originalFetch;
      (globalThis as unknown as { EventSource: unknown }).EventSource =
        originalEventSource;
      schedulerTestOnly.resetCommandQueue();
      render(null, container);
      cleanup();
    }
  },
);
