// frontend/tests/projectionShellAdminRuntimeWritePayloadCapture.test.ts
//
// Production-path scenario proof (PR #599 review, rounds 1, 3, 6): the mechanism
// tests (uiEventEffectRunner.test.ts, payloadFromResolver.test.ts,
// runtimeComponentFactory.test.ts, renderEmissionPropBindings.test.ts) exercise
// renderEmission()/emitBoundEvent() directly against hand-built RuntimeComponentSpec
// objects. This file instead mounts the REAL frontend/islands/ProjectionShell.tsx
// production component, simulates genuine DOM input/click events AND a genuine SSE
// "projection" refresh event through the real projectionRuntime/projectionDefinition
// path (not a substitute), and asserts the resulting /api/dispatch request bodies —
// proving the live node value tracker + Lane 2 payloadFrom resolution wiring inside
// ProjectionShell itself, across rerender/SSE-refresh/node-reconciliation/unmount-remount.
//
// SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
// lane_storage_boundary.known_gaps.remaining_write_payload_capture_gap,
// admin_runtime_payload_binding_contract.

import {
  assert,
  assertEquals,
  assertExists,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { flushUpdates, setupDom } from "./test-dom-setup.ts";
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

/**
 * EventSource stub — happy-dom's Window does not implement it, and
 * ProjectionShell calls receiver.connect() unconditionally after a successful
 * initial dispatch (see sseReceiver.ts). No real network connection is attempted;
 * `emit()` lets a test fire a genuine SSE event through the SAME
 * addEventListener registration sseReceiver.ts's connect() performs — the
 * real production listener wiring, not a bypassed substitute.
 */
class FakeEventSource {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 2;
  static instances: FakeEventSource[] = [];
  readyState = FakeEventSource.OPEN;
  onmessage: ((e: MessageEvent) => void) | null = null;
  onerror: ((e: Event) => void) | null = null;
  private listeners = new Map<string, Array<(e: { data: string }) => void>>();
  constructor(public url: string) {
    FakeEventSource.instances.push(this);
  }
  addEventListener(type: string, cb: (e: { data: string }) => void) {
    if (!this.listeners.has(type)) this.listeners.set(type, []);
    this.listeners.get(type)!.push(cb);
  }
  emit(type: string, data: string) {
    for (const cb of this.listeners.get(type) ?? []) {
      cb({ data } as unknown as MessageEvent);
    }
  }
  close() {
    this.readyState = FakeEventSource.CLOSED;
  }
}

/** A minimal ProjectionDefinition that makes projectionRuntime's ui_projection
 * output kind trivially succeed (constructProjection's only requirement for
 * that outputKind is a non-empty constructorKey) — required so the SSE
 * "projection" event's real handling path (not a stub) reaches
 * ProjectionShell's onProjectionUpdate handler at all. */
const MINIMAL_PROJECTION_DEFINITION = {
  constructorKey: "projection-shell-scenario-test",
  packageIds: [] as string[],
  outputKind: "ui_projection" as const,
};

type MockScenario = {
  fetch: typeof fetch;
  capturedDispatchBodies: Record<string, unknown>[];
};

/** Builds a mock fetch that answers auth probe/refresh + routes /api/dispatch
 * calls through a caller-supplied responder keyed by call index (1-based). */
function buildMockScenario(
  dispatchResponder: (
    callIndex: number,
    body: Record<string, unknown>,
  ) => Record<string, unknown>,
): MockScenario {
  const capturedDispatchBodies: Record<string, unknown>[] = [];
  const mockFetch = ((url: string, init?: RequestInit) => {
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
      const responseBody = dispatchResponder(
        capturedDispatchBodies.length,
        body,
      );
      return Promise.resolve(
        new Response(JSON.stringify(responseBody), { status: 200 }),
      );
    }
    return Promise.resolve(
      new Response(JSON.stringify({ success: true }), { status: 200 }),
    );
  }) as typeof fetch;
  return { fetch: mockFetch, capturedDispatchBodies };
}

function inputChangeSetStateInteraction(targetNodeId: string) {
  // A harmless UI状態更新 targeting a DIFFERENT node (predeclared automatically
  // from the current node list — uiEventEffectRunner.ts predeclareProjectionState;
  // targeting itself would trip side_effect_cycle_policy's direct-self-loop guard,
  // an unrelated policy this test does not exercise) satisfies inputFactory's
  // requireBinding(spec,"change") without establishing any backend dispatch of
  // its own. Orthogonal to (and does not interfere with) the node-value-tracking
  // lane under test, which fires unconditionally before binding dispatch.
  return [{
    trigger: "change",
    actionType: "setState",
    targetNodeId,
    statePath: "touched",
    value: true,
  }];
}

function simulateRowClick(rowEl: HTMLTableRowElement) {
  const event = new (globalThis as unknown as { Event: typeof Event }).Event(
    "click",
    { bubbles: true },
  );
  try {
    rowEl.dispatchEvent(event);
  } catch {
    // Same rationale as simulateClick below — asserting on captured dispatch
    // bodies, not on whether this call itself threw.
  }
}

function simulateInput(inputEl: HTMLInputElement, value: string) {
  inputEl.value = value;
  const event = new (globalThis as unknown as { Event: typeof Event }).Event(
    "input",
    { bubbles: true },
  );
  inputEl.dispatchEvent(event);
}

function simulateClick(buttonEl: HTMLButtonElement) {
  const event = new (globalThis as unknown as { Event: typeof Event }).Event(
    "click",
    { bubbles: true },
  );
  // A rejected/malformed dispatch (e.g. resolvePayloadFrom fail-close) throws
  // inside buttonFactory's onClick before enqueue — per the DOM event spec this
  // is reported to the global error handler asynchronously, not re-thrown to
  // the dispatchEvent() caller, but guard defensively for the test environment.
  try {
    buttonEl.dispatchEvent(event);
  } catch {
    // Intentionally swallowed — the test asserts on captured dispatch bodies,
    // not on whether this call itself threw.
  }
}

async function waitFor(
  predicate: () => boolean,
  maxIterations = 40,
): Promise<void> {
  for (let i = 0; i < maxIterations && !predicate(); i++) {
    await flushUpdates();
  }
}

