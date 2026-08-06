import {
  assertEquals,
  assertExists,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildLayoutPreviewPlaceholderProps,
  buildLayoutPreviewRuntimeSpec,
  renderLayoutComponentPreview,
  resolveComponentKindForLayoutPreview,
} from "../runtime/layoutComponentPreview.ts";
import Box from "../components/Box.tsx";
import { ensureRuntimeComponentRegistryInitialized } from "../runtime/runtimeComponentRegistry.ts";
import { __testOnly } from "../runtime/runtimeComponentFactory.ts";
import { __testOnly as schedulerTestOnly } from "../runtime/frontendScheduler.ts";
import type { RuntimeComponentSpec } from "../runtime/runtimeComponentAdapter.ts";
import {
  createRuntimeLocalStateStore,
  createRuntimeStateDispatcher,
} from "../runtime/uiEventEffectRunner.ts";

const ADMIN_ENUM_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae200";

Deno.test("resolveComponentKindForLayoutPreview: catalog maps box.primitive to layout/box", () => {
  assertEquals(
    resolveComponentKindForLayoutPreview("box.primitive"),
    "layout/box",
  );
});

Deno.test("resolveComponentKindForLayoutPreview: prefers explicit hint", () => {
  assertEquals(
    resolveComponentKindForLayoutPreview("custom.key", "action/button"),
    "action/button",
  );
});

Deno.test("buildLayoutPreviewPlaceholderProps: button includes disabled preview label", () => {
  const props = buildLayoutPreviewPlaceholderProps(
    "action/button",
    "button.primitive",
  );
  const data = props.data as Record<string, unknown>;
  assertEquals(data.disabled, true);
  assertExists(data.label);
});

Deno.test("buildLayoutPreviewPlaceholderProps: input omits componentKey caption label", () => {
  const props = buildLayoutPreviewPlaceholderProps(
    "form_input/input",
    "input.primitive",
  );
  const data = props.data as Record<string, unknown>;
  assertEquals("label" in data, false);
  assertEquals(data.placeholder, "プレビュー");
});

Deno.test("buildLayoutPreviewPlaceholderProps: input inlineText overrides placeholder not label", () => {
  const props = buildLayoutPreviewPlaceholderProps(
    "form_input/input",
    "input.primitive",
    {
      inlineText: "氏名",
    },
  );
  const data = props.data as Record<string, unknown>;
  assertEquals(data.placeholder, "氏名");
  assertEquals("label" in data, false);
});

Deno.test("buildLayoutPreviewRuntimeSpec: sets previewMode on spec", () => {
  const built = buildLayoutPreviewRuntimeSpec({
    componentKey: "card.primitive",
  });
  if (!built.ok) throw new Error(built.reason);
  assertEquals(built.spec.previewMode, true);
  assertEquals(built.spec.componentType, "display/card");
});

