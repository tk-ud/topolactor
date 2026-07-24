import {
  assertEquals,
  assertExists,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  createLiveNodeValueTracker,
} from "../runtime/liveNodeValueTracker.ts";
import {
  buildCatalogComponentEventBinding,
  buildRuntimeDispatchSpec,
  renderEmission,
} from "../runtime/renderEmission.ts";
import type { RuntimeDispatchSpec } from "../runtime/frontendScheduler.ts";
import { ensureRuntimeComponentRegistryInitialized } from "../runtime/runtimeComponentRegistry.ts";
import { __testOnly as factoryTestOnly } from "../runtime/runtimeComponentFactory.ts";
import { __testOnly as schedulerTestOnly } from "../runtime/frontendScheduler.ts";
import type { RuntimeComponentSpec } from "../runtime/runtimeComponentAdapter.ts";
import type { Emission } from "../api/dispatch.ts";

const ADMIN_ENUM_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae200";

// ─── liveNodeValueTracker: register / update / deregister ───────────────────

Deno.test("createLiveNodeValueTracker: set() registers a node's value", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("node-1", "Status");
  assertEquals(tracker.snapshot(), { "node-1": "Status" });
});

Deno.test("createLiveNodeValueTracker: set() updates an already-registered node's value", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("node-1", "Status");
  tracker.set("node-1", "Priority");
  assertEquals(tracker.snapshot(), { "node-1": "Priority" });
});

Deno.test("createLiveNodeValueTracker: snapshot() returns the SAME object reference across calls", () => {
  const tracker = createLiveNodeValueTracker();
  const first = tracker.snapshot();
  tracker.set("node-1", "x");
  const second = tracker.snapshot();
  assertEquals(first, second, "same reference must reflect later mutations");
  assertEquals(first === second, true);
});

Deno.test("createLiveNodeValueTracker: reconcile() deregisters nodes absent from the current node list", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("node-1", "a");
  tracker.set("node-2", "b");
  tracker.reconcile(["node-1"]);
  assertEquals(tracker.snapshot(), { "node-1": "a" });
});

Deno.test("createLiveNodeValueTracker: reconcile() keeps values for nodes still present", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("node-1", "a");
  tracker.reconcile(["node-1", "node-2"]);
  assertEquals(tracker.snapshot(), { "node-1": "a" });
});

Deno.test("createLiveNodeValueTracker: set() with empty nodeId is a no-op (fail-close, no anonymous key)", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("", "x");
  assertEquals(tracker.snapshot(), {});
});

// ─── emitBoundEvent: node value tracking lane (write side of payloadFromNodeValues) ──

Deno.test("emitBoundEvent: onNodeValueChange fires with the node's own value on change", () => {
  ensureRuntimeComponentRegistryInitialized();
  const captured: unknown[] = [];
  const spec: RuntimeComponentSpec = {
    componentId: "comp-input-001",
    packageId: null,
    layoutId: "layout-001",
    wiringId: null,
    componentType: "form_input/input",
    props: { data: { value: "" } },
    eventBinding: {
      change: { eventType: "change" },
    },
    onNodeValueChange: (value) => captured.push(value),
  };
  const result = factoryTestOnly.emitBoundEvent(spec, "change", {
    value: "新規グループ",
  });
  assertEquals(result.ok, true);
  assertEquals(captured, ["新規グループ"]);
});

Deno.test("emitBoundEvent: onNodeValueChange does not fire when payload carries no 'value' key", () => {
  ensureRuntimeComponentRegistryInitialized();
  const captured: unknown[] = [];
  const spec: RuntimeComponentSpec = {
    componentId: "comp-btn-001",
    packageId: null,
    layoutId: "layout-001",
    wiringId: null,
    componentType: "action/button",
    props: { data: { label: "Go" } },
    eventBinding: { click: { eventType: "click" } },
    onNodeValueChange: (value) => captured.push(value),
  };
  const result = factoryTestOnly.emitBoundEvent(spec, "click", {});
  assertEquals(result.ok, true);
  assertEquals(captured.length, 0);
});

