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
import {
  enqueueRuntimeComponentCommand,
  __testOnly as schedulerTestOnly,
} from "../runtime/frontendScheduler.ts";
import { __testOnly as runtimeComponentFactoryTestOnly } from "../runtime/runtimeComponentFactory.ts";
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

/**
 * Round 3 (preview-gap audit, settlement authority): production ManifestDispatcher always
 * resolves an Emission for any manifest-resolved dispatch (see api/dispatch.ts's Emission.manifestId
 * doc comment) — a real backend response never carries success:true with no emission at all. Before
 * this round, most of this file's fallback mock responses were the unrealistic
 * `{ success: true, errors: [] }` (no emission), which the OLD Modal-mutation gate tolerated because
 * it only checked result.success. The new shared settlement authority
 * (runtime/runtimeDispatchSettlement.ts's resolveRuntimeDispatchSettlement, used by BOTH
 * ProjectionShell's handleRuntimeDispatchResult and runtimeComponentFactory's deferred
 * localStateMutation gate) requires a present Emission to accept a settled result — matching real
 * backend behavior — so every fallback response a test does not otherwise care about must now carry
 * a plausible Emission too, or it would (correctly) fail settlement and block Modal open/close.
 * manifestId is derived from the REQUEST's own payload.target_ref.
 *
 * Scoped to CROSS-manifest (child) dispatches only — a preview/confirm admin_runtime override
 * always targets a dedicated child manifest (ae210/ae220/ae230/...), never ae200 itself. A
 * same-manifest or no-target_ref call (e.g. enum_table's own row-select list_groups re-read, or
 * ae200's own canonical-reread redispatch) is deliberately left at the pre-existing bare
 * `{success:true, errors:[]}` shape: round_27_28_settled_child_dispatch_result_authority's own
 * comment already documents that a same-manifest result through this handler is not the shape any
 * seeded write override in practice produces, and round-3's settlement-authority fix is scoped to
 * the cross-manifest preview/confirm Modal-mutation gate, not to this file's incidental same-manifest
 * read-circuit calls; giving those calls a synthesized Emission would make ProjectionShell's SEPARATE
 * same-manifest direct-adoption path (confirmProjectionEntryEmission + setEmission/setSpecs) replace
 * the already-rendered DOM tree with the synthesized emission's own (empty) layoutNodes, which is a
 * fixture-realism regression this file's other assertions do not expect.
 */
function extractManifestIdFromRequestBody(body: Record<string, unknown>): string | undefined {
  const payload = body.payload as Record<string, unknown> | undefined;
  const targetRef = payload?.target_ref;
  if (typeof targetRef !== "string") return undefined;
  const match = /^manifest:([^:]+):/.exec(targetRef);
  return match ? match[1] : undefined;
}