Deno.test("renderLayoutComponentPreview: button.primitive renders live preview vnode", () => {
  ensureRuntimeComponentRegistryInitialized();
  const result = renderLayoutComponentPreview({
    componentKey: "button.primitive",
    componentKind: "action/button",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
  assertExists(result.node);
});

Deno.test("renderLayoutComponentPreview: box.primitive renders Box preview not Card", () => {
  ensureRuntimeComponentRegistryInitialized();
  const result = renderLayoutComponentPreview({
    componentKey: "box.primitive",
    componentKind: "layout/box",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
  assertEquals(result.node.type, Box);
});

Deno.test("renderLayoutComponentPreview: draft-only is explicit DRAFT_ONLY fallback", () => {
  const result = renderLayoutComponentPreview({
    componentKey: "button.primitive",
    componentKind: "action/button",
    isDraftOnly: true,
  });
  assertEquals(result.ok, false);
  if (result.ok) return;
  assertEquals(result.code, "DRAFT_ONLY");
});

Deno.test("renderLayoutComponentPreview: unknown kind fails explicitly", () => {
  const result = renderLayoutComponentPreview({
    componentKey: "totally.unknown.widget",
  });
  assertEquals(result.ok, false);
  if (result.ok) return;
  assertEquals(result.code, "KIND_UNRESOLVED");
});

Deno.test("button click event binding: previewMode suppresses runtimeDispatch", () => {
  const binding = {
    eventType: "click",
    payload: {},
    runtimeDispatch: {
      operationType: "admin",
      layer: "ui_topology",
      action: "list_packages",
      target: "admin",
    },
  };
  const parsed = __testOnly.parseEventBinding(binding);
  assertExists(parsed);
  assertEquals(parsed?.eventType, "click");
  assertExists(parsed?.runtimeDispatch);
  assertEquals(parsed?.runtimeDispatch?.action, "list_packages");
  const spec = {
    componentId: "test-button",
    componentType: "action/button",
    props: { data: { label: "Click" } },
    eventBinding: { click: binding },
    previewMode: true,
  };
  const emitResult = __testOnly.emitBoundEvent(spec, "click", {});
  assertEquals(emitResult.ok, true);
});

Deno.test("row_detail_drawer.primitive renders in preview mode with inert toggle", () => {
  ensureRuntimeComponentRegistryInitialized();
  const result = renderLayoutComponentPreview({
    componentKey: "row_detail_drawer.primitive",
    componentKind: "table_op/row_detail_drawer",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
  assertExists(result.node);
});

Deno.test("modal.template renders in preview mode via disclosure/modal factory", () => {
  ensureRuntimeComponentRegistryInitialized();
  const result = renderLayoutComponentPreview({
    componentKey: "modal.template",
    componentKind: "disclosure/modal",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
  assertExists(result.node);
});

Deno.test("renderLayoutComponentPreview: card_list.primitive renders preview vnode", () => {
  ensureRuntimeComponentRegistryInitialized();
  const result = renderLayoutComponentPreview({
    componentKey: "card_list.primitive",
    componentKind: "display/card_list",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
  assertExists(result.node);
});

Deno.test("buildLayoutPreviewPlaceholderProps: card_list returns items array with two sample cards", () => {
  const props = buildLayoutPreviewPlaceholderProps(
    "display/card_list",
    "card_list.primitive",
  );
  const items = props.items as unknown[];
  assertExists(items);
  assertEquals(Array.isArray(items), true);
  assertEquals(items.length, 2);
});

Deno.test("buildLayoutPreviewPlaceholderProps: card does NOT include items array (single-record only)", () => {
  const props = buildLayoutPreviewPlaceholderProps(
    "display/card",
    "card.primitive",
  );
  assertEquals("items" in props, false);
  const data = props.data as Record<string, unknown> | undefined;
  assertExists(data);
  assertExists(data.title);
});

Deno.test("renderLayoutComponentPreview: aggregation_preview_table.primitive renders preview vnode", () => {
  ensureRuntimeComponentRegistryInitialized();
  const result = renderLayoutComponentPreview({
    componentKey: "aggregation_preview_table.primitive",
    componentKind: "calc_topology/aggregation_preview_table",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
  assertExists(result.node);
});

Deno.test("renderLayoutComponentPreview: hub_statistics_panel.primitive renders preview vnode", () => {
  ensureRuntimeComponentRegistryInitialized();
  const result = renderLayoutComponentPreview({
    componentKey: "hub_statistics_panel.primitive",
    componentKind: "calc_topology/hub_statistics_panel",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
  assertExists(result.node);
});

// ─── emitBoundEvent: node value tracking lane (write side of payloadFromNodeValues,
// frontend/runtime/liveNodeValueTracker.ts) ──────────────────────────────────

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
  const result = __testOnly.emitBoundEvent(spec, "change", {
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
  const result = __testOnly.emitBoundEvent(spec, "click", {});
  assertEquals(result.ok, true);
  assertEquals(captured.length, 0);
});

// ─── emitBoundEvent: admin_runtime Lane 2 payloadFrom resolution (reuses the SAME
// resolvePayloadFrom() single authority dispatchExternalPort/dispatchInstanceOperation
// use) ─────────────────────────────────────────────────────────────────────

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
    const nodeValues: Record<string, unknown> = { "node-name-input": "Status" };
    const spec: RuntimeComponentSpec = {
      componentId: "comp-submit-button-001",
      packageId: null,
      layoutId: "layout-admin-runtime-payloadfrom-001",
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
            payloadFrom: { groupName: "node:node-name-input.value" },
          },
        },
      },
      payloadFromNodeValues: nodeValues,
    };
    const clickResult = __testOnly.emitBoundEvent(spec, "click", {});
    assertEquals(clickResult.ok, true);
    for (let i = 0; i < 20 && capturedBody === null; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    assertExists(
      capturedBody,
      "the api_command_lane request body must have been captured",
    );
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

Deno.test("emitBoundEvent: admin_runtime Lane 2 forwards the settled dispatch result to spec.onRuntimeDispatchResult (previously void-discarded, PR #600 review round 12)", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  const serverEmissionData = { ok: true, dryRun: true, preview: { groupName: "demo_status" } };
  globalThis.fetch = ((_url: string) => {
    return Promise.resolve(
      new Response(
        JSON.stringify({
          success: true,
          errors: [],
          emission: { data: serverEmissionData },
        }),
        { status: 200 },
      ),
    );
  }) as typeof fetch;
  try {
    let forwardedResult: unknown = null;
    let forwardedContext: { targetRef?: string; dryRun: boolean } | null = null;
    const spec: RuntimeComponentSpec = {
      componentId: "comp-load-button-001",
      packageId: null,
      layoutId: "layout-admin-runtime-dispatch-result-001",
      wiringId: null,
      componentType: "action/button",
      props: { data: { label: "Load current values" } },
      eventBinding: {
        click: {
          eventType: "click",
          runtimeDispatch: {
            operationType: "update_group",
            target: "manifest",
            layer: "enum_dictionary",
            action: "update_group",
            targetRef:
              `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:update_group`,
            payloadFrom: { groupId: "node:group-id-input.value" },
          },
        },
      },
      payloadFromNodeValues: { "group-id-input": "11111111-1111-1111-1111-111111111111" },
      onRuntimeDispatchResult: (result, context) => {
        forwardedResult = result;
        forwardedContext = context;
      },
    };
    const clickResult = __testOnly.emitBoundEvent(spec, "click", {});
    assertEquals(clickResult.ok, true);
    for (let i = 0; i < 20 && forwardedResult === null; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    assertExists(forwardedResult, "onRuntimeDispatchResult must have been called");
    const result = forwardedResult as {
      success: boolean;
      emission?: { data?: unknown };
    };
    assertEquals(result.success, true);
    assertEquals(result.emission?.data, serverEmissionData);
    // Round 29: the authored targetRef actually dispatched must be forwarded verbatim as context,
    // so a caller can confirm a settled response really landed on the manifest it targeted.
    // preview-gap round: this dispatch's payloadFrom carries no dryRun field, so context.dryRun
    // must be false — the resolved payload actually sent, never assumed.
    assertEquals(
      forwardedContext,
      {
        targetRef: `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:update_group`,
        dryRun: false,
      },
    );
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

Deno.test("emitBoundEvent: admin_runtime Lane 2 never calls onRuntimeDispatchResult when the caller did not wire one in (today's fire-and-forget behavior unchanged)", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  let fetchCalled = false;
  globalThis.fetch = ((_url: string) => {
    fetchCalled = true;
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, errors: [] }), { status: 200 }),
    );
  }) as typeof fetch;
  try {
    const spec: RuntimeComponentSpec = {
      componentId: "comp-no-forward-001",
      packageId: null,
      layoutId: "layout-admin-runtime-dispatch-result-002",
      wiringId: null,
      componentType: "action/button",
      props: { data: { label: "Preview" } },
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
            payloadFrom: { groupName: "node:node-name-input.value" },
          },
        },
      },
      payloadFromNodeValues: { "node-name-input": "Status" },
      // onRuntimeDispatchResult intentionally absent.
    };
    const clickResult = __testOnly.emitBoundEvent(spec, "click", {});
    assertEquals(clickResult.ok, true);
    for (let i = 0; i < 20 && !fetchCalled; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    assertEquals(fetchCalled, true, "the dispatch must still have fired");
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

Deno.test("emitBoundEvent: admin_runtime Lane 2's dispatchRuntimeComponentCommandAndForwardResult catch() branch — a GENUINE enqueueRuntimeComponentCommand promise rejection (not a mocked-fetch success:false conversion) forwards a synthetic failure to onRuntimeDispatchResult and never applies the deferred localStateMutation", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  // Round 4 (preview-gap audit round 4): dispatchOperation (frontend/api/dispatch.ts) never
  // rejects — every fetch/network failure it catches becomes a normal {success:false} response,
  // so a mocked-fetch rejection can NEVER reach dispatchRuntimeComponentCommandAndForwardResult's
  // own .catch() branch through the real call chain; it only ever re-proves the SAME
  // success:false path a "backend_failure" scenario already covers. The module-level
  // enqueueRuntimeComponentCommand override __testOnly exposes is the real seam: it makes
  // enqueueRuntimeComponentCommand's OWN returned promise genuinely reject, exactly the shape
  // this catch() branch exists to handle.
  const originalFetch = globalThis.fetch;
  let fetchCalled = false;
  globalThis.fetch = ((_url: string) => {
    fetchCalled = true;
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, errors: [] }), { status: 200 }),
    );
  }) as typeof fetch;
  __testOnly.setEnqueueRuntimeComponentCommandForTest(() =>
    Promise.reject(new Error("simulated queue/network rejection"))
  );
  try {
    const rawStore = createRuntimeLocalStateStore();
    const dispatcher = createRuntimeStateDispatcher(rawStore);
    dispatcher.declare("confirm_modal", "open", false);

    let forwardedResult: unknown = null;
    let forwardedContext: unknown = null;
    const spec: RuntimeComponentSpec = {
      componentId: "comp-queue-rejection-001",
      packageId: null,
      layoutId: "layout-queue-rejection-001",
      wiringId: null,
      componentType: "action/button",
      props: { data: { label: "Confirm" } },
      eventBinding: {
        click: {
          eventType: "click",
          runtimeDispatch: {
            operationType: "delete_group",
            target: "manifest",
            layer: "enum_dictionary",
            action: "delete_group",
            targetRef:
              `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:delete_group`,
            payloadFrom: { groupId: "node:group-id-input.value", confirmed: "literal:true" },
          },
          // Round 25 deferred-gate shape: a trigger carrying BOTH runtimeDispatch and
          // localStateMutation only applies the mutation once the dispatch settles
          // successfully — this is the SAME shape a real Confirm button's closeModal uses.
          localStateMutation: {
            targetNodeId: "confirm_modal",
            statePath: "open",
            action: "set",
            value: false,
          },
        },
      },
      payloadFromNodeValues: { "group-id-input": "11111111-1111-1111-1111-111111111111" },
      localStateStore: dispatcher,
      onRuntimeDispatchResult: (result, context) => {
        forwardedResult = result;
        forwardedContext = context;
      },
    };
    const clickResult = __testOnly.emitBoundEvent(spec, "click", {});
    assertEquals(clickResult.ok, true);
    for (let i = 0; i < 20 && forwardedResult === null; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    assertEquals(fetchCalled, false, "a rejected enqueueRuntimeComponentCommand must never reach fetch at all");
    assertExists(forwardedResult, "the catch() branch must forward a synthetic failure to onRuntimeDispatchResult");
    const result = forwardedResult as { success: boolean; errors?: Array<{ message?: string }> };
    assertEquals(result.success, false, "the synthetic forwarded result must be success:false");
    assertExists(
      result.errors?.[0]?.message?.includes("simulated queue/network rejection"),
      "the synthetic failure message must carry the real rejection's own message",
    );
    assertEquals(
      forwardedContext,
      {
        targetRef: `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:delete_group`,
        dryRun: false,
      },
    );
    // The deferred localStateMutation gate (onSettled, the SAME mechanism that opens/closes a
    // Modal in production) must never have applied — the modal's own tracked state stays at its
    // predeclared initial value (false), never flipped to the queued mutation's value.
    assertEquals(
      dispatcher.get("confirm_modal", "open"),
      false,
      "onSettled (Modal open/close) must never fire on a queue/network rejection",
    );
  } finally {
    __testOnly.setEnqueueRuntimeComponentCommandForTest(null);
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
    const result = __testOnly.emitBoundEvent(spec, "click", {});
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
    const result = __testOnly.emitBoundEvent(spec, "select", {
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
    const result = __testOnly.emitBoundEvent(spec, "select", {
      row: {
        groupId: "11111111-1111-1111-1111-111111111111",
        groupName: "Status",
      },
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
    const result = __testOnly.emitBoundEvent(spec, "click", {
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

// ─── parseEventBinding: payloadFrom runtime parse boundary fail-close
// (PR #599 review round 3) — a SEPARATE boundary from renderEmission.ts's
// build-time dispatchPayloadFromByTrigger validation. parseEventBinding
// re-parses spec.eventBinding[trigger] (untyped JSON) at dispatch time; build-time
// validation existing elsewhere must never be treated as a substitute for this
// boundary's own fail-close ──

function runtimeDispatchBindingWithPayloadFrom(
  payloadFrom: unknown,
): Record<string, unknown> {
  return {
    eventType: "click",
    runtimeDispatch: {
      operationType: "create_group",
      target: "manifest",
      layer: "enum_dictionary",
      action: "create_group",
      targetRef:
        `manifest:${ADMIN_ENUM_MANIFEST_ID}:enum_dictionary:create_group`,
      payloadFrom,
    },
  };
}

Deno.test("parseEventBinding: non-object payloadFrom fails the whole binding closed (null), never silently dropped to 'no payloadFrom'", () => {
  const parsed = __testOnly.parseEventBinding(
    runtimeDispatchBindingWithPayloadFrom("not-an-object"),
  );
  assertEquals(parsed, null);
});

Deno.test("parseEventBinding: empty-object payloadFrom fails the whole binding closed (null)", () => {
  const parsed = __testOnly.parseEventBinding(
    runtimeDispatchBindingWithPayloadFrom({}),
  );
  assertEquals(parsed, null);
});

Deno.test("parseEventBinding: a non-string payloadFrom field value fails the whole binding closed (null), never filtered out of the map", () => {
  const parsed = __testOnly.parseEventBinding(
    runtimeDispatchBindingWithPayloadFrom({
      groupName: "node:name_input.value",
      indexNum: 42,
    }),
  );
  assertEquals(parsed, null);
});

Deno.test("parseEventBinding: an array payloadFrom fails the whole binding closed (null)", () => {
  const parsed = __testOnly.parseEventBinding(
    runtimeDispatchBindingWithPayloadFrom(["node:name_input.value"]),
  );
  assertEquals(parsed, null);
});

Deno.test("parseEventBinding: valid payloadFrom parses through unchanged", () => {
  const parsed = __testOnly.parseEventBinding(
    runtimeDispatchBindingWithPayloadFrom({
      groupName: "node:name_input.value",
    }),
  );
  assertExists(parsed);
  assertExists(parsed!.runtimeDispatch);
  assertEquals(parsed!.runtimeDispatch!.payloadFrom, {
    groupName: "node:name_input.value",
  });
});

Deno.test("parseEventBinding: absent payloadFrom parses through as undefined (raw passthrough contract unchanged)", () => {
  const parsed = __testOnly.parseEventBinding({
    eventType: "click",
    runtimeDispatch: {
      operationType: "list_groups",
      target: "manifest",
      layer: "enum_dictionary",
      action: "list_groups",
    },
  });
  assertExists(parsed);
  assertExists(parsed!.runtimeDispatch);
  assertEquals(parsed!.runtimeDispatch!.payloadFrom, undefined);
});

Deno.test("emitBoundEvent: malformed payloadFrom at the dispatch-time parse boundary fails close (RUNTIME_PRIMITIVE_RENDERER_INVALID_EVENT_BINDING) and never enqueues, even if it somehow bypassed build-time validation", () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (() => new Promise<Response>(() => {})) as typeof fetch;
  try {
    const spec: RuntimeComponentSpec = {
      componentId: "comp-malformed-parse-001",
      packageId: null,
      layoutId: "layout-malformed-parse-001",
      wiringId: null,
      componentType: "action/button",
      props: { data: { label: "Create" } },
      // Hand-built eventBinding bypassing renderEmission.ts's builder entirely —
      // simulates any future/alternate construction path that doesn't go through
      // buildAdminRuntimePayloadFromByTrigger, proving this parse boundary's
      // fail-close is real and not merely inherited from the builder.
      eventBinding: runtimeDispatchBindingWithPayloadFrom({
        groupName: "node:name_input.value",
        indexNum: 42,
      }),
      payloadFromNodeValues: { "name_input": "Status" },
    };
    const result = __testOnly.emitBoundEvent(spec, "click", {});
    assertEquals(result.ok, false);
    if (!result.ok) {
      assertEquals(
        result.error.includes(
          "RUNTIME_PRIMITIVE_RENDERER_INVALID_EVENT_BINDING",
        ),
        true,
      );
    }
    assertEquals(schedulerTestOnly.getCommandQueueLength(), 0);
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

Deno.test("emitBoundEvent: two DISTINCT layout nodes sharing the SAME catalog componentId (e.g. two Fields both instantiating form_input/search_input, whose componentId is the CATALOG component definition's own registry identity, not a per-instance one) maintain fully independent debounce timers and dispatch sequencing -- neither node's debounced dispatch cancels or overwrites the other's (round 38)", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  const capturedBodies: Record<string, unknown>[] = [];
  globalThis.fetch = ((_url: string, init?: RequestInit) => {
    capturedBodies.push(JSON.parse(String(init?.body ?? "{}")));
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, errors: [] }), { status: 200 }),
    );
  }) as typeof fetch;
  try {
    const SHARED_CATALOG_COMPONENT_ID = "comp-shared-search-input-catalog-001";
    const buildSpec = (
      nodeId: string,
      inputNodeId: string,
      targetLayer: string,
    ): RuntimeComponentSpec => ({
      // SAME componentId on both specs -- the exact shape two independent Fields both
      // instantiating the same catalog component (e.g. "search_input.alias") actually have
      // in production, since componentId is the catalog registry's own identity.
      componentId: SHARED_CATALOG_COMPONENT_ID,
      nodeId,
      packageId: null,
      layoutId: "layout-shared-componentid-001",
      wiringId: null,
      componentType: "form_input/search_input",
      props: { data: { value: "" } },
      debounceMs: 30,
      eventBinding: {
        change: {
          eventType: "change",
          runtimeDispatch: {
            operationType: "list_x",
            target: "manifest",
            layer: targetLayer,
            action: "list_x",
            targetRef: `manifest:${ADMIN_ENUM_MANIFEST_ID}:${targetLayer}:list_x`,
            payloadFrom: { q: `node:${inputNodeId}.value` },
          },
        },
      },
      payloadFromNodeValues: { [inputNodeId]: nodeId === "node-A" ? "alpha" : "beta" },
    });
    const specA = buildSpec("node-A", "input-A", "layer_a");
    const specB = buildSpec("node-B", "input-B", "layer_b");

    // Fire A's own debounced dispatch, then IMMEDIATELY fire B's own debounced dispatch
    // (before A's 30ms timer has elapsed). If componentId were still used as the identity
    // (the round-37 bug this proves fixed), scheduling B would cancel A's still-pending
    // timer since both specs share one componentId -- A's dispatch would then never fire
    // at all.
    const resultA = __testOnly.emitBoundEvent(specA, "change", { value: "alpha" });
    assertEquals(resultA.ok, true);
    const resultB = __testOnly.emitBoundEvent(specB, "change", { value: "beta" });
    assertEquals(resultB.ok, true);

    for (let i = 0; i < 40 && capturedBodies.length < 2; i++) {
      await new Promise((resolve) => setTimeout(resolve, 5));
    }
    assertEquals(
      capturedBodies.length,
      2,
      "both A's and B's own debounced dispatch must independently fire -- neither node's own componentId-sharing with the other silently cancels its timer",
    );
    const layers = capturedBodies.map((b) => b.layer);
    assertEquals(
      new Set(layers),
      new Set(["layer_a", "layer_b"]),
      "each node's dispatch must reach ITS OWN targetRef/layer, not the other node's",
    );
    const bodyA = capturedBodies.find((b) => b.layer === "layer_a")!;
    const bodyB = capturedBodies.find((b) => b.layer === "layer_b")!;
    assertEquals((bodyA.payload as Record<string, unknown>).q, "alpha");
    assertEquals((bodyB.payload as Record<string, unknown>).q, "beta");
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

// round 39: search_filter_input_contract debounce_policy runtime enforcement -- a
// componentType="form_input/search_input" Field on the admin_runtime dispatch lane must fail
// closed BEFORE any dispatch attempt when its own debounceMs is missing/invalid, never falling
// through to an immediate dispatch (the silent "invalid debounceMs == no debounce" fallback the
// governance NG axis prohibits). Mirrors the backend persistence-boundary matrix
// (NpgsqlUiTopologyRepositoryLayoutPatchValidationTests.cs) and the translator's own
// FIELD_ADMIN_RUNTIME_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS rule. ────────────────────────────────

function buildSearchInputSpec(
  debounceMs: unknown,
  componentType = "form_input/search_input",
): RuntimeComponentSpec {
  return {
    componentId: "comp-search-input-debounce-fail-close-001",
    nodeId: "search-debounce-fail-close",
    packageId: null,
    layoutId: "layout-search-debounce-fail-close-001",
    wiringId: null,
    componentType,
    props: { data: { value: "" } },
    debounceMs: debounceMs as RuntimeComponentSpec["debounceMs"],
    eventBinding: {
      change: {
        eventType: "change",
        runtimeDispatch: {
          operationType: "list_x",
          target: "manifest",
          layer: "layer_a",
          action: "list_x",
          targetRef: `manifest:${ADMIN_ENUM_MANIFEST_ID}:layer_a:list_x`,
          payloadFrom: { q: "node:search-debounce-fail-close.value" },
        },
      },
    },
    payloadFromNodeValues: { "search-debounce-fail-close": "typed value" },
  };
}

Deno.test("emitBoundEvent: a form_input/search_input Field on the admin_runtime dispatch lane fails closed (no dispatch, no timer scheduled) when its own debounceMs is missing, zero, negative, non-integer, boolean, or a string -- never silently falls through to an immediate dispatch", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  let fetchCallCount = 0;
  globalThis.fetch = (() => {
    fetchCallCount++;
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, errors: [] }), { status: 200 }),
    );
  }) as typeof fetch;
  try {
    const invalidDebounceValues: unknown[] = [
      undefined,
      0,
      -5,
      1.5,
      true,
      "300",
    ];
    for (const invalid of invalidDebounceValues) {
      fetchCallCount = 0;
      const spec = buildSearchInputSpec(invalid);
      const result = __testOnly.emitBoundEvent(spec, "change", { value: "x" });
      assertEquals(
        result.ok,
        false,
        `debounceMs=${JSON.stringify(invalid)} must fail close synchronously, never schedule or dispatch`,
      );
      if (!result.ok) {
        assertEquals(
          result.error.includes("FIELD_ADMIN_RUNTIME_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS"),
          true,
          `error must carry the FIELD_ADMIN_RUNTIME_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS code, got: ${result.error}`,
        );
      }
      // Give any (incorrectly) scheduled timer a chance to fire before asserting it never did.
      await new Promise((resolve) => setTimeout(resolve, 20));
      assertEquals(
        fetchCallCount,
        0,
        `debounceMs=${JSON.stringify(invalid)} must never reach the network, immediately or after a delay`,
      );
    }
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

Deno.test("emitBoundEvent: a form_input/search_input Field with a valid positive-integer debounceMs dispatches normally (fail-close does not regress the legitimate case)", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  let fetchCallCount = 0;
  globalThis.fetch = (() => {
    fetchCallCount++;
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, errors: [] }), { status: 200 }),
    );
  }) as typeof fetch;
  try {
    const spec = buildSearchInputSpec(20);
    const result = __testOnly.emitBoundEvent(spec, "change", { value: "x" });
    assertEquals(result.ok, true);
    for (let i = 0; i < 40 && fetchCallCount === 0; i++) {
      await new Promise((resolve) => setTimeout(resolve, 5));
    }
    assertEquals(fetchCallCount, 1);
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

Deno.test("emitBoundEvent: a NON-search_input Field (e.g. form_input/select, discrete-choice) on the admin_runtime dispatch lane is NEVER required to declare debounceMs -- dispatches immediately with no debounceMs authored, matching the discrete-choice exemption exactly", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  let fetchCallCount = 0;
  globalThis.fetch = (() => {
    fetchCallCount++;
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, errors: [] }), { status: 200 }),
    );
  }) as typeof fetch;
  try {
    const spec = buildSearchInputSpec(undefined, "form_input/select");
    const result = __testOnly.emitBoundEvent(spec, "change", { value: "x" });
    assertEquals(result.ok, true);
    for (let i = 0; i < 40 && fetchCallCount === 0; i++) {
      await new Promise((resolve) => setTimeout(resolve, 5));
    }
    assertEquals(fetchCallCount, 1);
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});