// ─── renderEmission: onNodeValueChange option wires per-node closures ───────

Deno.test("renderEmission: onNodeValueChange option registers per-node closures carrying nodeId", () => {
  ensureRuntimeComponentRegistryInitialized();
  const captured: Array<[string, unknown]> = [];
  const emission: Emission = {
    layoutId: "layout-nv-001",
    layoutNodes: [
      {
        nodeId: "node-nv-1",
        nodeKind: "catalog_component",
        componentId: "comp-nv-001",
        componentKind: "form_input/input",
        componentKey: "text_input.primitive",
        orderIndex: 0,
      },
    ],
  };
  const specs = renderEmission(emission, {}, {
    onNodeValueChange: (nodeId, value) => captured.push([nodeId, value]),
  });
  assertExists(specs[0].runtimeSpec);
  // No wiringKind/runtimeInteractions authored on this node, so it carries no
  // "change" event binding — emitBoundEvent still returns ok:false
  // (RUNTIME_PRIMITIVE_RENDERER_INVALID_EVENT_BINDING) for the dispatch lanes,
  // but the node-value-tracking lane fires unconditionally BEFORE that check,
  // exactly like calcTriggerCallback already does.
  const result = factoryTestOnly.emitBoundEvent(
    specs[0].runtimeSpec!,
    "change",
    { value: "hello" },
  );
  assertEquals(result.ok, false);
  assertEquals(captured, [["node-nv-1", "hello"]]);
});

// ─── Lane 2 payloadFrom resolution (admin_runtime, "bindRuntimeDispatchPayload") ──

Deno.test("buildAdminRuntimePayloadFromByTrigger via buildCatalogComponentEventBinding: payloadFrom attaches to the matching trigger only", () => {
  const spec: RuntimeDispatchSpec = {
    operationType: "create_group",
    target: "manifest",
    layer: "enum_dictionary",
    action: "create_group",
    targetRef: `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:create_group`,
  };
  const binding = buildCatalogComponentEventBinding(spec, {
    click: { groupName: "node:name_input.value" },
  });
  const clickBinding = binding.click as Record<string, unknown>;
  const clickRd = clickBinding.runtimeDispatch as Record<string, unknown>;
  assertEquals(clickRd.payloadFrom, { groupName: "node:name_input.value" });

  const submitBinding = binding.submit as Record<string, unknown>;
  const submitRd = submitBinding.runtimeDispatch as Record<string, unknown>;
  assertEquals(submitRd.payloadFrom, undefined);
});