Deno.test(
  "ProjectionShell (real mount): typing into a rendered input then clicking a rendered admin_runtime button resolves live value tracking + Lane 2 payloadFrom into the /api/dispatch request body",
  async () => {
    ensureRuntimeComponentRegistryInitialized();
    schedulerTestOnly.resetCommandQueue();
    FakeEventSource.instances = [];

    const { container, cleanup } = setupDom();
    const originalEventSource =
      (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource =
      FakeEventSource;
    const originalFetch = globalThis.fetch;

    const scenario = buildMockScenario((callIndex) => {
      if (callIndex === 1) {
        return {
          success: true,
          emission: {
            manifestId: ADMIN_ENUM_MANIFEST_ID,
            layoutId: "layout-projection-shell-scenario",
            projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
            layoutNodes: [
              {
                nodeId: "node-name-input",
                nodeKind: "catalog_component",
                componentId: "comp-name-input-001",
                componentKind: "form_input/input",
                componentKey: "text_input.primitive",
                orderIndex: 0,
                runtimeInteractions: inputChangeSetStateInteraction(
                  "node-submit-button",
                ),
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
                dispatchPayloadFromByTrigger: {
                  click: { groupName: "node:node-name-input.value" },
                },
              },
            ],
          },
        };
      }
      // Lane 2 write dispatch triggered by the simulated button click.
      return { success: true, errors: [] };
    });
    globalThis.fetch = scenario.fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());

      render(h(ProjectionShell, {}), container);

      let buttonEl: HTMLButtonElement | null = null;
      await waitFor(() => {
        buttonEl = container.querySelector("button");
        return buttonEl !== null;
      });
      assertExists(
        buttonEl,
        "the admin_runtime button must have rendered from the initial dispatch's emission",
      );
      const inputEl = container.querySelector("input");
      assertExists(inputEl, "the text input must have rendered");

      simulateInput(inputEl as HTMLInputElement, "Status");
      await flushUpdates();

      simulateClick(buttonEl!);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 2);

      assertEquals(
        scenario.capturedDispatchBodies.length >= 2,
        true,
        "expected an initial dispatch plus the button click's Lane 2 dispatch",
      );
      const writeDispatchBody = scenario.capturedDispatchBodies[1];
      assertEquals(writeDispatchBody.layer, "enum_dictionary");
      assertEquals(writeDispatchBody.action, "create_group");
      const payload = writeDispatchBody.payload as Record<string, unknown>;
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

Deno.test(
  "ProjectionShell (real mount): a node's own dispatchTargetRefByTrigger override dispatches to a DIFFERENT manifest/layer/action than the layout's own uniform target_ref (real db/seed_empty.sql ae200 enum_create_group_button shape, round 19)",
  async () => {
    ensureRuntimeComponentRegistryInitialized();
    schedulerTestOnly.resetCommandQueue();
    FakeEventSource.instances = [];

    const { container, cleanup } = setupDom();
    const originalEventSource =
      (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource =
      FakeEventSource;
    const originalFetch = globalThis.fetch;

    const AE210_CREATE_GROUP_TARGET_REF =
      "manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group";

    const scenario = buildMockScenario((callIndex) => {
      if (callIndex === 1) {
        return {
          success: true,
          emission: {
            manifestId: ADMIN_ENUM_MANIFEST_ID,
            layoutId: "layout-ae200-scenario",
            projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
            layoutNodes: [
              {
                nodeId: "enum_create_group_name_input",
                nodeKind: "catalog_component",
                componentId: "comp-name-input-001",
                componentKind: "form_input/input",
                componentKey: "text_input.primitive",
                orderIndex: 0,
                runtimeInteractions: inputChangeSetStateInteraction(
                  "enum_create_group_button",
                ),
              },
              {
                nodeId: "enum_create_group_button",
                nodeKind: "catalog_component",
                componentId: "comp-create-group-button-001",
                componentKind: "action/button",
                componentKey: "button.primitive",
                orderIndex: 1,
                // The LAYOUT's own uniform binding (ae205's real target_ref) — every OTHER node
                // in ae200's layout dispatches list_groups by default.
                wiringKind: "admin_runtime",
                targetSurface: "manifest",
                targetRef:
                  `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:list_groups`,
                // The node-local override (db/seed_empty.sql's real enum_create_group_button
                // tensor node, round 19) — must WIN over the layout's own uniform target_ref
                // above for this node's "click" trigger.
                dispatchTargetRefByTrigger: {
                  click: AE210_CREATE_GROUP_TARGET_REF,
                },
                dispatchPayloadFromByTrigger: {
                  click: {
                    groupName: "node:enum_create_group_name_input.value",
                    confirmed: "literal:true",
                  },
                },
              },
            ],
          },
        };
      }
      // The override dispatch triggered by the simulated button click.
      return { success: true, errors: [] };
    });
    globalThis.fetch = scenario.fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());

      render(h(ProjectionShell, {}), container);

      let buttonEl: HTMLButtonElement | null = null;
      await waitFor(() => {
        buttonEl = container.querySelector("button");
        return buttonEl !== null;
      });
      assertExists(
        buttonEl,
        "the enum_create_group_button must have rendered from the initial dispatch's emission",
      );
      const inputEl = container.querySelector("input");
      assertExists(inputEl, "the group name input must have rendered");

      simulateInput(inputEl as HTMLInputElement, "live-db-round-trip-status");
      await flushUpdates();

      simulateClick(buttonEl!);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 2);

      assertEquals(
        scenario.capturedDispatchBodies.length >= 2,
        true,
        "expected an initial dispatch plus the button click's override dispatch",
      );
      const overrideDispatchBody = scenario.capturedDispatchBodies[1];
      // Layer/action must reflect the OVERRIDE's own embedded layer:action
      // (enum_dictionary:create_group), NOT the layout's own uniform binding
      // (enum_dictionary:list_groups) -- proving the node-level override actually took effect,
      // not merely that the layout happened to already be bound to the right thing.
      assertEquals(overrideDispatchBody.layer, "enum_dictionary");
      assertEquals(overrideDispatchBody.action, "create_group");
      const overridePayload = overrideDispatchBody.payload as Record<
        string,
        unknown
      >;
      assertEquals(overridePayload.target_ref, AE210_CREATE_GROUP_TARGET_REF);
      assertEquals(overridePayload.groupName, "live-db-round-trip-status");
      // literal: sources always resolve to a JS string (payloadFromResolver.ts) -- the backend's
      // IsTruthyPayloadFlag deliberately accepts this string form for exactly this reason
      // (AdminRuntimeMasterRoster.cs), so "true" (not the JSON boolean) is the correct wire value.
      assertEquals(overridePayload.confirmed, "true");
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

Deno.test(
  "ProjectionShell (real mount): a genuine SSE refresh event reconciles a removed node's tracked value — a later dispatch referencing it fails close, never reuses the stale value",
  async () => {
    ensureRuntimeComponentRegistryInitialized();
    schedulerTestOnly.resetCommandQueue();
    FakeEventSource.instances = [];

    const { container, cleanup } = setupDom();
    const originalEventSource =
      (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource =
      FakeEventSource;
    const originalFetch = globalThis.fetch;

    const initialEmission = {
      manifestId: ADMIN_ENUM_MANIFEST_ID,
      layoutId: "layout-projection-shell-refresh-scenario",
      projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
      layoutNodes: [
        {
          nodeId: "node-name-input",
          nodeKind: "catalog_component",
          componentId: "comp-name-input-001",
          componentKind: "form_input/input",
          componentKey: "text_input.primitive",
          orderIndex: 0,
          runtimeInteractions: inputChangeSetStateInteraction(
            "node-submit-button",
          ),
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
          dispatchPayloadFromByTrigger: {
            click: { groupName: "node:node-name-input.value" },
          },
        },
      ],
    };
    // Refresh removes node-name-input entirely (a real projection change — e.g.
    // the row/section that carried it is gone) while the SAME persisted wiring
    // still references it in payloadFrom, exactly the "stale/deleted-node
    // reference" case component_runtime_state_effect_boundary's
    // mutation_authority_unification fails close on for UI状態更新 — this proof
    // extends the same contract to live node value tracking.
    const refreshedEmission = {
      ...initialEmission,
      layoutNodes: [
        {
          nodeId: "node-submit-button",
          nodeKind: "catalog_component",
          componentId: "comp-submit-button-001",
          componentKind: "action/button",
          componentKey: "button.primitive",
          orderIndex: 0,
          wiringKind: "admin_runtime",
          targetSurface: "manifest",
          targetRef:
            `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:create_group`,
          dispatchPayloadFromByTrigger: {
            click: { groupName: "node:node-name-input.value" },
          },
        },
      ],
    };

    const scenario = buildMockScenario((callIndex) => {
      if (callIndex === 1) return { success: true, emission: initialEmission };
      if (callIndex === 3) {
        return { success: true, emission: refreshedEmission };
      }
      // callIndex 2 (first click's Lane 2 write) — succeeds normally.
      return { success: true, errors: [] };
    });
    globalThis.fetch = scenario.fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
      render(h(ProjectionShell, {}), container);

      let buttonEl: HTMLButtonElement | null = null;
      await waitFor(() => {
        buttonEl = container.querySelector("button");
        return buttonEl !== null;
      });
      assertExists(buttonEl);
      const inputEl = container.querySelector("input") as HTMLInputElement;
      assertExists(inputEl);

      // Establish a real tracked value and confirm the write path works BEFORE
      // the refresh (baseline — proves this isn't just testing an already-broken path).
      simulateInput(inputEl, "Beta");
      await flushUpdates();
      simulateClick(buttonEl!);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 2);
      assertEquals(scenario.capturedDispatchBodies.length, 2);
      assertEquals(
        (scenario.capturedDispatchBodies[1].payload as Record<string, unknown>)
          .groupName,
        "Beta",
      );

      // Fire a REAL SSE "projection" event through the same addEventListener
      // registration sseReceiver.ts's connect() performs — routes through
      // enqueueProjectionHookTrigger -> sseDispatcher -> projectionRuntime ->
      // ProjectionShell's onProjectionUpdate handler, which re-dispatches via
      // queueClientCommand (the 3rd /api/dispatch call).
      assertEquals(
        FakeEventSource.instances.length,
        1,
        "sseReceiver.connect() must have created exactly one EventSource",
      );
      FakeEventSource.instances[0].emit(
        "projection",
        JSON.stringify({ manifest_id: ADMIN_ENUM_MANIFEST_ID }),
      );

      // Wait for the refresh to land: the input node disappears from the DOM
      // once renderEmission() re-renders from the refreshed (node-removed) emission.
      await waitFor(() => container.querySelector("input") === null);
      assertEquals(
        container.querySelector("input"),
        null,
        "the removed input node must no longer be in the rendered DOM after SSE refresh",
      );
      assertEquals(
        scenario.capturedDispatchBodies.length,
        3,
        "the SSE event must have triggered exactly one refresh dispatch",
      );

      const refreshedButtonEl = container.querySelector(
        "button",
      ) as HTMLButtonElement;
      assertExists(
        refreshedButtonEl,
        "the button must still be rendered after refresh",
      );

      // Click again: node-name-input is gone from the tracker (reconcile() ran
      // on the refreshed node list), so resolvePayloadFrom must fail close
      // (PAYLOAD_FROM_NODE_NOT_FOUND) — no 4th dispatch is ever captured, and
      // in particular no dispatch carrying the stale "Beta" value.
      simulateClick(refreshedButtonEl);
      // Give any (incorrect) fire-and-forget dispatch a chance to land before
      // asserting its absence.
      for (let i = 0; i < 10; i++) await flushUpdates();
      assertEquals(
        scenario.capturedDispatchBodies.length,
        3,
        "a click referencing a reconciled-away node must not produce a new dispatch call",
      );
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

Deno.test(
  "ProjectionShell (real mount): two input nodes keep independently tracked values, a surviving node's value persists across a real SSE refresh, and a fresh remount starts with no inherited values",
  async () => {
    ensureRuntimeComponentRegistryInitialized();
    schedulerTestOnly.resetCommandQueue();
    FakeEventSource.instances = [];

    const { container, cleanup } = setupDom();
    const originalEventSource =
      (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource =
      FakeEventSource;
    const originalFetch = globalThis.fetch;

    function twoInputEmission(orderShift = 0) {
      return {
        manifestId: ADMIN_ENUM_MANIFEST_ID,
        layoutId: "layout-projection-shell-multi-node-scenario",
        projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
        layoutNodes: [
          {
            nodeId: "node-input-a",
            nodeKind: "catalog_component",
            componentId: "comp-input-a-001",
            componentKind: "form_input/input",
            componentKey: "text_input.primitive",
            orderIndex: 0 + orderShift,
            runtimeInteractions: inputChangeSetStateInteraction(
              "node-submit-button",
            ),
          },
          {
            nodeId: "node-input-b",
            nodeKind: "catalog_component",
            componentId: "comp-input-b-001",
            componentKind: "form_input/input",
            componentKey: "text_input.primitive",
            orderIndex: 1 + orderShift,
            runtimeInteractions: inputChangeSetStateInteraction(
              "node-submit-button",
            ),
          },
          {
            nodeId: "node-submit-button",
            nodeKind: "catalog_component",
            componentId: "comp-submit-button-001",
            componentKind: "action/button",
            componentKey: "button.primitive",
            orderIndex: 2 + orderShift,
            wiringKind: "admin_runtime",
            targetSurface: "manifest",
            targetRef:
              `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:create_group`,
            dispatchPayloadFromByTrigger: {
              click: {
                fieldA: "node:node-input-a.value",
                fieldB: "node:node-input-b.value",
              },
            },
          },
        ],
      };
    }

    const scenario = buildMockScenario((callIndex) => {
      if (callIndex === 1 || callIndex === 3) {
        // callIndex 1: initial mount. callIndex 3: SSE-triggered refresh —
        // same layout shape (both nodes survive), simulating an ordinary
        // re-render-driving refresh rather than a node removal.
        return { success: true, emission: twoInputEmission() };
      }
      // callIndex 2/4: Lane 2 write dispatches — succeed normally.
      return { success: true, errors: [] };
    });
    globalThis.fetch = scenario.fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
      render(h(ProjectionShell, {}), container);

      let buttonEl: HTMLButtonElement | null = null;
      await waitFor(() => {
        buttonEl = container.querySelector("button");
        return buttonEl !== null;
      });
      const inputs = () =>
        Array.from(container.querySelectorAll("input")) as HTMLInputElement[];
      await waitFor(() => inputs().length === 2);
      assertEquals(inputs().length, 2);

      // Multi-node separation: typing into A must not affect B's tracked value.
      simulateInput(inputs()[0], "Alpha");
      await flushUpdates();
      simulateInput(inputs()[1], "Beta");
      await flushUpdates();

      simulateClick(buttonEl!);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 2);
      const firstWrite = scenario.capturedDispatchBodies[1]
        .payload as Record<string, unknown>;
      assertEquals(
        firstWrite,
        {
          fieldA: "Alpha",
          fieldB: "Beta",
          target_ref:
            `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:create_group`,
        },
        "two distinct input nodes must resolve into two distinct payload fields — no cross-node value bleed",
      );

      // Real SSE refresh — same two nodes survive (rerender, not removal).
      assertEquals(FakeEventSource.instances.length, 1);
      FakeEventSource.instances[0].emit(
        "projection",
        JSON.stringify({ manifest_id: ADMIN_ENUM_MANIFEST_ID }),
      );
      await waitFor(() => scenario.capturedDispatchBodies.length >= 3);
      assertEquals(scenario.capturedDispatchBodies.length, 3);

      // Click again WITHOUT retyping: both surviving nodes' tracked values must
      // still be present after the refresh/rerender — AND the re-rendered DOM
      // must display those SAME values (applyLiveNodeValueOverride in
      // renderEmission.ts promotes the live node value tracker to the display
      // authority too, not just the dispatch authority — closing the
      // divergence a raw emission-derived-default rerender would otherwise
      // reintroduce). What the user sees is what the next dispatch sends.
      const refreshedButton = container.querySelector(
        "button",
      ) as HTMLButtonElement;
      assertEquals(inputs().length, 2, "both nodes must still be rendered");
      assertEquals(
        inputs()[0].value,
        "Alpha",
        "the surviving input's DISPLAYED value must still show what the user typed, not reset to a placeholder, after a real SSE refresh",
      );
      assertEquals(inputs()[1].value, "Beta");
      simulateClick(refreshedButton);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 4);
      assertEquals(
        scenario.capturedDispatchBodies[3].payload as Record<
          string,
          unknown
        >,
        {
          fieldA: "Alpha",
          fieldB: "Beta",
          target_ref:
            `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:create_group`,
        },
        "a surviving node's tracked value must persist across a real SSE refresh + rerender, unchanged",
      );
    } finally {
      globalThis.fetch = originalFetch;
      (globalThis as unknown as { EventSource: unknown }).EventSource =
        originalEventSource;
      schedulerTestOnly.resetCommandQueue();
      render(null, container);
      cleanup();
    }

    // ── unmount / remount: a fresh mount must start with no inherited values ──
    const { container: container2, cleanup: cleanup2 } = setupDom();
    (globalThis as unknown as { EventSource: unknown }).EventSource =
      FakeEventSource;
    FakeEventSource.instances = [];
    const remountScenario = buildMockScenario((callIndex) => {
      if (callIndex === 1) {
        return { success: true, emission: twoInputEmission() };
      }
      return { success: true, errors: [] };
    });
    globalThis.fetch = remountScenario.fetch;
    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
      render(h(ProjectionShell, {}), container2);
      let buttonEl2: HTMLButtonElement | null = null;
      await waitFor(() => {
        buttonEl2 = container2.querySelector("button");
        return buttonEl2 !== null;
      });
      assertExists(buttonEl2);

      // No input() was ever simulated on this fresh mount/tracker — a click
      // must fail close (missing node value), never resolve to a PRIOR
      // mount's "Alpha"/"Beta", proving the tracker is per-mount, not a
      // module-level/global leak across unmount+remount.
      simulateClick(buttonEl2!);
      for (let i = 0; i < 10; i++) await flushUpdates();
      assertEquals(
        remountScenario.capturedDispatchBodies.length,
        1,
        "a fresh mount's click before any input must not produce a Lane 2 write dispatch at all — no inherited values from a prior mount",
      );
    } finally {
      globalThis.fetch = originalFetch;
      (globalThis as unknown as { EventSource: unknown }).EventSource =
        originalEventSource;
      schedulerTestOnly.resetCommandQueue();
      render(null, container2);
      cleanup2();
    }
  },
);

// ─── PR #600 review round 12: a node's own admin_runtime dispatch result was
// previously void-discarded (never adopted into production emission), so a
// load_button's dryRun pre-fill could never actually render, and re-Loading a
// DIFFERENT record risked leaving a STALE tracked value diverging from what
// was just displayed. This proves the fix end-to-end through a real
// ProjectionShell mount: Load(A) then Load(B) — B's value must appear in
// BOTH the rendered display AND the confirm dispatch's payload. ───────────

Deno.test(
  "ProjectionShell (real mount): Load(A) then Load(B) — the second Load's own dispatch result is adopted into production emission, and B's value appears in BOTH the re-rendered search_input display AND the Confirm click's dispatch payload (no stale A leak)",
  async () => {
    ensureRuntimeComponentRegistryInitialized();
    schedulerTestOnly.resetCommandQueue();
    FakeEventSource.instances = [];

    const { container, cleanup } = setupDom();
    const originalEventSource =
      (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource =
      FakeEventSource;
    const originalFetch = globalThis.fetch;

    function loadAndConfirmLayoutNodes() {
      return [
        {
          nodeId: "node-group-id-input",
          nodeKind: "catalog_component",
          componentId: "comp-group-id-input-001",
          componentKind: "form_input/input",
          componentKey: "text_input.primitive",
          orderIndex: 0,
          runtimeInteractions: inputChangeSetStateInteraction(
            "node-confirm-button",
          ),
        },
        {
          nodeId: "node-load-button",
          nodeKind: "catalog_component",
          componentId: "comp-load-button-001",
          componentKind: "action/button",
          componentKey: "button.primitive",
          orderIndex: 1,
          wiringKind: "admin_runtime",
          targetSurface: "manifest",
          targetRef:
            `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:update_group`,
          dispatchPayloadFromByTrigger: {
            click: {
              groupId: "node:node-group-id-input.value",
              dryRun: "literal:true",
            },
          },
        },
        {
          nodeId: "node-group-name-search",
          nodeKind: "catalog_component",
          componentId: "comp-group-name-search-001",
          componentKind: "form_input/search_input",
          componentKey: "search_input.primitive",
          orderIndex: 2,
          runtimeInteractions: inputChangeSetStateInteraction(
            "node-confirm-button",
          ),
          propBindings: {
            value: { source: "emission.data.preview.groupName" },
          },
        },
        {
          nodeId: "node-confirm-button",
          nodeKind: "catalog_component",
          componentId: "comp-confirm-button-001",
          componentKind: "action/button",
          componentKey: "button.primitive",
          orderIndex: 3,
          wiringKind: "admin_runtime",
          targetSurface: "manifest",
          targetRef:
            `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:update_group`,
          dispatchPayloadFromByTrigger: {
            click: {
              groupId: "node:node-group-id-input.value",
              groupName: "node:node-group-name-search.value",
              dryRun: "literal:false",
            },
          },
        },
      ];
    }

    function emissionWithPreview(groupName: string | undefined) {
      return {
        manifestId: ADMIN_ENUM_MANIFEST_ID,
        layoutId: "layout-projection-shell-load-then-load-scenario",
        projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
        layoutNodes: loadAndConfirmLayoutNodes(),
        data: groupName !== undefined ? { preview: { groupName } } : {},
      };
    }

    const scenario = buildMockScenario((callIndex) => {
      if (callIndex === 1) {
        // Initial mount: no record loaded yet, no preview data.
        return { success: true, emission: emissionWithPreview(undefined) };
      }
      if (callIndex === 2) {
        // Load(A)'s own Lane 2 dryRun dispatch result.
        return { success: true, emission: emissionWithPreview("record_a") };
      }
      if (callIndex === 4) {
        // Load(B)'s own Lane 2 dryRun dispatch result — a DIFFERENT record.
        return { success: true, emission: emissionWithPreview("record_b") };
      }
      // callIndex 3 and 5: Confirm clicks — succeed normally (no emission needed).
      return { success: true, errors: [] };
    });
    globalThis.fetch = scenario.fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
      render(h(ProjectionShell, {}), container);

      const buttons = () =>
        Array.from(
          container.querySelectorAll("button"),
        ) as HTMLButtonElement[];
      await waitFor(() => buttons().length === 2);
      const inputs = () =>
        Array.from(container.querySelectorAll("input")) as HTMLInputElement[];
      await waitFor(() => inputs().length === 2);
      assertEquals(inputs().length, 2);

      const [groupIdInput, groupNameSearchInput] = inputs();
      const [loadButton, confirmButton] = buttons();

      // ── Load(A) ──
      simulateInput(groupIdInput, "group-a-id");
      await flushUpdates();
      simulateClick(loadButton);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 2);
      assertEquals(scenario.capturedDispatchBodies.length, 2);

      // The load_button's own dispatch result must have been ADOPTED into
      // production emission — re-rendering the search_input with the
      // resolved preview.groupName, WITHOUT the user ever typing into it.
      await waitFor(() => inputs()[1].value === "record_a");
      assertEquals(
        inputs()[1].value,
        "record_a",
        "Load(A)'s dryRun preview must render into the search_input's displayed value (previously impossible — the dispatch response was void-discarded)",
      );

      simulateClick(confirmButton);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 3);
      assertEquals(
        (scenario.capturedDispatchBodies[2].payload as Record<
          string,
          unknown
        >).groupName,
        "record_a",
        "Confirm after Load(A) must dispatch record_a's name — display and dispatch payload must agree",
      );

      // ── Load(B): a DIFFERENT record ──
      simulateInput(groupIdInput, "group-b-id");
      await flushUpdates();
      simulateClick(loadButton);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 4);
      assertEquals(scenario.capturedDispatchBodies.length, 4);

      // The round-12-identified divergence bug: without forceOverwrite, the
      // tracker would still hold "record_a" (untouched-only seeding), so the
      // search_input would keep DISPLAYING "record_a" even though B was just
      // loaded. This must now show "record_b".
      await waitFor(() => inputs()[1].value === "record_b");
      assertEquals(
        inputs()[1].value,
        "record_b",
        "Load(B) must overwrite the stale record_a display with record_b — no display divergence on record switch",
      );

      simulateClick(confirmButton);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 5);
      assertEquals(
        (scenario.capturedDispatchBodies[4].payload as Record<
          string,
          unknown
        >).groupName,
        "record_b",
        "Confirm after Load(B) must dispatch record_b's name, NOT the stale record_a — display and dispatch payload must agree after a record switch",
      );
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