function fallbackDispatchResponse(
  body: Record<string, unknown>,
): Record<string, unknown> {
  const manifestId = extractManifestIdFromRequestBody(body);
  if (!manifestId || manifestId === ADMIN_ENUM_MANIFEST_ID) {
    return { success: true, errors: [] };
  }
  return {
    success: true,
    errors: [],
    emission: {
      manifestId,
      layoutId: "layout-fallback-generic-emission",
      projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
      layoutNodes: [],
    },
  };
}

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

    const scenario = buildMockScenario((callIndex, body) => {
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
      return fallbackDispatchResponse(body);
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

    const scenario = buildMockScenario((callIndex, body) => {
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
      return fallbackDispatchResponse(body);
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

    const scenario = buildMockScenario((callIndex, body) => {
      if (callIndex === 1) return { success: true, emission: initialEmission };
      if (callIndex === 3) {
        return { success: true, emission: refreshedEmission };
      }
      // callIndex 2 (first click's Lane 2 write) — succeeds normally.
      return fallbackDispatchResponse(body);
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

    const scenario = buildMockScenario((callIndex, body) => {
      if (callIndex === 1 || callIndex === 3) {
        // callIndex 1: initial mount. callIndex 3: SSE-triggered refresh —
        // same layout shape (both nodes survive), simulating an ordinary
        // re-render-driving refresh rather than a node removal.
        return { success: true, emission: twoInputEmission() };
      }
      // callIndex 2/4: Lane 2 write dispatches — succeed normally.
      return fallbackDispatchResponse(body);
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
    const remountScenario = buildMockScenario((callIndex, body) => {
      if (callIndex === 1) {
        return { success: true, emission: twoInputEmission() };
      }
      return fallbackDispatchResponse(body);
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

    const scenario = buildMockScenario((callIndex, body) => {
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
      return fallbackDispatchResponse(body);
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

    const scenario = buildMockScenario((callIndex, body) => {
      if (callIndex === 1) {
        return { success: true, emission: tableAndDeleteButtonEmission() };
      }
      // callIndex 2: the row select's own list_groups re-dispatch (harmless, idempotent read).
      // callIndex 3: the delete button's override dispatch.
      return fallbackDispatchResponse(body);
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
      // Round 3 (preview-gap audit, settlement authority): the confirmed write's settled result
      // now carries a realistic Emission (manifestId ae230, matching its own authored target_ref)
      // instead of the old fixture's unrealistic no-emission fallback — so
      // round_27_28_settled_child_dispatch_result_authority's OWN documented behavior (a settled
      // cross-manifest write's success re-reads ae200's own canonical identity) now actually
      // fires, adding a 4th dispatch (the canonical reread) this test does not otherwise inspect.
      await waitFor(() => scenario.capturedDispatchBodies.length >= 4);
      assertEquals(
        scenario.capturedDispatchBodies.length,
        4,
        "expected the row-select dispatch, the delete button's override dispatch, and the " +
          "confirmed write's own canonical-reread redispatch",
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

    const scenario = buildMockScenario((callIndex, body) => {
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
      return fallbackDispatchResponse(body);
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
      // preview-gap round: the visible trigger now dispatches a non-mutating dryRun preview to
      // the SAME ae230 target the Confirm button writes to (groupId re-resolved fresh from
      // enum_table's own tracked selected-row value) -- the modal only opens once that preview
      // settles successfully (deferred openModal below), matching real db/seed_empty.sql shape.
      wiringKind: "admin_runtime",
      targetSurface: "manifest",
      targetRef: `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:list_groups`,
      dispatchTargetRefByTrigger: { click: AE230_DELETE_GROUP_TARGET_REF },
      dispatchPayloadFromByTrigger: {
        click: {
          groupId: "node:enum_table.value.groupId",
          dryRun: "literal:true",
        },
      },
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

    const scenario = buildMockScenario((callIndex, body) => {
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
      return fallbackDispatchResponse(body);
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

      // preview-gap round: delete_group's preview ALSO needs groupId, so a row must be
      // selected before Delete can resolve its payloadFrom and dispatch at all. Selecting a
      // row itself re-issues enum_table's own admin_runtime list_groups read (a SEPARATE
      // dispatch from the preview below) — wait for it to land first.
      const rowsForOpen = () =>
        Array.from(container.querySelectorAll("tbody tr")) as HTMLTableRowElement[];
      await waitFor(() => rowsForOpen().length === 1);
      simulateRowClick(rowsForOpen()[0]);
      await waitFor(() => scenario.capturedDispatchBodies.length >= 2);
      const dispatchCountAfterRowSelect = scenario.capturedDispatchBodies.length;

      simulateClick(deleteButtonEl!);
      // preview-gap round: Delete now dispatches a non-mutating dryRun preview before the modal
      // opens (this scenario's fallback mock response succeeds it) — wait for the async open.
      await waitFor(() => queryDialog(container) !== null);

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
        dispatchCountAfterRowSelect + 1,
        "opening the modal must send exactly one non-mutating dryRun preview dispatch (never a write) beyond the row-select's own dispatch",
      );
      assertEquals(
        (scenario.capturedDispatchBodies[dispatchCountAfterRowSelect]
          .payload as Record<string, unknown> | undefined)
          ?.dryRun,
        "true",
        "the dispatch that opened the modal must be a dryRun preview, not a write",
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

    const scenario = buildMockScenario((callIndex, body) => {
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
      return fallbackDispatchResponse(body);
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

      // preview-gap round: delete_group's preview ALSO needs groupId, so a row must be
      // selected before Delete can resolve its payloadFrom and dispatch at all.
      const rowsForFirstOpen = () =>
        Array.from(container.querySelectorAll("tbody tr")) as HTMLTableRowElement[];
      await waitFor(() => rowsForFirstOpen().length === 2);
      simulateRowClick(rowsForFirstOpen()[0]);
      await flushUpdates();

      // --- Cancel: open (preview-gated), cancel, no write, closes ---
      simulateClick(deleteButtonEl!);
      // preview-gap round: Delete now dispatches a dryRun preview before the modal opens.
      await waitFor(() => queryDialog(container) !== null);
      assertExists(queryDialog(container), "Delete must open the modal");
      const cancelButtonEl = queryCancelButton(container);
      assertExists(cancelButtonEl, "Cancel must be present while open");
      const dispatchCountAfterFirstOpen = scenario.capturedDispatchBodies.length;
      simulateClick(cancelButtonEl!);
      await flushUpdates();
      assert(queryDialog(container) === null, "Cancel must close the modal");
      assert(
        queryConfirmButton(container) === null,
        "Confirm must be gone from the DOM again after Cancel closes the modal",
      );
      assertEquals(
        scenario.capturedDispatchBodies.length,
        dispatchCountAfterFirstOpen,
        "Cancel must never send a write dispatch beyond what opening the modal already dispatched (the preview)",
      );

      // --- Reopen (a fresh preview), select the SECOND row, Confirm: fresh groupId, gated close ---
      simulateClick(deleteButtonEl!);
      await waitFor(() => queryDialog(container) !== null);
      assertExists(queryDialog(container), "Delete must be able to reopen the modal");
      const dispatchCountAfterReopen = scenario.capturedDispatchBodies.length;

      const rows = () =>
        Array.from(container.querySelectorAll("tbody tr")) as HTMLTableRowElement[];
      await waitFor(() => rows().length === 2);
      simulateRowClick(rows()[1]);
      await flushUpdates();

      // enum_table's OWN admin_runtime binding re-issues the layout's uniform list_groups read on
      // select (same as the round 20 row-select test above) — this reissue is a SEPARATE dispatch
      // from both the preview above and the Confirm dispatch below.
      await waitFor(() => scenario.capturedDispatchBodies.length >= dispatchCountAfterReopen + 1);

      const confirmButtonEl = queryConfirmButton(container);
      assertExists(confirmButtonEl, "Confirm must still be present after selecting a row");
      const dispatchCountBeforeConfirm = scenario.capturedDispatchBodies.length;
      simulateClick(confirmButtonEl!);

      await waitFor(() => scenario.capturedDispatchBodies.length >= dispatchCountBeforeConfirm + 1);
      const confirmDispatchBody = scenario.capturedDispatchBodies[dispatchCountBeforeConfirm];
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
  "ProjectionShell (real mount): delete_group's confirm modal — Delete with no row selected fails closed BEFORE the preview even dispatches (payloadFrom resolution, no network call, modal never opens); once open, a backend failure on Confirm never closes the modal either",
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

    // preview-gap round: the preview dispatch (dryRun) always succeeds in this scenario so the
    // modal can open; the CONFIRMED write dispatch settles as a genuine backend failure — never
    // a network/queue rejection — proving a failed write is not mistaken for success.
    const scenario = buildMockScenario((callIndex, body) => {
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
      const payload = (body.payload ?? {}) as Record<string, unknown>;
      if (payload.dryRun === "true") {
        return { success: true, emission: { manifestId: "00000000-0000-0000-0000-0000000ae230", data: { ok: true } } };
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

      // --- No selection: Delete click itself fails closed -- the preview ALSO needs groupId,
      // so payloadFrom resolution fails before any network call, and the modal never opens. ---
      simulateClick(deleteButtonEl!);
      for (let i = 0; i < 10; i++) await flushUpdates();

      assertEquals(
        scenario.capturedDispatchBodies.length,
        1,
        "Delete with no row selected must fail close (PAYLOAD_FROM_NODE_NOT_FOUND) on the preview itself — no dispatch at all",
      );
      assert(
        queryDialog(container) === null,
        "the modal must never open when the preview's own payloadFrom resolution failed",
      );

      // --- Select a row, Delete (preview succeeds, modal opens), Confirm, backend responds success:false ---
      const rows = () =>
        Array.from(container.querySelectorAll("tbody tr")) as HTMLTableRowElement[];
      await waitFor(() => rows().length === 1);
      simulateRowClick(rows()[0]);
      await flushUpdates();

      simulateClick(deleteButtonEl!);
      await waitFor(() => queryDialog(container) !== null);

      const confirmButtonEl = queryConfirmButton(container);
      assertExists(confirmButtonEl, "Confirm must be present once the preview opened the modal");
      const dispatchCountBeforeConfirm = scenario.capturedDispatchBodies.length;
      simulateClick(confirmButtonEl!);
      await waitFor(() => scenario.capturedDispatchBodies.length >= dispatchCountBeforeConfirm + 1);

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

Deno.test(
  "ProjectionShell (real mount, round 27 owner decision): a successful child-manifest write's own Emission is never adopted into ae200's projection state — ae200 is re-dispatched with its OWN identity afterward, and only THAT response's data is rendered",
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

    // A child-manifest emission that, if ever wrongly adopted, would be trivially detectable —
    // it carries a manifestId belonging to ae230 (delete_group's own dedicated write manifest,
    // NOT ae200) and a data shape (a bare {ok, groupId} write-result) that looks nothing like
    // ae200's own list_groups table rows.
    const CHILD_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae230";
    const CANARY_CHILD_DATA = { ok: true, groupId: "row-uuid-1" };

    // ae200's own re-dispatch (round 27's redispatch, replaying the SAME entry axes
    // resolveProjectionEntryAxes produced for the initial mount: layer="screen_list",
    // action="Search") must be distinguishable from both the confirm dispatch (layer/action =
    // enum_dictionary/delete_group) and the row-select reissue (enum_dictionary/list_groups).
    let redispatchCount = 0;
    const scenario = buildMockScenario((callIndex, body) => {
      const layer = body.layer as string | undefined;
      const action = body.action as string | undefined;
      if (callIndex === 1) {
        return {
          success: true,
          emission: {
            manifestId: ADMIN_ENUM_MANIFEST_ID,
            layoutId: "layout-ae200-round27-redispatch-scenario",
            projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
            layoutNodes: enumDeleteGroupConfirmModalLayoutNodes([
              { groupId: "row-uuid-1", groupName: "Alpha" },
            ]),
          },
        };
      }
      if (layer === "enum_dictionary" && action === "delete_group") {
        // The confirm dispatch itself: a genuine child-manifest response, WITH an emission (the
        // real AdminRuntimeDispatchAdapter always wraps a successful data result in one — see
        // backend/runtime/AdminRuntimeDispatchAdapter.cs), but that emission's manifestId is the
        // CHILD's own (ae230), which confirmProjectionEntryEmission's adoptedManifestId guard
        // must reject.
        return {
          success: true,
          emission: {
            manifestId: CHILD_MANIFEST_ID,
            data: CANARY_CHILD_DATA,
          },
        };
      }
      if (layer === "screen_list" && action === "Search") {
        // ae200's own re-dispatch (round 27) — a FRESH list_groups-shaped read, reflecting the
        // group having been deleted (empty rows), and the SAME ae200 manifestId.
        redispatchCount++;
        return {
          success: true,
          emission: {
            manifestId: ADMIN_ENUM_MANIFEST_ID,
            layoutId: "layout-ae200-round27-redispatch-scenario",
            projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
            layoutNodes: enumDeleteGroupConfirmModalLayoutNodes([]),
          },
        };
      }
      // Row-select reissue (enum_dictionary/list_groups) — irrelevant to this test, no-op.
      return fallbackDispatchResponse(body);
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

      // preview-gap round: Delete's own preview also needs the selected row's groupId, so select
      // FIRST, then click Delete — its preview dispatch (also layer=enum_dictionary/
      // action=delete_group, so it ALSO receives the child-manifest canary response below) must
      // settle as a no-op (context.dryRun) before the modal opens, never adopted, never
      // redispatching ae200's own identity on its own.
      const rows = () =>
        Array.from(container.querySelectorAll("tbody tr")) as HTMLTableRowElement[];
      await waitFor(() => rows().length === 1);
      simulateRowClick(rows()[0]);
      await flushUpdates();

      simulateClick(deleteButtonEl!);
      await waitFor(() => queryDialog(container) !== null);
      assertEquals(
        redispatchCount,
        0,
        "the preview's own settled success (even with a cross-manifest canary emission) must never trigger ae200's canonical redispatch",
      );

      const confirmButtonEl = queryConfirmButton(container);
      assertExists(confirmButtonEl, "Confirm must be present");
      simulateClick(confirmButtonEl!);

      // Wait for the round-27 redispatch to actually fire and settle.
      await waitFor(() => redispatchCount >= 1);
      await flushUpdates();

      // The child response's own canary data must never appear anywhere reachable from the
      // rendered emission — the only proof surface available here is that the redispatch fired
      // with the CORRECT (ae200-owned) identity and that the table now reflects ITS data (the
      // deleted group's row is gone), not any state derived from the child's {ok, groupId} shape.
      // The table's empty state renders an explicit "No data." placeholder row rather than
      // zero <tr> elements, so absence is checked by content, not row count.
      await waitFor(() =>
        !(container.querySelector("tbody")?.textContent ?? "").includes("Alpha")
      );
      const tbodyText = container.querySelector("tbody")?.textContent ?? "";
      assert(
        !tbodyText.includes("Alpha"),
        "after the round-27 redispatch, ae200's own re-read must show the group gone, not the child's own {ok, groupId} canary data",
      );
      assertEquals(
        redispatchCount,
        1,
        "exactly one ae200 redispatch must fire for the one settled child write",
      );

      // The modal still closes once the write settled successfully (round 25 behavior,
      // unaffected by round 27 — closing is driven by the settled RESULT, never by which
      // response ended up adopted into projection state).
      await waitFor(() => queryDialog(container) === null);
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
  "ProjectionShell (real mount, round 28): a failed ae200 canonical reread (after a settled write succeeded) surfaces an explicit, non-destructive warning — retains the old DOM, never resends the write, never reopens the modal",
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

    const CHILD_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae230";

    // The initial mount's own screen_list/Search load must succeed (it is the baseline the
    // test's own assertions depend on); only the SUBSEQUENT redispatch (triggered by the
    // settled write) is made to fail, isolating the redispatch-failure behavior specifically.
    let screenListSearchCount = 0;
    const scenario = buildMockScenario((_callIndex, body) => {
      const layer = body.layer as string | undefined;
      const action = body.action as string | undefined;
      if (layer === "screen_list" && action === "Search") {
        screenListSearchCount++;
        if (screenListSearchCount === 1) {
          return {
            success: true,
            emission: {
              manifestId: ADMIN_ENUM_MANIFEST_ID,
              layoutId: "layout-ae200-round28-redispatch-failure-scenario",
              projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
              layoutNodes: enumDeleteGroupConfirmModalLayoutNodes([
                { groupId: "row-uuid-1", groupName: "Alpha" },
              ]),
            },
          };
        }
        return { success: false, errors: [{ code: "DB_UNAVAILABLE", message: "db unavailable" }] };
      }
      if (layer === "enum_dictionary" && action === "delete_group") {
        return {
          success: true,
          emission: { manifestId: CHILD_MANIFEST_ID, data: { ok: true } },
        };
      }
      return fallbackDispatchResponse(body);
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

      // preview-gap round: select the row FIRST -- Delete's own preview also needs groupId. Its
      // preview dispatch also matches layer=enum_dictionary/action=delete_group above (settling
      // as a harmless no-op canary success per context.dryRun), so it does not disturb this
      // test's own screen_list/Search-keyed redispatch-failure scenario.
      const rows = () =>
        Array.from(container.querySelectorAll("tbody tr")) as HTMLTableRowElement[];
      await waitFor(() => rows().length === 1);
      simulateRowClick(rows()[0]);
      await flushUpdates();

      simulateClick(deleteButtonEl!);
      await waitFor(() => queryDialog(container) !== null);

      const confirmButtonEl = queryConfirmButton(container);
      assertExists(confirmButtonEl, "Confirm must be present");
      simulateClick(confirmButtonEl!);

      // Wait for the (failing) redispatch to settle.
      await waitFor(() => screenListSearchCount >= 2);
      await flushUpdates();

      const warningEl = container.querySelector(
        "[data-projection-refresh-warning]",
      );
      assertExists(
        warningEl,
        "a failed canonical reread must surface an explicit warning banner",
      );

      // The OLD DOM is retained — the (stale, but real) row is still shown, never blanked.
      assert(
        (container.querySelector("tbody")?.textContent ?? "").includes("Alpha"),
        "a failed redispatch must retain the old DOM rather than blank it — the group row must still be present",
      );

      // The CONFIRMED write itself is never resent because its OWN reread failed -- exactly one
      // confirmed:true dispatch total (the preview's own dryRun:true dispatch to the same
      // layer/action is a separate, expected call and is excluded here).
      const confirmedDeleteGroupDispatches = scenario.capturedDispatchBodies.filter(
        (b) =>
          b.layer === "enum_dictionary" && b.action === "delete_group" &&
          (b.payload as Record<string, unknown> | undefined)?.confirmed === "true",
      );
      assertEquals(
        confirmedDeleteGroupDispatches.length,
        1,
        "a failed canonical reread must never cause the settled CONFIRMED write to be resent",
      );

      // The modal still closes — closing is driven by the WRITE's own settled result
      // (round 25), independent of whether the subsequent canonical reread succeeds.
      assert(
        queryDialog(container) === null,
        "the modal must not be force-reopened just because the canonical reread failed",
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
  "ProjectionShell (real mount, round 28): a settled write's canonical reread resets a stale tracked prop-bound value to the DB-authoritative one, while a passive SSE refresh preserves an in-progress edit",
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

    const CHILD_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae230";

    function probeLayoutNodes(probeValue: string, rows: Record<string, unknown>[]) {
      return [
        ...enumDeleteGroupConfirmModalLayoutNodes(rows),
        {
          nodeId: "canonical_probe_input",
          nodeKind: "catalog_component",
          componentId: "comp-canonical-probe-input-001",
          componentKind: "form_input/search_input",
          componentKey: "search_input.primitive",
          orderIndex: 10,
          runtimeInteractions: inputChangeSetStateInteraction("enum_table"),
          propBindings: { value: { source: "emission.data.probeValue" } },
        },
      ];
    }

    let searchCallCount = 0;
    const scenario = buildMockScenario((_callIndex, body) => {
      const layer = body.layer as string | undefined;
      const action = body.action as string | undefined;
      if (layer === "screen_list" && action === "Search") {
        searchCallCount++;
        return {
          success: true,
          emission: {
            manifestId: ADMIN_ENUM_MANIFEST_ID,
            layoutId: "layout-ae200-round28-canonical-reset-scenario",
            projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
            layoutNodes: probeLayoutNodes(
              "from-db",
              searchCallCount === 1
                ? [{ groupId: "row-uuid-1", groupName: "Alpha" }]
                : [],
            ),
            data: { probeValue: "from-db" },
          },
        };
      }
      if (layer === "enum_dictionary" && action === "delete_group") {
        return {
          success: true,
          emission: { manifestId: CHILD_MANIFEST_ID, data: { ok: true } },
        };
      }
      return fallbackDispatchResponse(body);
    });
    globalThis.fetch = scenario.fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
      render(h(ProjectionShell, {}), container);

      const probeInput = () =>
        container.querySelector(
          '[data-node-id="canonical_probe_input"] input',
        ) as HTMLInputElement | null;
      await waitFor(() => probeInput() !== null);
      await waitFor(() => probeInput()!.value === "from-db");

      // The user starts editing — a value that must survive a PASSIVE (SSE) refresh
      // but must NOT survive a canonical write reread.
      simulateInput(probeInput()!, "user-typed-edit");
      await flushUpdates();
      assertEquals(probeInput()!.value, "user-typed-edit");

      // A genuine write settles (delete_group), triggering the canonical reread.
      let deleteButtonEl: HTMLButtonElement | null = null;
      await waitFor(() => {
        deleteButtonEl = container.querySelector(
          '[data-node-id="enum_delete_group_button"] button',
        );
        return deleteButtonEl !== null;
      });
      // preview-gap round: select the row FIRST -- Delete's own preview also needs groupId.
      const rows = () =>
        Array.from(container.querySelectorAll("tbody tr")) as HTMLTableRowElement[];
      await waitFor(() => rows().length === 1);
      simulateRowClick(rows()[0]);
      await flushUpdates();
      simulateClick(deleteButtonEl!);
      await waitFor(() => queryDialog(container) !== null);
      const confirmButtonEl = queryConfirmButton(container);
      assertExists(confirmButtonEl, "Confirm must be present");
      simulateClick(confirmButtonEl!);

      // Wait for the canonical reread to land.
      await waitFor(() => searchCallCount >= 2);
      await waitFor(() => probeInput()?.value === "from-db");
      assertEquals(
        probeInput()!.value,
        "from-db",
        "a settled write's canonical reread must force the stale typed value back to the DB-authoritative one",
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
// ─────────────────────────────────────────────────────────────────────────
// Round 26: shared scenario contract, generalized across the 6 write actions
// newly embedded into ae200's single surface behind their own disclosure/modal
// (create_group's own single-click confirmed:true NG-axis violation fixed;
// update_group/create_item/update_item/delete_item/set_group_items newly
// embedded) -- one config table + one shared test body per round 26's own
// instruction not to duplicate per-operation test bodies. Each config
// describes the fields a real admin would type (if any), the group-row value
// (if the operation needs the currently-selected group's identity), and the
// expected dispatch target/payload the Confirm click must produce.
// ─────────────────────────────────────────────────────────────────────────

interface ConfirmModalScenarioConfig {
  readonly label: string;
  readonly prefix: string;
  readonly title: string;
  readonly body: string;
  readonly targetRef: string;
  /** Typed input fields the user fills before opening/confirming, keyed by nodeId -> value typed. */
  readonly typedFields: Record<string, string>;
  /** A->B fresh-value proof: NEW values typed into the SAME nodes (a strict superset of
   * typedFields' keys) AFTER a preview has already succeeded and the modal is open, but BEFORE
   * Confirm is clicked -- proving Confirm re-resolves fresh at click time rather than reusing
   * whatever the preview saw. Empty for scenarios with no typed fields (e.g. delete_group). */
  readonly freshConfirmTypedFields: Record<string, string>;
  /** Whether the operation reads the selected enum_table row's groupId (update_group/set_group_items). */
  readonly needsSelectedGroupRow: boolean;
  /** Keys expected in the Confirm dispatch payload besides "confirmed". */
  readonly expectedPayloadKeys: readonly string[];
}

const CONFIRM_MODAL_SCENARIOS: readonly ConfirmModalScenarioConfig[] = [
  {
    label: "create_group",
    prefix: "enum_create_group",
    title: "Create group",
    body: "Create a new enum group with the entered name.",
    targetRef:
      "manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group",
    typedFields: { enum_create_group_name_input: "Widgets" },
    freshConfirmTypedFields: { enum_create_group_name_input: "Widgets-B" },
    needsSelectedGroupRow: false,
    expectedPayloadKeys: ["groupName"],
  },
  {
    label: "update_group",
    prefix: "enum_update_group",
    title: "Update group",
    body: "Rename the selected enum group to the entered name.",
    targetRef:
      "manifest:00000000-0000-0000-0000-0000000ae220:enum_dictionary:update_group",
    typedFields: { enum_update_group_name_input: "Widgets Renamed" },
    freshConfirmTypedFields: { enum_update_group_name_input: "Widgets Renamed-B" },
    needsSelectedGroupRow: true,
    expectedPayloadKeys: ["groupId", "groupName"],
  },
  {
    label: "delete_group",
    prefix: "enum_delete_group",
    title: "Delete group",
    body:
      "This will permanently delete the selected enum group. This cannot be undone.",
    targetRef: AE230_DELETE_GROUP_TARGET_REF,
    typedFields: {},
    freshConfirmTypedFields: {},
    needsSelectedGroupRow: true,
    expectedPayloadKeys: ["groupId"],
  },
  {
    label: "create_item",
    prefix: "enum_create_item",
    title: "Create item",
    body: "Create a new enum item with the entered name.",
    targetRef:
      "manifest:00000000-0000-0000-0000-0000000ae240:enum_dictionary:create_item",
    typedFields: { enum_create_item_name_input: "small" },
    freshConfirmTypedFields: { enum_create_item_name_input: "small-B" },
    needsSelectedGroupRow: false,
    expectedPayloadKeys: ["name"],
  },
  {
    label: "update_item",
    prefix: "enum_update_item",
    title: "Update item",
    body: "Rename the enum item at the entered index to the entered name.",
    targetRef:
      "manifest:00000000-0000-0000-0000-0000000ae250:enum_dictionary:update_item",
    typedFields: {
      enum_update_item_index_input: "3",
      enum_update_item_name_input: "medium",
    },
    freshConfirmTypedFields: {
      enum_update_item_index_input: "4",
      enum_update_item_name_input: "medium-B",
    },
    needsSelectedGroupRow: false,
    expectedPayloadKeys: ["indexNum", "name"],
  },
  {
    label: "delete_item",
    prefix: "enum_delete_item",
    title: "Delete item",
    body:
      "This will permanently delete the enum item at the entered index. This cannot be undone.",
    targetRef:
      "manifest:00000000-0000-0000-0000-0000000ae260:enum_dictionary:delete_item",
    typedFields: { enum_delete_item_index_input: "3" },
    freshConfirmTypedFields: { enum_delete_item_index_input: "4" },
    needsSelectedGroupRow: false,
    expectedPayloadKeys: ["indexNum"],
  },
  {
    label: "set_group_items",
    prefix: "enum_set_group_items",
    title: "Set group items",
    body: "Replace the selected group's item membership with the entered indexes.",
    targetRef:
      "manifest:00000000-0000-0000-0000-0000000ae270:enum_dictionary:set_group_items",
    typedFields: { enum_set_group_items_input: "1,2,3" },
    freshConfirmTypedFields: { enum_set_group_items_input: "4,5,6" },
    needsSelectedGroupRow: true,
    expectedPayloadKeys: ["groupId", "enumIndexNums"],
  },
];

/** field -> source mapping shared by the preview dispatch (open button) and the confirmed write
 * dispatch (Confirm button) -- both resolve the SAME business fields, differing only in the
 * dryRun/confirmed literal appended by each call site. */
function scenarioFieldSources(
  config: ConfirmModalScenarioConfig,
): Record<string, string> {
  const sources: Record<string, string> = {};
  for (const key of config.expectedPayloadKeys) {
    if (key === "groupId") {
      sources[key] = "node:enum_table.value.groupId";
    } else {
      const fieldNodeId = Object.keys(config.typedFields)[
        config.expectedPayloadKeys.filter((k) => k !== "groupId").indexOf(key)
      ];
      sources[key] = `node:${fieldNodeId}.value`;
    }
  }
  return sources;
}

function buildConfirmModalLayoutNodes(
  config: ConfirmModalScenarioConfig,
  rows: Record<string, unknown>[],
) {
  const { prefix, title, body, targetRef, typedFields } = config;
  const modalKey = `${prefix}_confirm_modal`;

  const nodes: Record<string, unknown>[] = [
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
  ];

  let orderIndex = 1;
  for (const nodeId of Object.keys(typedFields)) {
    nodes.push({
      nodeId,
      nodeKind: "catalog_component",
      componentId: `comp-${nodeId}-001`,
      componentKind: "form_input/input",
      componentKey: "form_field.primitive",
      orderIndex: orderIndex++,
      // inputFactory's requireBinding(spec,"change") fails the whole render closed without a
      // "change" trigger binding -- same pattern as the existing node-group-id-input scenario
      // above (a harmless setState targeting the modal, predeclared automatically).
      runtimeInteractions: inputChangeSetStateInteraction(`${prefix}_confirm_modal`),
    });
  }

  // preview-gap round: the SAME field-source mapping the Confirm button below uses (minus
  // "confirmed"/plus "dryRun") — the open/action button now dispatches a non-mutating preview
  // to the SAME target manifest BEFORE the modal is allowed to open (deferred
  // secondaryDisclosureActionType=openModal, gated on this dispatch's own success), mirroring
  // db/seed_empty.sql's real ae204/ae206 shape (react_schema_topology_seed_translator.py's
  // build_admin_runtime_dispatch_override_candidate + build_secondary_disclosure_runtime_
  // interaction_candidate both attaching to the SAME open-button leaf node).
  const previewPayloadFrom: Record<string, string> = { ...scenarioFieldSources(config) };
  previewPayloadFrom["dryRun"] = "literal:true";

  nodes.push({
    nodeId: `${prefix}_button`,
    nodeKind: "catalog_component",
    componentId: `comp-${prefix}-button-001`,
    componentKind: "action/button",
    componentKey: "button.primitive",
    orderIndex: orderIndex++,
    wiringKind: "admin_runtime",
    targetSurface: "manifest",
    targetRef: `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:list_groups`,
    dispatchTargetRefByTrigger: { click: targetRef },
    dispatchPayloadFromByTrigger: { click: previewPayloadFrom },
    runtimeInteractions: [
      {
        trigger: "click",
        actionType: "openModal",
        targetNodeId: modalKey,
        statePath: "open",
      },
    ],
  });

  nodes.push({
    nodeId: modalKey,
    nodeKind: "catalog_component",
    componentId: modalKey,
    componentKind: "disclosure/modal",
    componentKey: "modal.template",
    orderIndex: orderIndex++,
    propsJson: JSON.stringify({ data: { open: false, title, body } }),
    runtimeInteractions: [
      {
        trigger: "toggle",
        actionType: "closeModal",
        targetNodeId: modalKey,
        statePath: "open",
      },
    ],
  });

  // Same field-source mapping as previewPayloadFrom above (minus "dryRun"/plus "confirmed") —
  // Confirm re-resolves fresh from current node values at click time, same as before.
  const dispatchPayloadFrom: Record<string, string> = { ...scenarioFieldSources(config) };
  dispatchPayloadFrom["confirmed"] = "literal:true";

  nodes.push({
    nodeId: `${prefix}_confirm_button`,
    nodeKind: "catalog_component",
    componentId: `comp-${prefix}-confirm-button-001`,
    componentKind: "action/button",
    componentKey: "button.primitive",
    orderIndex: 0,
    parentNodeId: modalKey,
    wiringKind: "admin_runtime",
    targetSurface: "manifest",
    targetRef: `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:list_groups`,
    dispatchTargetRefByTrigger: { click: targetRef },
    dispatchPayloadFromByTrigger: { click: dispatchPayloadFrom },
    runtimeInteractions: [
      {
        trigger: "click",
        actionType: "closeModal",
        targetNodeId: modalKey,
        statePath: "open",
      },
    ],
  });

  nodes.push({
    nodeId: `${prefix}_cancel_button`,
    nodeKind: "catalog_component",
    componentId: `comp-${prefix}-cancel-button-001`,
    componentKind: "action/button",
    componentKey: "button.primitive",
    orderIndex: 1,
    parentNodeId: modalKey,
    runtimeInteractions: [
      {
        trigger: "click",
        actionType: "closeModal",
        targetNodeId: modalKey,
        statePath: "open",
      },
    ],
  });

  return nodes;
}

function queryOpenButtonFor(container: Element, prefix: string) {
  return container.querySelector(
    `[data-node-id="${prefix}_button"] button`,
  ) as HTMLButtonElement | null;
}

function queryModalFor(container: Element) {
  return container.querySelector('[role="dialog"]');
}

function queryConfirmButtonFor(container: Element, prefix: string) {
  return container.querySelector(
    `[data-node-id="${prefix}_confirm_button"] button`,
  ) as HTMLButtonElement | null;
}

function queryCancelButtonFor(container: Element, prefix: string) {
  return container.querySelector(
    `[data-node-id="${prefix}_cancel_button"] button`,
  ) as HTMLButtonElement | null;
}

/** "manifest:<uuid>:<layer>:<action>" -> "<uuid>", the same generic parse
 * ProjectionShell.tsx's extractManifestIdFromTargetRef uses. */
function manifestIdFromTargetRef(targetRef: string): string {
  const m = /^manifest:([^:]+):/.exec(targetRef);
  if (!m) throw new Error(`not a manifest targetRef: ${targetRef}`);
  return m[1];
}

function payloadOf(b: Record<string, unknown>): Record<string, unknown> {
  return (b.payload ?? {}) as Record<string, unknown>;
}

function queryRefreshWarning(container: Element): string | null {
  const el = container.querySelector("[data-projection-refresh-warning]");
  return el ? el.textContent : null;
}

for (const config of CONFIRM_MODAL_SCENARIOS) {
  Deno.test(
    `ProjectionShell (real mount, preview-gap round shared scenario): ${config.label}'s ae200-embedded confirm modal — the open/action button dispatches a dryRun preview to the SAME target manifest; a FAILED preview never opens the modal and surfaces the backend validation error while typed values/selection survive; a SUCCEEDED preview opens the modal without adopting state or triggering a canonical reread; Cancel sends no write; Confirm re-resolves fresh values, dispatches confirmed:true, and closes only after backend success`,
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

      const targetManifestId = manifestIdFromTargetRef(config.targetRef);
      let previewAttempts = 0;
      const scenario = buildMockScenario((callIndex, body) => {
        if (callIndex === 1) {
          return {
            success: true,
            emission: {
              manifestId: ADMIN_ENUM_MANIFEST_ID,
              layoutId: `layout-ae200-preview-gap-${config.label}-scenario`,
              projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
              layoutNodes: buildConfirmModalLayoutNodes(config, [
                { groupId: "row-uuid-1", groupName: "Alpha" },
                { groupId: "row-uuid-2", groupName: "Beta" },
              ]),
            },
          };
        }
        const payload = payloadOf(body);
        const isPreviewDispatch = payload.target_ref === config.targetRef &&
          payload.dryRun === "true";
        if (isPreviewDispatch) {
          previewAttempts += 1;
          if (previewAttempts === 1) {
            // First preview attempt: a genuine backend validation failure — never a queue/network
            // rejection — proving a failed preview surfaces explicitly and never opens the modal.
            return {
              success: false,
              errors: [{
                code: "ENUM_PREVIEW_VALIDATION_FAILED",
                message: `${config.label} preview validation failed (scenario-injected).`,
              }],
            };
          }
          // Every subsequent preview attempt succeeds — a non-mutating validation-only result.
          return {
            success: true,
            emission: { manifestId: targetManifestId, data: { ok: true, dryRun: true, valid: true } },
          };
        }
        return fallbackDispatchResponse(body);
      });
      globalThis.fetch = scenario.fetch;

      try {
        globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
        render(h(ProjectionShell, {}), container);

        let openButtonEl: HTMLButtonElement | null = null;
        await waitFor(() => {
          openButtonEl = queryOpenButtonFor(container, config.prefix);
          return openButtonEl !== null;
        });
        assertExists(openButtonEl, `${config.label}'s open trigger must render`);

        // Closed by default: Confirm/Cancel/dialog genuinely absent from the DOM.
        assert(
          queryModalFor(container) === null,
          `${config.label}'s modal must not be in the DOM before opening`,
        );
        assert(
          queryConfirmButtonFor(container, config.prefix) === null,
          `${config.label}'s Confirm must not be in the DOM before opening`,
        );
        assert(
          queryCancelButtonFor(container, config.prefix) === null,
          `${config.label}'s Cancel must not be in the DOM before opening`,
        );

        // Type the fields (and select the row, if needed) BEFORE the first open/preview attempt —
        // this surface's typed fields live outside the modal, so the SAME click that requests the
        // preview is the click that would otherwise open the modal.
        for (const [nodeId, value] of Object.entries(config.typedFields)) {
          const inputEl = container.querySelector(
            `[data-node-id="${nodeId}"] input`,
          ) as HTMLInputElement | null;
          assertExists(inputEl, `${config.label}'s ${nodeId} input must render`);
          simulateInput(inputEl!, value);
        }
        if (config.needsSelectedGroupRow) {
          const rowEl = container.querySelector(
            "tbody tr",
          ) as HTMLTableRowElement | null;
          assertExists(rowEl, `${config.label} needs a selectable group row`);
          simulateRowClick(rowEl!);
          await flushUpdates();
        }

        // --- Attempt 1: preview FAILS -> modal never opens, error surfaced, values retained ---
        simulateClick(queryOpenButtonFor(container, config.prefix)!);
        await waitFor(() => previewAttempts >= 1);
        await flushUpdates();
        assert(
          queryModalFor(container) === null,
          `${config.label}'s modal must NOT open when the dryRun preview fails`,
        );
        assert(
          (queryRefreshWarning(container) ?? "").includes(
            `${config.label} preview validation failed`,
          ),
          `${config.label}'s failed preview must surface the backend validation error explicitly, got: ${
            queryRefreshWarning(container)
          }`,
        );
        // Value retention is proven through the mechanism that actually carries it in this
        // architecture -- the live node value tracker resolved fresh into each dispatch's own
        // payload (form_input/input's rendered DOM value display is a separate, pre-existing,
        // unrelated component-registry gap -- form_input/search_input is this repo's own
        // established display-value-carrying control, per round23's "Load(A)->Load(B)" test; this
        // scenario's field control choice mirrors the real ae200 seed's form_input/form_field
        // fields and is not itself in scope here). The failed preview attempt's OWN dispatch
        // payload already proves the typed values were resolved correctly; the retry below proves
        // they SURVIVED across the failure (never reset/cleared by the failed attempt).
        const failedPreviewPayload = payloadOf(
          scenario.capturedDispatchBodies[scenario.capturedDispatchBodies.length - 1],
        );
        const fieldSources = scenarioFieldSources(config);
        for (const [nodeId, value] of Object.entries(config.typedFields)) {
          const key = Object.entries(fieldSources).find(
            ([, source]) => source === `node:${nodeId}.value`,
          )?.[0];
          assertExists(key, `${config.label}'s ${nodeId} must map to a preview payload field`);
          assertEquals(
            failedPreviewPayload[key!],
            value,
            `${config.label}'s ${nodeId} typed value must reach the (failed) preview dispatch payload`,
          );
        }
        const dispatchCountAfterFailedPreview = scenario.capturedDispatchBodies.length;

        // --- Attempt 2: preview SUCCEEDS -> modal opens, no state adoption, no canonical reread ---
        simulateClick(queryOpenButtonFor(container, config.prefix)!);
        await waitFor(() => previewAttempts >= 2);
        await flushUpdates();
        assertExists(
          queryModalFor(container),
          `${config.label}'s modal must open once the dryRun preview succeeds`,
        );
        assertEquals(
          queryRefreshWarning(container),
          null,
          `${config.label}'s stale failure banner must clear once a later preview succeeds`,
        );
        // preview-gap round core assertion: a settled dryRun preview SUCCESS must never be
        // classified the same as a confirmed write success -- exactly one extra dispatch call
        // (the successful preview itself) happened, never a second (canonical reread) call.
        assertEquals(
          scenario.capturedDispatchBodies.length,
          dispatchCountAfterFailedPreview + 1,
          `${config.label}'s successful preview must dispatch exactly once -- ` +
            `no canonical reread triggered by preview success`,
        );

        // Retry-retention proof: this SUCCEEDED retry's own dispatch payload (not just the
        // earlier failed attempt's) must directly carry the SAME typed values/selection that
        // were retained across the failure -- proving retention survives an actual retry, not
        // merely that resolution succeeds once.
        const retryPreviewPayload = payloadOf(
          scenario.capturedDispatchBodies[scenario.capturedDispatchBodies.length - 1],
        );
        for (const [nodeId, value] of Object.entries(config.typedFields)) {
          const key = Object.entries(fieldSources).find(
            ([, source]) => source === `node:${nodeId}.value`,
          )?.[0];
          assertExists(key, `${config.label}'s ${nodeId} must map to a preview payload field`);
          assertEquals(
            retryPreviewPayload[key!],
            value,
            `${config.label}'s ${nodeId} typed value must still be RETAINED and carried directly ` +
              `by the retried preview's own dispatch payload (not merely the failed attempt's)`,
          );
        }
        if (config.needsSelectedGroupRow) {
          assertEquals(
            retryPreviewPayload.groupId,
            "row-uuid-1",
            `${config.label}'s selected row must still be RETAINED and carried directly by the ` +
              `retried preview's own dispatch payload`,
          );
        }

        const confirmButtonEl = queryConfirmButtonFor(container, config.prefix);
        const cancelButtonEl = queryCancelButtonFor(container, config.prefix);
        assertExists(confirmButtonEl, `${config.label}'s Confirm must be nested inside the open modal`);
        assertExists(cancelButtonEl, `${config.label}'s Cancel must be nested inside the open modal`);

        // Cancel: no dispatch, modal closes.
        const dispatchCountBeforeCancel = scenario.capturedDispatchBodies.length;
        simulateClick(cancelButtonEl!);
        await flushUpdates();
        assert(
          queryModalFor(container) === null,
          `${config.label}'s modal must close on Cancel`,
        );
        assertEquals(
          scenario.capturedDispatchBodies.length,
          dispatchCountBeforeCancel,
          `Cancel must never dispatch for ${config.label}`,
        );

        // Reopen (a fresh, successful preview attempt) then Confirm.
        simulateClick(queryOpenButtonFor(container, config.prefix)!);
        await waitFor(() => previewAttempts >= 3);
        await flushUpdates();
        assertExists(
          queryModalFor(container),
          `${config.label}'s modal must reopen on a fresh successful preview`,
        );

        // A -> B fresh-value proof: the preview above just resolved against value/selection A.
        // NOW change the typed fields to B (and/or reselect a DIFFERENT row) while the modal is
        // already open, BEFORE clicking Confirm -- proving Confirm re-resolves fresh at click
        // time from the LATEST node values, rather than replaying whatever the preview captured.
        for (const [nodeId, valueB] of Object.entries(config.freshConfirmTypedFields)) {
          const inputEl = container.querySelector(
            `[data-node-id="${nodeId}"] input`,
          ) as HTMLInputElement | null;
          assertExists(inputEl, `${config.label}'s ${nodeId} input must still render while reopened`);
          simulateInput(inputEl!, valueB);
        }
        if (config.needsSelectedGroupRow) {
          const rowsForFreshSelect = Array.from(
            container.querySelectorAll("tbody tr"),
          ) as HTMLTableRowElement[];
          assertExists(
            rowsForFreshSelect[1],
            `${config.label} needs a SECOND selectable row for the A->B fresh-selection proof`,
          );
          simulateRowClick(rowsForFreshSelect[1]);
          await flushUpdates();
        }

        const confirmAgainEl = queryConfirmButtonFor(container, config.prefix);
        assertExists(confirmAgainEl, `${config.label}'s Confirm must still be present after reopening`);
        simulateClick(confirmAgainEl!);
        await waitFor(() =>
          scenario.capturedDispatchBodies.some((b) =>
            payloadOf(b).target_ref === config.targetRef &&
            payloadOf(b).confirmed === "true"
          )
        );
        const confirmBody = scenario.capturedDispatchBodies.find((b) =>
          payloadOf(b).target_ref === config.targetRef &&
          payloadOf(b).confirmed === "true"
        ) as Record<string, unknown>;
        assertExists(confirmBody, `${config.label}'s Confirm dispatch must target ${config.targetRef}`);
        assertEquals(confirmBody.layer, "enum_dictionary");
        const confirmPayload = payloadOf(confirmBody);
        assertEquals(confirmPayload.confirmed, "true");
        assertEquals(
          confirmPayload.dryRun,
          undefined,
          `${config.label}'s Confirm dispatch must never carry a dryRun flag`,
        );
        for (const key of config.expectedPayloadKeys) {
          assertExists(
            confirmPayload[key],
            `${config.label}'s Confirm dispatch payload must carry '${key}'`,
          );
        }
        // A -> B fresh-value proof (continued): the Confirm payload must carry the NEW (B)
        // values typed after the preview settled, never the stale (A) values the preview saw.
        for (const [nodeId, valueB] of Object.entries(config.freshConfirmTypedFields)) {
          const key = Object.entries(fieldSources).find(
            ([, source]) => source === `node:${nodeId}.value`,
          )?.[0];
          assertExists(key, `${config.label}'s ${nodeId} must map to a confirm payload field`);
          assertEquals(
            confirmPayload[key!],
            valueB,
            `${config.label}'s Confirm payload must carry the FRESH (B) value for ${nodeId}, ` +
              `re-resolved at click time -- not the (A) value the earlier preview saw`,
          );
        }
        if (config.needsSelectedGroupRow) {
          assertEquals(
            confirmPayload.groupId,
            "row-uuid-2",
            `${config.label}'s Confirm payload must carry the FRESHLY re-selected (B) row's ` +
              `groupId, re-resolved at click time -- not the (A) row the earlier preview saw`,
          );
        }

        // Backend success closes the modal.
        await waitFor(() => queryModalFor(container) === null);
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
}

// ─── round 29: authored-target_ref identity confirmation, generic unbound-tracker
// clear on canonical_reread, and the canonical_reread-outranks-passive_invalidation
// ordering contract ───────────────────────────────────────────────────────────────

Deno.test(
  "ProjectionShell (real mount, round 29): a settled write's response manifestId that does NOT match the manifest actually authored in target_ref is a genuine identity anomaly — never treated as an ordinary expected child response, fails close with an explicit warning, and never triggers a canonical reread",
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

    // Never the manifest actually authored in target_ref (ae230) nor ae200 itself — proves the
    // round 29 check is a genuine confirmation against the DISPATCHED identity, not merely a
    // "differs from adopted" heuristic that would have silently accepted this as an ordinary
    // cross-manifest child response.
    const UNEXPECTED_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae999";

    let searchCallCount = 0;
    const scenario = buildMockScenario((_callIndex, body) => {
      const layer = body.layer as string | undefined;
      const action = body.action as string | undefined;
      if (layer === "screen_list" && action === "Search") {
        searchCallCount++;
        return {
          success: true,
          emission: {
            manifestId: ADMIN_ENUM_MANIFEST_ID,
            layoutId: "layout-ae200-round29-identity-mismatch-scenario",
            projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
            layoutNodes: enumDeleteGroupConfirmModalLayoutNodes([
              { groupId: "row-uuid-1", groupName: "Alpha" },
            ]),
          },
        };
      }
      if (layer === "enum_dictionary" && action === "delete_group") {
        // Round 3 (preview-gap audit, settlement authority): the PREVIEW dispatch (dryRun) must
        // resolve the CORRECT manifest (ae230) so the shared settlement authority accepts it and
        // the Modal actually opens — the settlement authority now validates identity for the
        // Modal-open gate too, not just for handleRuntimeDispatchResult's own classification (the
        // OLD comment on this test claimed opening "gates purely on result.success... independent
        // of handleRuntimeDispatchResult's own identity classification", which was exactly the gap
        // this round closes). Only the CONFIRM dispatch (confirmed:true) returns the wrong
        // manifestId, so the identity anomaly this test is actually about is observed on Confirm,
        // after the Modal has legitimately opened — matching what the assertions below check.
        const payload = body.payload as Record<string, unknown> | undefined;
        if (payload?.confirmed === "true" || payload?.confirmed === true) {
          return {
            success: true,
            emission: { manifestId: UNEXPECTED_MANIFEST_ID, data: { ok: true } },
          };
        }
        return {
          success: true,
          emission: { manifestId: "00000000-0000-0000-0000-0000000ae230", data: {} },
        };
      }
      return fallbackDispatchResponse(body);
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
      // preview-gap round: select the row FIRST -- Delete's own preview also needs groupId. Its
      // preview dispatch ALSO matches layer=enum_dictionary/action=delete_group above, but (round
      // 3) resolves the CORRECT ae230 manifest, so the shared settlement authority accepts it and
      // the Modal legitimately opens -- only Confirm's own dispatch below carries the wrong
      // manifestId, which is what this test is actually about.
      const rows = () =>
        Array.from(container.querySelectorAll("tbody tr")) as HTMLTableRowElement[];
      await waitFor(() => rows().length === 1);
      simulateRowClick(rows()[0]);
      await flushUpdates();
      simulateClick(deleteButtonEl!);
      await waitFor(() => queryDialog(container) !== null);

      const confirmButtonEl = queryConfirmButton(container);
      assertExists(confirmButtonEl, "Confirm must be present");
      simulateClick(confirmButtonEl!);
      for (let i = 0; i < 15; i++) await flushUpdates();

      const warningEl = container.querySelector(
        "[data-projection-refresh-warning]",
      );
      assertExists(
        warningEl,
        "an identity anomaly (response manifestId != authored target_ref manifest) must surface an explicit warning",
      );
      assert(
        (warningEl!.textContent ?? "").includes(UNEXPECTED_MANIFEST_ID),
        "the warning must name the unexpected manifest identity actually returned",
      );
      assertEquals(
        searchCallCount,
        1,
        "an identity anomaly must NOT be treated as an ordinary expected child response — no canonical reread may be triggered",
      );
      assert(
        (container.querySelector("tbody")?.textContent ?? "").includes("Alpha"),
        "the old DOM must be retained — never blanked on an identity anomaly",
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

function unboundProbeLayoutNodes(rows: Record<string, unknown>[]) {
  return [
    ...enumDeleteGroupConfirmModalLayoutNodes(rows),
    {
      nodeId: "unbound_probe_input",
      nodeKind: "catalog_component",
      componentId: "comp-unbound-probe-input-001",
      componentKind: "form_input/input",
      componentKey: "text_input.primitive",
      orderIndex: 10,
      runtimeInteractions: inputChangeSetStateInteraction(
        "enum_delete_group_confirm_modal",
      ),
      // No propBindings at all — a genuinely unbound, free-typed field. Only
      // onChange keystroke tracking (renderEmission's onNodeValueChange wiring)
      // ever populates its tracker entry; nothing re-seeds it from emission.data.
    },
    {
      nodeId: "probe_dispatch_button",
      nodeKind: "catalog_component",
      componentId: "comp-probe-dispatch-button-001",
      componentKind: "action/button",
      componentKey: "button.primitive",
      orderIndex: 11,
      wiringKind: "admin_runtime",
      targetSurface: "manifest",
      targetRef: `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:noop_probe`,
      dispatchPayloadFromByTrigger: {
        click: { probeField: "node:unbound_probe_input.value" },
      },
    },
  ];
}

Deno.test(
  "ProjectionShell (real mount, round 29): a settled write's canonical reread discards an UNBOUND (no propBindings) typed input's stale tracked value — a later dispatch referencing it fails close instead of resending the pre-write value",
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

    const CHILD_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae230";
    let searchCallCount = 0;
    let noopProbeDispatchCount = 0;

    const scenario = buildMockScenario((_callIndex, body) => {
      const layer = body.layer as string | undefined;
      const action = body.action as string | undefined;
      if (layer === "screen_list" && action === "Search") {
        searchCallCount++;
        return {
          success: true,
          emission: {
            manifestId: ADMIN_ENUM_MANIFEST_ID,
            layoutId: "layout-ae200-round29-unbound-clear-scenario",
            projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
            layoutNodes: unboundProbeLayoutNodes(
              searchCallCount === 1
                ? [{ groupId: "row-uuid-1", groupName: "Alpha" }]
                : [],
            ),
          },
        };
      }
      if (layer === "enum_dictionary" && action === "delete_group") {
        return {
          success: true,
          emission: { manifestId: CHILD_MANIFEST_ID, data: { ok: true } },
        };
      }
      if (layer === "enum_dictionary" && action === "noop_probe") {
        noopProbeDispatchCount++;
        return fallbackDispatchResponse(body);
      }
      return fallbackDispatchResponse(body);
    });
    globalThis.fetch = scenario.fetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
      render(h(ProjectionShell, {}), container);

      const probeInput = () =>
        container.querySelector(
          '[data-node-id="unbound_probe_input"] input',
        ) as HTMLInputElement | null;
      const probeButton = () =>
        container.querySelector(
          '[data-node-id="probe_dispatch_button"] button',
        ) as HTMLButtonElement | null;
      await waitFor(() => probeInput() !== null && probeButton() !== null);

      // Type into the UNBOUND field and prove the tracker holds it BEFORE any write —
      // a probe dispatch referencing it must resolve, not fail close.
      simulateInput(probeInput()!, "pre-write-typed-value");
      await flushUpdates();
      simulateClick(probeButton()!);
      await waitFor(() => noopProbeDispatchCount >= 1);
      const firstProbeBody = scenario.capturedDispatchBodies.find(
        (b) => b.layer === "enum_dictionary" && b.action === "noop_probe",
      ) as Record<string, unknown>;
      assertExists(firstProbeBody, "the pre-write probe dispatch must have been sent");
      assertEquals(
        (firstProbeBody.payload as Record<string, unknown>).probeField,
        "pre-write-typed-value",
        "the tracker must hold the freshly typed value before any write settles",
      );

      // A genuine, UNRELATED write settles (delete_group), triggering ae200's canonical reread.
      let deleteButtonEl: HTMLButtonElement | null = null;
      await waitFor(() => {
        deleteButtonEl = container.querySelector(
          '[data-node-id="enum_delete_group_button"] button',
        );
        return deleteButtonEl !== null;
      });
      // preview-gap round: select the row FIRST -- Delete's own preview also needs groupId.
      const rows = () =>
        Array.from(container.querySelectorAll("tbody tr")) as HTMLTableRowElement[];
      await waitFor(() => rows().length === 1);
      simulateRowClick(rows()[0]);
      await flushUpdates();
      simulateClick(deleteButtonEl!);
      await waitFor(() => queryDialog(container) !== null);
      const confirmButtonEl = queryConfirmButton(container);
      assertExists(confirmButtonEl, "Confirm must be present");
      simulateClick(confirmButtonEl!);
      await waitFor(() => searchCallCount >= 2);
      await flushUpdates();

      // A second probe click, AFTER the canonical reread, must fail close (no new probe
      // dispatch captured) — the unbound field's stale pre-write value must not survive an
      // unrelated write's canonical reread, and must never be silently resent.
      simulateClick(probeButton()!);
      for (let i = 0; i < 10; i++) await flushUpdates();
      assertEquals(
        noopProbeDispatchCount,
        1,
        "the unbound field's stale tracked value must have been discarded by the canonical " +
          "reread — a later reference to it must fail close, never resend the pre-write value",
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
  "ProjectionShell (real mount, round 29): a canonical_reread's OWN failure still surfaces as an explicit warning even though a passive_invalidation STARTED (queued) while the canonical reread was still in flight — never silently discarded as merely 'superseded' by that later call",
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

    const CHILD_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae230";

    function probeLayoutNodes(probeValue: string, rows: Record<string, unknown>[]) {
      return [
        ...enumDeleteGroupConfirmModalLayoutNodes(rows),
        {
          nodeId: "canonical_probe_input",
          nodeKind: "catalog_component",
          componentId: "comp-canonical-probe-input-001",
          componentKind: "form_input/search_input",
          componentKey: "search_input.primitive",
          orderIndex: 10,
          runtimeInteractions: inputChangeSetStateInteraction(
            "enum_delete_group_confirm_modal",
          ),
          propBindings: { value: { source: "emission.data.probeValue" } },
        },
      ];
    }

    // The api_command_lane FIFO (frontendScheduler.ts drainClientCommandQueue) serializes
    // ACTUAL network calls strictly in queue order — a queued-but-not-yet-dequeued command
    // never races another command's fetch. The bug round 29 fixes is not about network
    // resolution order; it is that gen assignment happens SYNCHRONOUSLY at
    // refreshCurrentManifestAsync's call time (before the FIFO even dequeues that call's own
    // command) — so a passive_invalidation that merely STARTS (and is queued) while an
    // earlier canonical_reread's command is still draining can, under the OLD single-counter
    // design, make that canonical_reread's own (guaranteed-to-resolve-first, by FIFO order)
    // response look "stale" purely because the shared counter moved on, even though nothing
    // about the canonical_reread's own request was actually superseded. Reproducing this
    // needs only: hold the canonical_reread's OWN screen_list/Search call pending, fire the
    // SSE event WHILE it is still pending (bumping passiveGenRef synchronously, before its
    // own — necessarily later, FIFO-serialized — fetch can even begin), then resolve the
    // canonical_reread with a FAILURE and confirm it still surfaces as an explicit warning.
    let searchCallCount = 0;
    const canonicalSearchResolveHolder: {
      current: ((body: Record<string, unknown>) => void) | null;
    } = { current: null };

    const scenario = buildMockScenario((_callIndex, body) => {
      const layer = body.layer as string | undefined;
      const action = body.action as string | undefined;
      if (layer === "screen_list" && action === "Search") {
        searchCallCount++;
        if (searchCallCount === 1) {
          return {
            success: true,
            emission: {
              manifestId: ADMIN_ENUM_MANIFEST_ID,
              layoutId: "layout-ae200-round29-race-scenario",
              projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
              layoutNodes: probeLayoutNodes("initial", [
                { groupId: "row-uuid-1", groupName: "Alpha" },
              ]),
              data: { probeValue: "initial" },
            },
          };
        }
        // Unreachable synchronously — buildMockScenario's responder is synchronous, so the
        // pending-promise indirection is applied via the wrapping fetch below instead.
        return fallbackDispatchResponse(body);
      }
      if (layer === "enum_dictionary" && action === "delete_group") {
        return {
          success: true,
          emission: { manifestId: CHILD_MANIFEST_ID, data: { ok: true } },
        };
      }
      return fallbackDispatchResponse(body);
    });

    // Wrap scenario.fetch so ONLY the SECOND screen_list/Search call ever made (the
    // canonical_reread triggered by delete_group's own settlement) resolves when the test
    // explicitly calls canonicalSearchResolve — every other call, including any LATER
    // screen_list/Search call (the passive_invalidation's own eventual, FIFO-later call),
    // passes through unmodified so nothing is left permanently pending (a permanently-pending
    // fetch would wedge the shared api_command_lane FIFO for the rest of the test run).
    let searchCallsSeen = 0;
    const raceFetch = (async (url: string, init?: RequestInit) => {
      const isDispatch = url.toString() === "/api/dispatch";
      if (isDispatch) {
        const parsed = JSON.parse(String(init?.body ?? "{}")) as Record<string, unknown>;
        if (parsed.layer === "screen_list" && parsed.action === "Search") {
          searchCallsSeen++;
          if (searchCallsSeen === 2) {
            // The canonical_reread's own call — held pending until resolved explicitly.
            return await new Promise<Response>((resolve) => {
              canonicalSearchResolveHolder.current = (respBody) =>
                resolve(new Response(JSON.stringify(respBody), { status: 200 }));
            });
          }
        }
      }
      return await scenario.fetch(url, init);
    }) as typeof fetch;
    globalThis.fetch = raceFetch;

    try {
      globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
      render(h(ProjectionShell, {}), container);

      const probeInput = () =>
        container.querySelector(
          '[data-node-id="canonical_probe_input"] input',
        ) as HTMLInputElement | null;
      await waitFor(() => probeInput() !== null);
      await waitFor(() => probeInput()!.value === "initial");

      // Trigger the write → delete_group settles → its canonical reread issues the SECOND
      // screen_list/Search call, held pending by canonicalSearchResolve above.
      let deleteButtonEl: HTMLButtonElement | null = null;
      await waitFor(() => {
        deleteButtonEl = container.querySelector(
          '[data-node-id="enum_delete_group_button"] button',
        );
        return deleteButtonEl !== null;
      });
      // preview-gap round: select the row FIRST -- Delete's own preview also needs groupId. Its
      // preview dispatch targets enum_dictionary/delete_group, never screen_list/Search, so it
      // does not disturb this test's own searchCallsSeen counting below.
      const rows = () =>
        Array.from(container.querySelectorAll("tbody tr")) as HTMLTableRowElement[];
      await waitFor(() => rows().length === 1);
      simulateRowClick(rows()[0]);
      await flushUpdates();
      simulateClick(deleteButtonEl!);
      await waitFor(() => queryDialog(container) !== null);
      const confirmButtonEl = queryConfirmButton(container);
      assertExists(confirmButtonEl, "Confirm must be present");
      simulateClick(confirmButtonEl!);
      await waitFor(() => canonicalSearchResolveHolder.current !== null);

      // Fire a passive SSE event WHILE the canonical reread is still pending — this
      // synchronously starts a passive_invalidation call (queued behind the canonical
      // reread in the api_command_lane FIFO; it cannot begin its own fetch yet).
      assert(FakeEventSource.instances.length > 0, "SSE receiver must have connected");
      FakeEventSource.instances[0].emit(
        "projection",
        JSON.stringify({ manifest_id: ADMIN_ENUM_MANIFEST_ID }),
      );
      await flushUpdates();

      // Now resolve the canonical reread — with a FAILURE. Under the old single-counter
      // design this failure would be silently discarded (the shared counter had already
      // moved on when the passive call started) — under the round 29 fix, canonicalGenRef
      // is untouched by a passive call starting, so this failure must still surface.
      assertExists(
        canonicalSearchResolveHolder.current,
        "canonical reread's own call must be pending",
      );
      canonicalSearchResolveHolder.current!({
        success: false,
        errors: [{ code: "DB_UNAVAILABLE", message: "db unavailable" }],
      });

      const warningEl = () => container.querySelector("[data-projection-refresh-warning]");
      await waitFor(() => warningEl() !== null);
      assertExists(
        warningEl(),
        "the canonical reread's own failure must surface as an explicit warning — never " +
          "silently discarded merely because a later passive_invalidation had already started",
      );
      // The old DOM is retained (never blanked) — the probe field still shows the
      // pre-failure value, since the failed canonical reread never produced an Emission.
      assertEquals(probeInput()?.value, "initial");
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

// ─────────────────────────────────────────────────────────────────────────
// Round 3 (preview-gap audit): the shared settlement authority
// (runtime/runtimeDispatchSettlement.ts's resolveRuntimeDispatchSettlement) is the SAME function
// both ProjectionShell's handleRuntimeDispatchResult (error display / canonical-reread decision)
// AND runtimeComponentFactory's deferred localStateMutation gate (Modal open/close) call on every
// settled admin_runtime dispatch result. Before this round, the Modal-mutation gate checked ONLY
// result.success — a success response carrying an unexpected manifestId, or carrying no Emission
// at all, still opened/closed a Modal. These two tests drive delete_group's preview (open) and
// confirm (close) dispatches through all four settlement-rejection shapes
// round_27_28_settled_child_dispatch_result_authority names (wrong manifest, missing Emission,
// backend success:false, and a queue/network rejection) and assert the Modal never mutates and an
// explicit warning is always surfaced — never a silent no-op.
// ─────────────────────────────────────────────────────────────────────────

type NegativeSettlementShape =
  | "wrong_manifest"
  | "missing_emission"
  | "backend_failure"
  | "queue_rejection";

const NEGATIVE_SETTLEMENT_SHAPES: readonly NegativeSettlementShape[] = [
  "wrong_manifest",
  "missing_emission",
  "backend_failure",
  "queue_rejection",
];

const UNEXPECTED_SETTLEMENT_MANIFEST_ID =
  "00000000-0000-0000-0000-0000000ae998";

/**
 * Round 4 (preview-gap audit round 4): "queue_rejection" is handled by a SEPARATE mechanism
 * (installRejectingEnqueueOverride below, at the enqueueRuntimeComponentCommand level), never by
 * a mocked-fetch rejection here — dispatchOperation (frontend/api/dispatch.ts) deliberately
 * converts EVERY fetch/network failure into a normal {success:false} response and never rejects,
 * so a mocked-fetch Promise.reject can only ever re-prove the SAME success:false path
 * "backend_failure" already covers below, never runtimeComponentFactory's own
 * dispatchRuntimeComponentCommandAndForwardResult .catch() branch. Builds the raw HTTP Response a
 * mock fetch should settle a captured /api/dispatch call with, for the 3 shapes that ARE genuine
 * backend response shapes.
 */
function negativeSettlementResponseFor(
  shape: Exclude<NegativeSettlementShape, "queue_rejection">,
  scenarioLabel: string,
): Promise<Response> {
  let responseBody: Record<string, unknown>;
  switch (shape) {
    case "wrong_manifest":
      responseBody = {
        success: true,
        emission: { manifestId: UNEXPECTED_SETTLEMENT_MANIFEST_ID, data: {} },
      };
      break;
    case "missing_emission":
      responseBody = { success: true, errors: [] };
      break;
    case "backend_failure":
      responseBody = {
        success: false,
        errors: [{
          code: "ENUM_SETTLEMENT_REJECTED",
          message: `${scenarioLabel} rejected (${shape} scenario)`,
        }],
      };
      break;
  }
  return Promise.resolve(
    new Response(JSON.stringify(responseBody!), { status: 200 }),
  );
}

/**
 * Round 4: installs a rejecting override for enqueueRuntimeComponentCommand's module-level
 * reference (runtimeComponentFactory.ts's __testOnly seam) that intercepts ONLY the dispatch
 * matching targetRef + the given authority flag ("dryRun" for a preview, "confirmed" for a
 * Confirm) — every other admin_runtime dispatch (e.g. delete_group's own row-select list_groups
 * re-read) passes through to the REAL enqueueRuntimeComponentCommand unchanged, so it still
 * reaches the mock fetch exactly as before. Returns a restore function.
 */
function installRejectingEnqueueOverride(
  targetRef: string,
  authorityFlag: "dryRun" | "confirmed",
  message: string,
): () => void {
  runtimeComponentFactoryTestOnly.setEnqueueRuntimeComponentCommandForTest((dispatchSpec) => {
    const payload = dispatchSpec.payload as Record<string, unknown> | undefined;
    if (dispatchSpec.targetRef === targetRef && payload?.[authorityFlag] === "true") {
      return Promise.reject(new Error(message));
    }
    return enqueueRuntimeComponentCommand(dispatchSpec);
  });
  return () => runtimeComponentFactoryTestOnly.setEnqueueRuntimeComponentCommandForTest(null);
}

Deno.test(
  "ProjectionShell (real mount, round 3 settlement authority): delete_group's preview dispatch — wrong manifest / missing Emission / backend success:false / queue rejection settlement shapes never open the Modal, each surfacing an explicit warning",
  async () => {
    const config = CONFIRM_MODAL_SCENARIOS.find((c) => c.label === "delete_group")!;
    for (const shape of NEGATIVE_SETTLEMENT_SHAPES) {
      ensureRuntimeComponentRegistryInitialized();
      schedulerTestOnly.resetCommandQueue();
      FakeEventSource.instances = [];
      const { container, cleanup } = setupDom();
      const originalEventSource =
        (globalThis as unknown as { EventSource?: unknown }).EventSource;
      (globalThis as unknown as { EventSource: unknown }).EventSource =
        FakeEventSource;
      const originalFetch = globalThis.fetch;

      const capturedDispatchBodies: Record<string, unknown>[] = [];
      const mockFetch = ((url: string, init?: RequestInit) => {
        const path = url.toString();
        if (path.startsWith("/api/auth/session") || path === "/api/auth/refresh") {
          return Promise.resolve(
            new Response(JSON.stringify({ success: true }), { status: 200 }),
          );
        }
        if (path !== "/api/dispatch") {
          return Promise.resolve(
            new Response(JSON.stringify({ success: true }), { status: 200 }),
          );
        }
        const body = JSON.parse(String(init?.body ?? "{}")) as Record<string, unknown>;
        capturedDispatchBodies.push(body);
        if (capturedDispatchBodies.length === 1) {
          return Promise.resolve(
            new Response(
              JSON.stringify({
                success: true,
                emission: {
                  manifestId: ADMIN_ENUM_MANIFEST_ID,
                  layoutId: `layout-round3-negative-preview-${shape}`,
                  projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
                  layoutNodes: buildConfirmModalLayoutNodes(config, [
                    { groupId: "row-uuid-1", groupName: "Alpha" },
                  ]),
                },
              }),
              { status: 200 },
            ),
          );
        }
        const payload = payloadOf(body);
        const isPreviewDispatch = payload.target_ref === config.targetRef &&
          payload.dryRun === "true";
        if (isPreviewDispatch) {
          // Round 4: queue_rejection is intercepted BELOW the network entirely (at
          // enqueueRuntimeComponentCommand itself) — fetch must never be reached for it. If it
          // is, the interception isn't actually happening and this test would silently go back
          // to proving the wrong thing (dispatchOperation's success:false conversion).
          if (shape === "queue_rejection") {
            throw new Error(
              `[${shape}] fetch must never be reached for the intercepted dispatch — the ` +
                `enqueueRuntimeComponentCommand override should have rejected before the network`,
            );
          }
          return negativeSettlementResponseFor(shape, "delete_group's preview");
        }
        return Promise.resolve(
          new Response(JSON.stringify(fallbackDispatchResponse(body)), { status: 200 }),
        );
      }) as typeof fetch;
      globalThis.fetch = mockFetch;

      // Round 4: queue_rejection makes enqueueRuntimeComponentCommand's OWN returned promise
      // genuinely reject (dispatchOperation itself never rejects — see negativeSettlementResponseFor's
      // own doc comment) — installed only for THIS shape, restored in finally regardless of shape.
      const restoreEnqueue = shape === "queue_rejection"
        ? installRejectingEnqueueOverride(
          config.targetRef,
          "dryRun",
          "simulated queue/network rejection (delete_group's preview queue_rejection scenario)",
        )
        : null;

      try {
        globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
        render(h(ProjectionShell, {}), container);

        let openButtonEl: HTMLButtonElement | null = null;
        await waitFor(() => {
          openButtonEl = queryOpenButtonFor(container, config.prefix);
          return openButtonEl !== null;
        });
        assertExists(openButtonEl, `[${shape}] delete_group's open trigger must render`);

        const rowEl = container.querySelector("tbody tr") as HTMLTableRowElement | null;
        assertExists(rowEl, `[${shape}] delete_group needs a selectable group row`);
        simulateRowClick(rowEl!);
        await flushUpdates();

        simulateClick(queryOpenButtonFor(container, config.prefix)!);
        await waitFor(() => queryRefreshWarning(container) !== null);
        for (let i = 0; i < 15; i++) await flushUpdates();

        assert(
          queryModalFor(container) === null,
          `[${shape}] delete_group's modal must NOT open on a ${shape} preview settlement`,
        );
        assert(
          queryConfirmButtonFor(container, config.prefix) === null,
          `[${shape}] Confirm must not exist in the DOM while the modal stays closed`,
        );
        assertExists(
          queryRefreshWarning(container),
          `[${shape}] a ${shape} preview settlement must surface an explicit, non-destructive warning`,
        );
        if (shape === "queue_rejection") {
          assert(
            !capturedDispatchBodies.some((b) =>
              payloadOf(b).target_ref === config.targetRef && payloadOf(b).dryRun === "true"
            ),
            `[${shape}] the rejected preview dispatch must never reach the network at all`,
          );
        }
      } finally {
        restoreEnqueue?.();
        globalThis.fetch = originalFetch;
        (globalThis as unknown as { EventSource: unknown }).EventSource =
          originalEventSource;
        schedulerTestOnly.resetCommandQueue();
        render(null, container);
        cleanup();
      }
    }
  },
);

Deno.test(
  "ProjectionShell (real mount, round 3 settlement authority): delete_group's Confirm dispatch — wrong manifest / missing Emission / backend success:false / queue rejection settlement shapes never close the Modal (no state adopted, no resend), each surfacing an explicit warning",
  async () => {
    const config = CONFIRM_MODAL_SCENARIOS.find((c) => c.label === "delete_group")!;
    for (const shape of NEGATIVE_SETTLEMENT_SHAPES) {
      ensureRuntimeComponentRegistryInitialized();
      schedulerTestOnly.resetCommandQueue();
      FakeEventSource.instances = [];
      const { container, cleanup } = setupDom();
      const originalEventSource =
        (globalThis as unknown as { EventSource?: unknown }).EventSource;
      (globalThis as unknown as { EventSource: unknown }).EventSource =
        FakeEventSource;
      const originalFetch = globalThis.fetch;

      const capturedDispatchBodies: Record<string, unknown>[] = [];
      const mockFetch = ((url: string, init?: RequestInit) => {
        const path = url.toString();
        if (path.startsWith("/api/auth/session") || path === "/api/auth/refresh") {
          return Promise.resolve(
            new Response(JSON.stringify({ success: true }), { status: 200 }),
          );
        }
        if (path !== "/api/dispatch") {
          return Promise.resolve(
            new Response(JSON.stringify({ success: true }), { status: 200 }),
          );
        }
        const body = JSON.parse(String(init?.body ?? "{}")) as Record<string, unknown>;
        capturedDispatchBodies.push(body);
        if (capturedDispatchBodies.length === 1) {
          return Promise.resolve(
            new Response(
              JSON.stringify({
                success: true,
                emission: {
                  manifestId: ADMIN_ENUM_MANIFEST_ID,
                  layoutId: `layout-round3-negative-confirm-${shape}`,
                  projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
                  layoutNodes: buildConfirmModalLayoutNodes(config, [
                    { groupId: "row-uuid-1", groupName: "Alpha" },
                  ]),
                },
              }),
              { status: 200 },
            ),
          );
        }
        const payload = payloadOf(body);
        const isPreviewDispatch = payload.target_ref === config.targetRef &&
          payload.dryRun === "true";
        if (isPreviewDispatch) {
          // The preview itself must succeed cleanly so the Modal legitimately opens — this test
          // is about the CONFIRM dispatch's own settlement, not the preview's.
          return Promise.resolve(
            new Response(
              JSON.stringify({
                success: true,
                emission: {
                  manifestId: manifestIdFromTargetRef(config.targetRef),
                  data: { ok: true, dryRun: true },
                },
              }),
              { status: 200 },
            ),
          );
        }
        const isConfirmDispatch = payload.target_ref === config.targetRef &&
          payload.confirmed === "true";
        if (isConfirmDispatch) {
          // Round 4: queue_rejection is intercepted BELOW the network entirely (at
          // enqueueRuntimeComponentCommand itself) — fetch must never be reached for it.
          if (shape === "queue_rejection") {
            throw new Error(
              `[${shape}] fetch must never be reached for the intercepted Confirm dispatch — ` +
                `the enqueueRuntimeComponentCommand override should have rejected before the network`,
            );
          }
          return negativeSettlementResponseFor(shape, "delete_group's Confirm");
        }
        return Promise.resolve(
          new Response(JSON.stringify(fallbackDispatchResponse(body)), { status: 200 }),
        );
      }) as typeof fetch;
      globalThis.fetch = mockFetch;

      try {
        globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
        render(h(ProjectionShell, {}), container);

        let openButtonEl: HTMLButtonElement | null = null;
        await waitFor(() => {
          openButtonEl = queryOpenButtonFor(container, config.prefix);
          return openButtonEl !== null;
        });
        assertExists(openButtonEl, `[${shape}] delete_group's open trigger must render`);

        const rowEl = container.querySelector("tbody tr") as HTMLTableRowElement | null;
        assertExists(rowEl, `[${shape}] delete_group needs a selectable group row`);
        simulateRowClick(rowEl!);
        await flushUpdates();

        simulateClick(queryOpenButtonFor(container, config.prefix)!);
        await waitFor(() => queryModalFor(container) !== null);

        const confirmButtonEl = queryConfirmButtonFor(container, config.prefix);
        assertExists(confirmButtonEl, `[${shape}] Confirm must be present once the modal legitimately opened`);
        const dispatchCountBeforeConfirm = capturedDispatchBodies.length;

        // Round 4: queue_rejection makes enqueueRuntimeComponentCommand's OWN returned promise
        // genuinely reject — installed only for THIS shape, restored in finally regardless.
        const restoreEnqueue = shape === "queue_rejection"
          ? installRejectingEnqueueOverride(
            config.targetRef,
            "confirmed",
            "simulated queue/network rejection (delete_group's Confirm queue_rejection scenario)",
          )
          : null;

        simulateClick(confirmButtonEl!);
        await waitFor(() => queryRefreshWarning(container) !== null);
        for (let i = 0; i < 15; i++) await flushUpdates();

        assertExists(
          queryModalFor(container),
          `[${shape}] delete_group's modal must remain OPEN on a ${shape} Confirm settlement — never closed`,
        );
        assertExists(
          queryConfirmButtonFor(container, config.prefix),
          `[${shape}] Confirm must still be present — the modal was never closed`,
        );
        assertExists(
          queryRefreshWarning(container),
          `[${shape}] a ${shape} Confirm settlement must surface an explicit, non-destructive warning`,
        );
        if (shape === "queue_rejection") {
          // The rejected dispatch never reached the network at all — the capture count must not
          // have grown by the usual "+1 for the Confirm dispatch itself" either.
          assertEquals(
            capturedDispatchBodies.length,
            dispatchCountBeforeConfirm,
            `[${shape}] a queue-rejected Confirm dispatch must never reach the network at all`,
          );
        } else {
          assertEquals(
            capturedDispatchBodies.length,
            dispatchCountBeforeConfirm + 1,
            `[${shape}] a rejected Confirm settlement must never re-send the write itself`,
          );
        }
        restoreEnqueue?.();
      } finally {
        runtimeComponentFactoryTestOnly.setEnqueueRuntimeComponentCommandForTest(null);
        globalThis.fetch = originalFetch;
        (globalThis as unknown as { EventSource: unknown }).EventSource =
          originalEventSource;
        schedulerTestOnly.resetCommandQueue();
        render(null, container);
        cleanup();
      }
    }
  },
);

// ─────────────────────────────────────────────────────────────────────────
// Round 3 (preview-gap audit): the existing canonical-reread proofs (round 27's "child manifest
// response never adopted" test, round 28's "stale tracked value forced to DB-authoritative" and
// "failed canonical reread surfaces a warning" tests, round 29's supersession-priority test) all
// exercise delete_group as their SINGLE write trigger — round 27/28/29 established the MECHANISM
// generically, but no test previously drove all 7 CONFIRM_MODAL_SCENARIOS operations through it.
// This table-driven test closes that gap: for EACH of the 7 operations, a settled Confirm write
// triggers EXACTLY ONE ae200 canonical reread (never zero, never resent, never adopting the
// child manifest's own settled Emission data) via the SAME production ProjectionShell mount path
// every other test in this file uses — never a delete_group-only proof generalized by assertion.
// ─────────────────────────────────────────────────────────────────────────

for (const config of CONFIRM_MODAL_SCENARIOS) {
  Deno.test(
    `ProjectionShell (real mount, round 3 canonical reread proof): ${config.label}'s confirmed write triggers EXACTLY ONE ae200 canonical reread — no write resend, no child Emission data adopted`,
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

      const targetManifestId = manifestIdFromTargetRef(config.targetRef);
      // A canary string embedded ONLY in the confirm dispatch's own settled Emission -- if this
      // EVER reaches the rendered DOM, the child manifest's own response was wrongly adopted
      // into ae200's projection state (round 27's own prohibition).
      const CHILD_EMISSION_CANARY = `CHILD_MANIFEST_CANARY_${config.label}_MUST_NEVER_RENDER`;

      const scenario = buildMockScenario((callIndex, body) => {
        if (callIndex === 1) {
          return {
            success: true,
            emission: {
              manifestId: ADMIN_ENUM_MANIFEST_ID,
              layoutId: `layout-round3-canonical-reread-${config.label}`,
              projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
              layoutNodes: buildConfirmModalLayoutNodes(config, [
                { groupId: "row-uuid-1", groupName: "Alpha" },
              ]),
            },
          };
        }
        const payload = payloadOf(body);
        const isPreviewDispatch = payload.target_ref === config.targetRef &&
          payload.dryRun === "true";
        if (isPreviewDispatch) {
          return {
            success: true,
            emission: { manifestId: targetManifestId, data: { ok: true, dryRun: true } },
          };
        }
        const isConfirmDispatch = payload.target_ref === config.targetRef &&
          payload.confirmed === "true";
        if (isConfirmDispatch) {
          return {
            success: true,
            emission: { manifestId: targetManifestId, data: { ok: true, canary: CHILD_EMISSION_CANARY } },
          };
        }
        // Any OTHER dispatch is ae200's own read circuit (the row-select's own list_groups
        // re-read before Confirm, and the canonical reread after Confirm settles) -- always a
        // real, distinguishable success carrying fresh (post-write) row data, so the canonical
        // reread's own adoption is provably visible in the DOM.
        return {
          success: true,
          emission: {
            manifestId: ADMIN_ENUM_MANIFEST_ID,
            layoutId: `layout-round3-canonical-reread-${config.label}`,
            projectionDefinition: MINIMAL_PROJECTION_DEFINITION,
            layoutNodes: buildConfirmModalLayoutNodes(config, [
              { groupId: "row-uuid-1", groupName: "AlphaAfterWrite" },
            ]),
          },
        };
      });
      globalThis.fetch = scenario.fetch;

      try {
        globalThis.sessionStorage.setItem("demo_jwt_token", fakeJwt());
        render(h(ProjectionShell, {}), container);

        let openButtonEl: HTMLButtonElement | null = null;
        await waitFor(() => {
          openButtonEl = queryOpenButtonFor(container, config.prefix);
          return openButtonEl !== null;
        });
        assertExists(openButtonEl, `${config.label}'s open trigger must render`);

        for (const [nodeId, value] of Object.entries(config.typedFields)) {
          const inputEl = container.querySelector(
            `[data-node-id="${nodeId}"] input`,
          ) as HTMLInputElement | null;
          assertExists(inputEl, `${config.label}'s ${nodeId} input must render`);
          simulateInput(inputEl!, value);
        }
        if (config.needsSelectedGroupRow) {
          const rowEl = container.querySelector(
            "tbody tr",
          ) as HTMLTableRowElement | null;
          assertExists(rowEl, `${config.label} needs a selectable group row`);
          simulateRowClick(rowEl!);
          await flushUpdates();
        }

        simulateClick(queryOpenButtonFor(container, config.prefix)!);
        await waitFor(() => queryModalFor(container) !== null);

        const confirmButtonEl = queryConfirmButtonFor(container, config.prefix);
        assertExists(confirmButtonEl, `${config.label}'s Confirm must be present once the modal opened`);

        const dispatchCountBeforeConfirm = scenario.capturedDispatchBodies.length;
        simulateClick(confirmButtonEl!);
        await waitFor(() => queryModalFor(container) === null);
        // Give the canonical reread (fired only AFTER the confirm dispatch itself settles) time
        // to land, not merely the confirm dispatch's own settlement that closes the modal.
        for (let i = 0; i < 20; i++) await flushUpdates();

        const dispatchesSinceConfirm = scenario.capturedDispatchBodies.length - dispatchCountBeforeConfirm;
        assertEquals(
          dispatchesSinceConfirm,
          2,
          `${config.label}'s settled Confirm write must trigger EXACTLY ONE ae200 canonical ` +
            `reread (the Confirm dispatch itself, plus exactly one canonical-reread redispatch) ` +
            `-- got ${dispatchesSinceConfirm} dispatch(es) since Confirm was clicked`,
        );

        const confirmDispatchesTotal = scenario.capturedDispatchBodies.filter((b) =>
          payloadOf(b).target_ref === config.targetRef && payloadOf(b).confirmed === "true"
        ).length;
        assertEquals(
          confirmDispatchesTotal,
          1,
          `${config.label}'s settled write must never be RESENT once its canonical reread lands`,
        );

        assert(
          !(container.textContent ?? "").includes(CHILD_EMISSION_CANARY),
          `${config.label}'s child manifest response (the Confirm dispatch's own settled ` +
            `Emission) must NEVER be adopted into ae200's rendered projection state`,
        );

        assert(
          (container.querySelector("tbody")?.textContent ?? "").includes("AlphaAfterWrite"),
          `${config.label}'s canonical reread must actually re-dispatch and render ae200's OWN ` +
            `fresh (post-write) data -- the reread is not merely counted, its own response lands`,
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
}