Deno.test("emitBoundEvent: admin_runtime Lane 2 resolves payloadFrom from live node values (node:<id>.value) as the sole payload", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  let capturedBody: Record<string, unknown> | null = null;
  globalThis.fetch = ((_url: string, init?: RequestInit) => {
    capturedBody = JSON.parse(String(init?.body ?? "{}"));
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, errors: [] }), {
        status: 200,
      }),
    );
  }) as typeof fetch;
  try {
    const emission: Emission = {
      layoutId: "layout-admin-runtime-payloadfrom-001",
      layoutNodes: [
        {
          nodeId: "node-name-input",
          nodeKind: "catalog_component",
          componentId: "comp-name-input-001",
          componentKind: "form_input/input",
          componentKey: "text_input.primitive",
          orderIndex: 0,
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
              payloadFrom: { groupName: "node:node-name-input.value" },
            },
          ],
        },
      ],
    };
    const specs = renderEmission(emission, {}, {
      onNodeValueChange: () => {},
    });
    const inputSpec = specs.find((s) => s.nodeId === "node-name-input");
    const buttonSpec = specs.find((s) => s.nodeId === "node-submit-button");
    assertExists(inputSpec?.runtimeSpec);
    assertExists(buttonSpec?.runtimeSpec);

    // Live node value tracking: typing into the name input registers its value
    // under a shared payloadFromNodeValues snapshot BEFORE the button click —
    // the node-value-tracking lane itself is covered in isolation above, so
    // here the shared snapshot is populated directly via the SAME tracker
    // write path (spec.onNodeValueChange) to keep this test focused on Lane 2
    // payloadFrom resolution.
    const nodeValues: Record<string, unknown> = {};
    inputSpec!.runtimeSpec!.payloadFromNodeValues = nodeValues;
    buttonSpec!.runtimeSpec!.payloadFromNodeValues = nodeValues;
    inputSpec!.runtimeSpec!.onNodeValueChange = (value) => {
      nodeValues["node-name-input"] = value;
    };
    const changeResult = factoryTestOnly.emitBoundEvent(
      inputSpec!.runtimeSpec!,
      "change",
      { value: "Status" },
    );
    // The input node carries no wiringKind/runtimeInteractions of its own, so
    // it has no "change" dispatch binding (ok:false) — but the node-value
    // tracking lane still ran before that check, so nodeValues is populated.
    assertEquals(changeResult.ok, false);
    assertEquals(nodeValues["node-name-input"], "Status");

    const clickResult = factoryTestOnly.emitBoundEvent(
      buttonSpec!.runtimeSpec!,
      "click",
      {},
    );
    assertEquals(clickResult.ok, true);
    for (let i = 0; i < 20 && capturedBody === null; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    assertExists(capturedBody, "the api_command_lane request body must have been captured");
    const body = capturedBody as Record<string, unknown>;
    assertEquals(body.layer, "enum_dictionary");
    assertEquals(body.action, "create_group");
    const payload = body.payload as Record<string, unknown>;
    // payloadFrom resolution supplies the DATA fields; enqueueRuntimeComponentCommand
    // still appends target_ref (manifest-resolution routing metadata, not app
    // payload data) on top — same precedent as the existing raw-passthrough case.
    assertEquals(payload, {
      groupName: "Status",
      target_ref:
        `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:create_group`,
    });
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

Deno.test("emitBoundEvent: admin_runtime Lane 2 fails close on unresolved node value (missing node) and never enqueues", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (() => new Promise<Response>(() => {})) as typeof fetch;
  try {
    const spec: RuntimeComponentSpec = {
      componentId: "comp-missing-node-001",
      packageId: null,
      layoutId: "layout-missing-node-001",
      wiringId: null,
      componentType: "action/button",
      props: { data: { label: "Create" } },
      eventBinding: {
        click: {
          eventType: "click",
          runtimeDispatch: {
            operationType: "create_group",
            target: "manifest",
            layer: "enum_dictionary",
            action: "create_group",
            targetRef:
              `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:create_group`,
            payloadFrom: { groupName: "node:does-not-exist.value" },
          },
        },
      },
      payloadFromNodeValues: {},
    };
    const result = factoryTestOnly.emitBoundEvent(spec, "click", {});
    assertEquals(result.ok, false);
    if (!result.ok) {
      assertEquals(result.error.includes("PAYLOAD_FROM_NODE_NOT_FOUND"), true);
    }
    assertEquals(
      schedulerTestOnly.getCommandQueueLength(),
      0,
      "enqueueRuntimeComponentCommand must not run when payloadFrom resolution fails",
    );
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

Deno.test("emitBoundEvent: admin_runtime Lane 2 fails close on malformed event path", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (() => new Promise<Response>(() => {})) as typeof fetch;
  try {
    const spec: RuntimeComponentSpec = {
      componentId: "comp-bad-path-001",
      packageId: null,
      layoutId: "layout-bad-path-001",
      wiringId: null,
      componentType: "action/button",
      props: { data: { label: "Delete" } },
      eventBinding: {
        select: {
          eventType: "select",
          runtimeDispatch: {
            operationType: "delete_group",
            target: "manifest",
            layer: "enum_dictionary",
            action: "delete_group",
            targetRef:
              `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:delete_group`,
            payloadFrom: { groupId: "event.row.groupId" },
          },
        },
      },
      payloadFromNodeValues: {},
    };
    // event payload doesn't carry .row at all — malformed/absent path.
    const result = factoryTestOnly.emitBoundEvent(spec, "select", {
      unrelated: true,
    });
    assertEquals(result.ok, false);
    if (!result.ok) {
      assertEquals(
        result.error.includes("PAYLOAD_FROM_EVENT_PATH_NOT_FOUND"),
        true,
      );
    }
    assertEquals(schedulerTestOnly.getCommandQueueLength(), 0);
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

Deno.test("emitBoundEvent: admin_runtime Lane 2 resolves event.<path> from the trigger's OWN native payload (table row select)", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  let capturedBody: Record<string, unknown> | null = null;
  globalThis.fetch = ((_url: string, init?: RequestInit) => {
    capturedBody = JSON.parse(String(init?.body ?? "{}"));
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, errors: [] }), {
        status: 200,
      }),
    );
  }) as typeof fetch;
  try {
    const spec: RuntimeComponentSpec = {
      componentId: "comp-row-delete-001",
      packageId: null,
      layoutId: "layout-row-delete-001",
      wiringId: null,
      componentType: "data_display/table",
      props: { table: { columns: [], rows: [] } },
      eventBinding: {
        select: {
          eventType: "select",
          runtimeDispatch: {
            operationType: "delete_group",
            target: "manifest",
            layer: "enum_dictionary",
            action: "delete_group",
            targetRef:
              `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:delete_group`,
            payloadFrom: { groupId: "event.row.groupId" },
          },
        },
      },
      payloadFromNodeValues: {},
    };
    const result = factoryTestOnly.emitBoundEvent(spec, "select", {
      row: { groupId: "11111111-1111-1111-1111-111111111111", groupName: "Status" },
    });
    assertEquals(result.ok, true);
    for (let i = 0; i < 20 && capturedBody === null; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    assertExists(capturedBody);
    const body = capturedBody as Record<string, unknown>;
    assertEquals(body.action, "delete_group");
    const payload = body.payload as Record<string, unknown>;
    assertEquals(payload, {
      groupId: "11111111-1111-1111-1111-111111111111",
      target_ref:
        `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:delete_group`,
    });
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

Deno.test("emitBoundEvent: admin_runtime Lane 2 without payloadFrom keeps the pre-existing raw event-time payload passthrough unchanged", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  let capturedBody: Record<string, unknown> | null = null;
  globalThis.fetch = ((_url: string, init?: RequestInit) => {
    capturedBody = JSON.parse(String(init?.body ?? "{}"));
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, errors: [] }), {
        status: 200,
      }),
    );
  }) as typeof fetch;
  try {
    const spec: RuntimeComponentSpec = {
      componentId: "comp-legacy-passthrough-001",
      packageId: null,
      layoutId: "layout-legacy-passthrough-001",
      wiringId: null,
      componentType: "action/button",
      props: { data: { label: "List" } },
      eventBinding: {
        click: {
          eventType: "click",
          runtimeDispatch: {
            operationType: "list_groups",
            target: "manifest",
            layer: "enum_dictionary",
            action: "list_groups",
            targetRef:
              `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:list_groups`,
          },
        },
      },
    };
    const result = factoryTestOnly.emitBoundEvent(spec, "click", {
      whatever: "value",
    });
    assertEquals(result.ok, true);
    for (let i = 0; i < 20 && capturedBody === null; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    assertExists(capturedBody);
    const body = capturedBody as Record<string, unknown>;
    const payload = body.payload as Record<string, unknown>;
    assertEquals(payload.whatever, "value");
    assertEquals(
      payload.target_ref,
      `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:list_groups`,
    );
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});