// ─── PR #600 round 20: enum_delete_group_button's payloadFrom sources groupId
// from "node:enum_table.value.groupId" — the tracked value of a DIFFERENT
// node (the table), not the button's own input. This requires two things this
// round added: (1) tableFactory's onRowClick now also supplies `value: row` so
// emitBoundEvent's existing, universal Lane 3 node-value tracking fires for a
// table row select the same way it already fires for every other component's
// change/input/select event, and (2) payloadFromResolver.ts's new
// node:<id>.value.<path> dotted-path extraction. Mounts the real production
// ProjectionShell + Table, simulates a genuine row click then a genuine
// button click, and asserts the delete dispatch carries the SELECTED ROW's
// own groupId — the real db/seed_empty.sql ae200 enum_table/
// enum_delete_group_button shape. ───────────────────────────────────────────

Deno.test(
  "ProjectionShell (real mount): clicking a table row tracks that row as the table node's own value, and a separate button's payloadFrom (node:<table>.value.<field>) reads a field off it (real db/seed_empty.sql ae200 enum_table/enum_delete_group_button shape, round 20)",
  async () => {
    ensureRuntimeComponentRegistryInitialized();
    schedulerTestOnly.resetCommandQueue();
    FakeEventSource.instances = [];

    const { container, cleanup } = setupDom();
    const originalEventSource =
      (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource =
      FakeEventSource;
    const originalFetch = globalThis.fetch;

    const AE230_DELETE_GROUP_TARGET_REF =
      "manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group";

    function tableAndDeleteButtonEmission() {
      return {
        manifestId: ADMIN_ENUM_MANIFEST_ID,
        layoutId: "layout-ae200-selected-row-carrier-scenario",
        projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
        layoutNodes: [
          {
            nodeId: "enum_table",
            nodeKind: "catalog_component",
            componentId: "comp-enum-table-001",
            componentKind: "data_display/table",
            componentKey: "table.primitive",
            orderIndex: 0,
            // The LAYOUT's own uniform binding (ae205's real target_ref, list_groups) — a row
            // select re-issues this same idempotent read, exactly like production.
            wiringKind: "admin_runtime",
            targetSurface: "manifest",
            targetRef:
              `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:list_groups`,
            // table:null forces tableFactory's fallback to the flat top-level props (the same
            // shape db/seed_empty.sql's real enum_table propsJson uses) instead of the
            // placeholder default's own nested `table` object.
            propsJson: JSON.stringify({
              table: null,
              columns: [
                { key: "groupId", header: "Group ID" },
                { key: "groupName", header: "Group name" },
              ],
              rows: [
                { groupId: "row-uuid-1", groupName: "Alpha" },
                { groupId: "row-uuid-2", groupName: "Beta" },
              ],
            }),
          },
          {
            nodeId: "enum_delete_group_button",
            nodeKind: "catalog_component",
            componentId: "comp-delete-group-button-001",
            componentKind: "action/button",
            componentKey: "button.primitive",
            orderIndex: 1,
            wiringKind: "admin_runtime",
            targetSurface: "manifest",
            targetRef:
              `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:list_groups`,
            // The node-local override (db/seed_empty.sql's real enum_delete_group_button tensor
            // node, round 20) — groupId sourced from enum_table's OWN tracked selected-row value,
            // NOT from any input this button itself owns.
            dispatchTargetRefByTrigger: {
              click: AE230_DELETE_GROUP_TARGET_REF,
            },
            dispatchPayloadFromByTrigger: {
              click: {
                groupId: "node:enum_table.value.groupId",
                confirmed: "literal:true",
              },
            },
          },
        ],
      };
    }

    const scenario = buildMockScenario((callIndex) => {
      if (callIndex === 1) {
        return { success: true, emission: tableAndDeleteButtonEmission() };
      }
      // callIndex 2: the row select's own list_groups re-dispatch (harmless, idempotent read).
      // callIndex 3: the delete button's override dispatch.
      return { success: true, errors: [] };
    });
    globalThis.fetch = scenario.fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());

      render(h(ProjectionShell, {}), container);

      let buttonEl: HTMLButtonElement | null = null;
      await waitFor(() => {
        buttonEl = container.querySelector("button");
        return buttonEl !== null;
      });
      assertExists(
        buttonEl,
        "enum_delete_group_button must have rendered from the initial dispatch's emission",
      );

      const rows = () =>
        Array.from(
          container.querySelectorAll("tbody tr"),
        ) as HTMLTableRowElement[];
      await waitFor(() => rows().length === 2);
      assertEquals(rows().length, 2);

      // Select the SECOND row — proves the tracked value reflects whichever row was
      // actually clicked, not just always the first.
      simulateRowClick(rows()[1]);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 2);
      assertEquals(
        scenario.capturedDispatchBodies.length,
        2,
        "the row select must have triggered enum_table's own list_groups re-dispatch",
      );

      simulateClick(buttonEl!);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 3);
      assertEquals(
        scenario.capturedDispatchBodies.length,
        3,
        "expected the row-select dispatch plus the delete button's override dispatch",
      );

      const deleteDispatchBody = scenario.capturedDispatchBodies[2];
      // Layer/action must reflect the OVERRIDE's own embedded layer:action
      // (enum_dictionary:delete_group), NOT the layout's own uniform binding
      // (enum_dictionary:list_groups).
      assertEquals(deleteDispatchBody.layer, "enum_dictionary");
      assertEquals(deleteDispatchBody.action, "delete_group");
      const deletePayload = deleteDispatchBody.payload as Record<
        string,
        unknown
      >;
      assertEquals(deletePayload.target_ref, AE230_DELETE_GROUP_TARGET_REF);
      // The SELECTED (second) row's own groupId — not the first row's, not a static value.
      assertEquals(deletePayload.groupId, "row-uuid-2");
      assertEquals(deletePayload.confirmed, "true");
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

Deno.test(
  "ProjectionShell (real mount): a button referencing node:<table>.value.<field> before any row has been selected fails close (PAYLOAD_FROM_NODE_NOT_FOUND) — no dispatch, never a silent undefined groupId",
  async () => {
    ensureRuntimeComponentRegistryInitialized();
    schedulerTestOnly.resetCommandQueue();
    FakeEventSource.instances = [];

    const { container, cleanup } = setupDom();
    const originalEventSource =
      (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource =
      FakeEventSource;
    const originalFetch = globalThis.fetch;

    const AE230_DELETE_GROUP_TARGET_REF =
      "manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group";

    const scenario = buildMockScenario((callIndex) => {
      if (callIndex === 1) {
        return {
          success: true,
          emission: {
            manifestId: ADMIN_ENUM_MANIFEST_ID,
            layoutId: "layout-ae200-no-selection-yet-scenario",
            projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
            layoutNodes: [
              {
                nodeId: "enum_table",
                nodeKind: "catalog_component",
                componentId: "comp-enum-table-001",
                componentKind: "data_display/table",
                componentKey: "table.primitive",
                orderIndex: 0,
                propsJson: JSON.stringify({
                  table: null,
                  columns: [{ key: "groupId", header: "Group ID" }],
                  rows: [{ groupId: "row-uuid-1" }],
                }),
                // No wiringKind on this node in THIS scenario — no eventBinding.select at all,
                // so a row click never fires onRowClick, and enum_table's own tracked value is
                // never seeded (no select ever occurred).
              },
              {
                nodeId: "enum_delete_group_button",
                nodeKind: "catalog_component",
                componentId: "comp-delete-group-button-001",
                componentKind: "action/button",
                componentKey: "button.primitive",
                orderIndex: 1,
                wiringKind: "admin_runtime",
                targetSurface: "manifest",
                targetRef:
                  `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:list_groups`,
                dispatchTargetRefByTrigger: {
                  click: AE230_DELETE_GROUP_TARGET_REF,
                },
                dispatchPayloadFromByTrigger: {
                  click: {
                    groupId: "node:enum_table.value.groupId",
                    confirmed: "literal:true",
                  },
                },
              },
            ],
          },
        };
      }
      return { success: true, errors: [] };
    });
    globalThis.fetch = scenario.fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
      render(h(ProjectionShell, {}), container);

      let buttonEl: HTMLButtonElement | null = null;
      await waitFor(() => {
        buttonEl = container.querySelector("button");
        return buttonEl !== null;
      });
      assertExists(buttonEl);

      simulateClick(buttonEl!);
      for (let i = 0; i < 10; i++) await flushUpdates();

      assertEquals(
        scenario.capturedDispatchBodies.length,
        1,
        "a click referencing an unselected table's tracked value must fail close (PAYLOAD_FROM_NODE_NOT_FOUND) with no second dispatch — never a silent undefined/null groupId sent to the backend",
      );
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

// ─── Round 25: delete_group's confirm dialog — production DOM containment +
// result-gated close proof (real db/seed_empty.sql ae200
// enum_delete_group_button/enum_delete_group_confirm_modal/
// enum_delete_group_confirm_button/enum_delete_group_cancel_button shape).
//
// Mounts the REAL ProjectionShell + LayoutProjectionTree + disclosure/modal +
// action/button components. Proves:
//   1. Confirm/Cancel are genuinely ABSENT from the DOM while closed (not merely
//      hidden) — components/LayoutProjectionTree.tsx embeds them inside the
//      Modal's own rendered subtree (its footer) via the generic
//      acceptsAuthoredChildren capability, and Modal.tsx returns null entirely
//      when closed, taking that whole subtree out of the DOM with it.
//   2. Delete opens the modal (no write); Cancel closes it (no write); Confirm
//      resolves groupId FRESH from enum_table's own tracked selected-row value
//      at click time.
//   3. A confirm click with no selection fails closed (no dispatch, modal stays
//      open) — resolvePayloadFrom's existing fail-close behavior.
//   4. The confirm dispatch's own localStateMutation (closeModal) is gated on
//      the dispatch actually SETTLING successfully — enqueue alone must not
//      close the modal ahead of the real backend result.
// ───────────────────────────────────────────────────────────────────────────

const AE230_DELETE_GROUP_TARGET_REF =
  "manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group";

function enumDeleteGroupConfirmModalLayoutNodes(rows: Record<string, unknown>[]) {
  return [
    {
      nodeId: "enum_table",
      nodeKind: "catalog_component",
      componentId: "comp-enum-table-001",
      componentKind: "data_display/table",
      componentKey: "table.primitive",
      orderIndex: 0,
      wiringKind: "admin_runtime",
      targetSurface: "manifest",
      targetRef: `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:list_groups`,
      propsJson: JSON.stringify({
        table: null,
        columns: [
          { key: "groupId", header: "Group ID" },
          { key: "groupName", header: "Group name" },
        ],
        rows,
      }),
    },
    {
      nodeId: "enum_delete_group_button",
      nodeKind: "catalog_component",
      componentId: "comp-delete-group-button-001",
      componentKind: "action/button",
      componentKey: "button.primitive",
      orderIndex: 1,
      // No wiringKind/targetRef/dispatch fields at all — the visible trigger carries no write
      // authority of its own; it can only open the modal (round 25).
      runtimeInteractions: [
        {
          trigger: "click",
          actionType: "openModal",
          targetNodeId: "enum_delete_group_confirm_modal",
          statePath: "open",
        },
      ],
    },
    {
      nodeId: "enum_delete_group_confirm_modal",
      nodeKind: "catalog_component",
      // componentId is this node's own resolved NodeId (round 25 fix) — required non-empty by
      // adaptComponentDataHub, never a ui_component_registry id (Modal is a built-in primitive).
      componentId: "enum_delete_group_confirm_modal",
      componentKind: "disclosure/modal",
      componentKey: "modal.template",
      orderIndex: 2,
      // Round 25 finding: buildLayoutPreviewPlaceholderProps's "disclosure/modal" default is
      // { data: { open: true, title: "Modal", body: "プレビュー" } } (open:true — a UI-Builder
      // CANVAS-preview convenience, so an author can see the modal while designing). renderEmission
      // mergeNodeLocalProps does a SHALLOW top-level merge of propsJson onto props — a flat
      // { title, body } here would add new TOP-LEVEL keys while leaving that default's OWN nested
      // `data` object (still holding open:true) completely untouched, so modalFactory (which reads
      // props.data when it is an object) would keep reading the untouched placeholder, never this
      // flat title/body. propsJson must itself supply the WHOLE `data` object, closed with
      // open:false, exactly like every other disclosure/modal seed record needs to.
      propsJson: JSON.stringify({
        data: {
          open: false,
          title: "Delete group",
          body:
            "This will permanently delete the selected enum group and its items. This cannot be undone.",
        },
      }),
      // Self-close-on-toggle — modalFactory's requireBinding(spec, "toggle") fails the whole
      // render closed without this (the native backdrop/✕-close affordance).
      runtimeInteractions: [
        {
          trigger: "toggle",
          actionType: "closeModal",
          targetNodeId: "enum_delete_group_confirm_modal",
          statePath: "open",
        },
      ],
    },
    {
      nodeId: "enum_delete_group_confirm_button",
      nodeKind: "catalog_component",
      componentId: "comp-confirm-button-001",
      componentKind: "action/button",
      componentKey: "button.primitive",
      orderIndex: 0,
      parentNodeId: "enum_delete_group_confirm_modal",
      wiringKind: "admin_runtime",
      targetSurface: "manifest",
      targetRef: `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:list_groups`,
      dispatchTargetRefByTrigger: { click: AE230_DELETE_GROUP_TARGET_REF },
      dispatchPayloadFromByTrigger: {
        click: {
          groupId: "node:enum_table.value.groupId",
          confirmed: "literal:true",
        },
      },
      // Secondary disclosure action (round 24/25): closes the SAME modal on the SAME click,
      // gated on the dispatch above actually settling successfully (round 25 fix).
      runtimeInteractions: [
        {
          trigger: "click",
          actionType: "closeModal",
          targetNodeId: "enum_delete_group_confirm_modal",
          statePath: "open",
        },
      ],
    },
    {
      nodeId: "enum_delete_group_cancel_button",
      nodeKind: "catalog_component",
      componentId: "comp-cancel-button-001",
      componentKind: "action/button",
      componentKey: "button.primitive",
      orderIndex: 1,
      parentNodeId: "enum_delete_group_confirm_modal",
      // No dispatch fields at all — Cancel sends no write, ever.
      runtimeInteractions: [
        {
          trigger: "click",
          actionType: "closeModal",
          targetNodeId: "enum_delete_group_confirm_modal",
          statePath: "open",
        },
      ],
    },
  ];
}

function queryDialog(container: Element) {
  return container.querySelector('[role="dialog"]');
}

function queryConfirmButton(container: Element) {
  return container.querySelector(
    '[data-node-id="enum_delete_group_confirm_button"] button',
  ) as HTMLButtonElement | null;
}

function queryCancelButton(container: Element) {
  return container.querySelector(
    '[data-node-id="enum_delete_group_cancel_button"] button',
  ) as HTMLButtonElement | null;
}

Deno.test(
  "ProjectionShell (real mount): delete_group's confirm modal — closed by default, Confirm/Cancel are genuinely absent from the DOM (not merely hidden), and Delete opens it",
  async () => {
    ensureRuntimeComponentRegistryInitialized();
    schedulerTestOnly.resetCommandQueue();
    FakeEventSource.instances = [];
    const { container, cleanup } = setupDom();
    const originalEventSource =
      (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource =
      FakeEventSource;
    const originalFetch = globalThis.fetch;

    const scenario = buildMockScenario((callIndex) => {
      if (callIndex === 1) {
        return {
          success: true,
          emission: {
            manifestId: ADMIN_ENUM_MANIFEST_ID,
            layoutId: "layout-ae200-confirm-modal-containment-scenario",
            projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
            layoutNodes: enumDeleteGroupConfirmModalLayoutNodes([
              { groupId: "row-uuid-1", groupName: "Alpha" },
            ]),
          },
        };
      }
      return { success: true, errors: [] };
    });
    globalThis.fetch = scenario.fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
      render(h(ProjectionShell, {}), container);

      let deleteButtonEl: HTMLButtonElement | null = null;
      await waitFor(() => {
        deleteButtonEl = container.querySelector(
          '[data-node-id="enum_delete_group_button"] button',
        );
        return deleteButtonEl !== null;
      });
      assertExists(deleteButtonEl, "the Delete trigger button must have rendered");

      // Closed by default: no dialog role, and Confirm/Cancel are not anywhere in the DOM.
      // Boolean assertions (not assertEquals) — assertEquals's failure-path diff formatter can
      // hang attempting to serialize a live (happy-dom) DOM Element with circular parent/child
      // references; a plain boolean comparison never needs to format the element at all.
      assert(queryDialog(container) === null, "modal must be closed by default");
      assert(
        queryConfirmButton(container) === null,
        "Confirm must not exist in the DOM while the modal is closed",
      );
      assert(
        queryCancelButton(container) === null,
        "Cancel must not exist in the DOM while the modal is closed",
      );

      simulateClick(deleteButtonEl!);
      await flushUpdates();

      assertExists(queryDialog(container), "Delete must open the modal");
      assertExists(
        queryConfirmButton(container),
        "Confirm must now exist, nested inside the open modal",
      );
      assertExists(
        queryCancelButton(container),
        "Cancel must now exist, nested inside the open modal",
      );
      assertEquals(
        scenario.capturedDispatchBodies.length,
        1,
        "opening the modal must send no write dispatch — only the initial entry dispatch so far",
      );
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

Deno.test(
  "ProjectionShell (real mount): delete_group's confirm modal — Cancel sends no write and closes; reopening then Confirming resolves groupId fresh from the selected row and stays gated on backend success",
  async () => {
    ensureRuntimeComponentRegistryInitialized();
    schedulerTestOnly.resetCommandQueue();
    FakeEventSource.instances = [];
    const { container, cleanup } = setupDom();
    const originalEventSource =
      (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource =
      FakeEventSource;
    const originalFetch = globalThis.fetch;

    const scenario = buildMockScenario((callIndex) => {
      if (callIndex === 1) {
        return {
          success: true,
          emission: {
            manifestId: ADMIN_ENUM_MANIFEST_ID,
            layoutId: "layout-ae200-confirm-modal-result-gating-scenario",
            projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
            layoutNodes: enumDeleteGroupConfirmModalLayoutNodes([
              { groupId: "row-uuid-1", groupName: "Alpha" },
              { groupId: "row-uuid-2", groupName: "Beta" },
            ]),
          },
        };
      }
      // The confirm dispatch (only dispatch after the initial entry — row selects reuse
      // enum_table's own tracked value locally, no server round trip in this scenario).
      return { success: true, errors: [] };
    });
    globalThis.fetch = scenario.fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
      render(h(ProjectionShell, {}), container);

      let deleteButtonEl: HTMLButtonElement | null = null;
      await waitFor(() => {
        deleteButtonEl = container.querySelector(
          '[data-node-id="enum_delete_group_button"] button',
        );
        return deleteButtonEl !== null;
      });

      // --- Cancel: open, cancel, no write, closes ---
      simulateClick(deleteButtonEl!);
      await flushUpdates();
      assertExists(queryDialog(container), "Delete must open the modal");
      const cancelButtonEl = queryCancelButton(container);
      assertExists(cancelButtonEl, "Cancel must be present while open");
      simulateClick(cancelButtonEl!);
      await flushUpdates();
      assert(queryDialog(container) === null, "Cancel must close the modal");
      assert(
        queryConfirmButton(container) === null,
        "Confirm must be gone from the DOM again after Cancel closes the modal",
      );
      assertEquals(
        scenario.capturedDispatchBodies.length,
        1,
        "Cancel must never send a write dispatch — only the initial entry dispatch so far",
      );

      // --- Reopen, select the SECOND row, Confirm: fresh groupId, gated close ---
      simulateClick(deleteButtonEl!);
      await flushUpdates();
      assertExists(queryDialog(container), "Delete must be able to reopen the modal");

      const rows = () =>
        Array.from(container.querySelectorAll("tbody tr")) as HTMLTableRowElement[];
      await waitFor(() => rows().length === 2);
      simulateRowClick(rows()[1]);
      await flushUpdates();

      // enum_table's OWN admin_runtime binding re-issues the layout's uniform list_groups read on
      // select (same as the round 20 row-select test above) — capturedDispatchBodies[1] is THAT
      // reissue, not the Confirm dispatch.
      await waitFor(() => scenario.capturedDispatchBodies.length >= 2);

      const confirmButtonEl = queryConfirmButton(container);
      assertExists(confirmButtonEl, "Confirm must still be present after selecting a row");
      simulateClick(confirmButtonEl!);

      await waitFor(() => scenario.capturedDispatchBodies.length >= 3);
      const confirmDispatchBody = scenario.capturedDispatchBodies[2];
      assertEquals(confirmDispatchBody.layer, "enum_dictionary");
      assertEquals(confirmDispatchBody.action, "delete_group");
      const confirmPayload = confirmDispatchBody.payload as Record<string, unknown>;
      assertEquals(confirmPayload.target_ref, AE230_DELETE_GROUP_TARGET_REF);
      // The SELECTED (second) row's own groupId, resolved fresh at click time.
      assertEquals(confirmPayload.groupId, "row-uuid-2");
      assertEquals(confirmPayload.confirmed, "true");

      // The modal must close only AFTER the dispatch settles successfully — never merely on
      // enqueue. Await the async .then() chain (dispatchRuntimeComponentCommandAndForwardResult)
      // to actually run before asserting the closed state.
      await waitFor(() => queryDialog(container) === null);
      assert(
        queryConfirmButton(container) === null,
        "Confirm must be gone from the DOM once the modal has closed on successful settlement",
      );
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

Deno.test(
  "ProjectionShell (real mount): delete_group's confirm modal — Confirm with no row selected fails closed (no dispatch, modal stays open); a backend failure result never closes the modal either",
  async () => {
    ensureRuntimeComponentRegistryInitialized();
    schedulerTestOnly.resetCommandQueue();
    FakeEventSource.instances = [];
    const { container, cleanup } = setupDom();
    const originalEventSource =
      (globalThis as unknown as { EventSource?: unknown }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource =
      FakeEventSource;
    const originalFetch = globalThis.fetch;

    // Second real dispatch (the confirm click after a row IS selected) settles as a genuine
    // backend failure — never a network/queue rejection — proving a failed write is not
    // mistaken for success.
    const scenario = buildMockScenario((callIndex) => {
      if (callIndex === 1) {
        return {
          success: true,
          emission: {
            manifestId: ADMIN_ENUM_MANIFEST_ID,
            layoutId: "layout-ae200-confirm-modal-failure-scenario",
            projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
            layoutNodes: enumDeleteGroupConfirmModalLayoutNodes([
              { groupId: "row-uuid-1", groupName: "Alpha" },
            ]),
          },
        };
      }
      return {
        success: false,
        errors: [{ code: "ENUM_GROUP_REFERENCED", message: "group is referenced" }],
      };
    });
    globalThis.fetch = scenario.fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
      render(h(ProjectionShell, {}), container);

      let deleteButtonEl: HTMLButtonElement | null = null;
      await waitFor(() => {
        deleteButtonEl = container.querySelector(
          '[data-node-id="enum_delete_group_button"] button',
        );
        return deleteButtonEl !== null;
      });

      // --- No selection: Confirm click fails closed ---
      simulateClick(deleteButtonEl!);
      await flushUpdates();
      const confirmButtonNoSelection = queryConfirmButton(container);
      assertExists(confirmButtonNoSelection, "Confirm must be present after opening");
      simulateClick(confirmButtonNoSelection!);
      for (let i = 0; i < 10; i++) await flushUpdates();

      assertEquals(
        scenario.capturedDispatchBodies.length,
        1,
        "a Confirm click with no selection must fail close (PAYLOAD_FROM_NODE_NOT_FOUND) — no second dispatch",
      );
      assertExists(
        queryDialog(container),
        "the modal must remain open when the payloadFrom resolution failed — never silently closed",
      );

      // --- Select a row, Confirm, backend responds success:false ---
      const rows = () =>
        Array.from(container.querySelectorAll("tbody tr")) as HTMLTableRowElement[];
      await waitFor(() => rows().length === 1);
      simulateRowClick(rows()[0]);
      await flushUpdates();

      const confirmButtonEl = queryConfirmButton(container);
      assertExists(confirmButtonEl, "Confirm must still be present");
      simulateClick(confirmButtonEl!);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 2);

      // Give the async .then() chain every chance to run before asserting nothing closed.
      for (let i = 0; i < 10; i++) await flushUpdates();
      assertExists(
        queryDialog(container),
        "a backend failure result must never close the modal — a failed write is not completion",
      );
      assertExists(
        queryConfirmButton(container),
        "Confirm must still be present after a failed write — the user can retry or cancel",
      );
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
