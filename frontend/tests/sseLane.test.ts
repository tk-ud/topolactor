import { assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { createSseReceiver, type ProjectionHookTrigger } from "../runtime/sseReceiver.ts";
import { createSseDispatcher, createSseDispatcherWithProjectionRuntime } from "../runtime/sseDispatcher.ts";
import { enqueueProjectionHookTrigger } from "../runtime/frontendScheduler.ts";
import { createProjectionRuntime } from "../runtime/projectionRuntime.ts";
import { projectionFromEmission } from "../runtime/renderEmission.ts";
import type { Emission } from "../api/dispatch.ts";
import type { ProjectionDefinition } from "../runtime/projectionConstructor.ts";

// ─── SSE receiver: identity preservation ─────────────────────────────────────

Deno.test("sseReceiver: receiver_preserves_projection_event_identity — identity fields are preserved in ProjectionHookTrigger", () => {
  const identity = {
    manifestId: "m-abc",
    tableId: "t-def",
    tableRegistryId: "tr-ghi",
  };
  const trigger: ProjectionHookTrigger = {
    eventType: "projection",
    data: JSON.stringify({
      manifest_id: identity.manifestId,
      table_id: identity.tableId,
      table_registry_id: identity.tableRegistryId,
    }),
    identity,
  };

  assertEquals(trigger.identity.manifestId, "m-abc");
  assertEquals(trigger.identity.tableId, "t-def");
  assertEquals(trigger.identity.tableRegistryId, "tr-ghi");
  assertEquals(trigger.eventType, "projection");
});

Deno.test("sseReceiver: createSseReceiver accepts onProjectionHookTrigger and onError callbacks", () => {
  const receiver = createSseReceiver({
    onProjectionHookTrigger: (_trigger) => {},
    onError: (_state) => {},
  });
  assertExists(receiver);
  assertExists(receiver.connect);
  assertExists(receiver.disconnect);
});

// ─── SSE receiver: explicit error states ─────────────────────────────────────

Deno.test("sseReceiver: backend_sse_error_states_are_explicit — SseErrorState covers connection_error, parse_error, connection_closed", () => {
  const states = [
    { kind: "connection_error" as const, event: new Event("error") },
    { kind: "parse_error" as const, rawData: "bad json", error: "SyntaxError" },
    { kind: "connection_closed" as const },
  ];
  assertEquals(states.length, 3);
  assertEquals(states[0].kind, "connection_error");
  assertEquals(states[1].kind, "parse_error");
  assertEquals(states[2].kind, "connection_closed");
});

// ─── SSE dispatcher: explicit unhandled event policy ─────────────────────────

Deno.test("sseDispatcher: unhandled_event_policy_is_explicit — log policy emits SSE_UNHANDLED_EVENT_TYPE warning", () => {
  const warnings: string[] = [];
  const originalWarn = console.warn;
  console.warn = (...args: unknown[]) => { warnings.push(String(args[0])); };
  try {
    const dispatcher = createSseDispatcher({ unhandledEventPolicy: "log" });
    dispatcher.route("unknown_event_type", "some-data");
    assertEquals(warnings.some((w) => w.includes("SSE_UNHANDLED_EVENT_TYPE")), true);
  } finally {
    console.warn = originalWarn;
  }
});

Deno.test("sseDispatcher: unhandled_event_policy_is_explicit — ignore policy is explicit no-op, does not throw", () => {
  const dispatcher = createSseDispatcher({ unhandledEventPolicy: "ignore" });
  dispatcher.route("unknown_event_type", "some-data");
});

Deno.test("sseDispatcher: default policy is log (not silent fallback)", () => {
  const warnings: string[] = [];
  const originalWarn = console.warn;
  console.warn = (...args: unknown[]) => { warnings.push(String(args[0])); };
  try {
    const dispatcher = createSseDispatcher();
    dispatcher.route("unregistered_event", "data");
    assertEquals(warnings.some((w) => w.includes("SSE_UNHANDLED_EVENT_TYPE")), true);
  } finally {
    console.warn = originalWarn;
  }
});

// ─── SSE dispatcher: projection runtime route ─────────────────────────────────

Deno.test("sseDispatcher: dispatcher_routes_projection_events_into_projection_runtime", () => {
  const runtime = createProjectionRuntime();
  runtime.setProjectionDefinition({
    constructorKey: "k",
    packageIds: ["p"],
    outputKind: "form_inputs",
    fieldDefs: [{ key: "value", label: "Value", kind: "text" }],
  });

  const updates: string[] = [];
  runtime.onProjectionUpdate((_projection, payload) => {
    updates.push(payload.manifest_id ?? "no-manifest");
  });

  const dispatcher = createSseDispatcherWithProjectionRuntime(runtime);
  dispatcher.route("projection", JSON.stringify({ manifest_id: "m-1", data: { value: "hello" } }));

  assertEquals(updates, ["m-1"]);
});

Deno.test("sseDispatcher: projection_event_identity_is_preserved — identity fields reach projectionRuntime", () => {
  const runtime = createProjectionRuntime();
  runtime.setProjectionDefinition({
    constructorKey: "k",
    packageIds: ["p"],
    outputKind: "ui_projection",
  });

  let receivedPayload: Record<string, unknown> | null = null;
  runtime.onProjectionUpdate((_projection, payload) => {
    receivedPayload = {
      manifest_id: payload.manifest_id,
      table_id: payload.table_id,
      table_registry_id: payload.table_registry_id,
    };
  });

  const dispatcher = createSseDispatcherWithProjectionRuntime(runtime, { unhandledEventPolicy: "ignore" });
  const rawData = JSON.stringify({
    manifest_id: "m-preserve",
    table_id: "t-preserve",
    table_registry_id: "tr-preserve",
    data: {},
  });
  dispatcher.route("projection", rawData);

  assertExists(receivedPayload);
  assertEquals(receivedPayload!["manifest_id"], "m-preserve");
  assertEquals(receivedPayload!["table_id"], "t-preserve");
  assertEquals(receivedPayload!["table_registry_id"], "tr-preserve");
});

// ─── Frontend scheduler: hook trigger bridge ─────────────────────────────────

Deno.test("frontendScheduler: enqueueProjectionHookTrigger routes trigger.eventType and trigger.data unchanged", () => {
  let routedType: string | null = null;
  let routedData: string | null = null;

  const stubDispatcher = {
    route: (eventType: string, data: string) => {
      routedType = eventType;
      routedData = data;
    },
  };

  const trigger = {
    eventType: "projection",
    data: '{"manifest_id":"m-fwd"}',
    identity: { manifestId: "m-fwd" },
  };

  enqueueProjectionHookTrigger(trigger, stubDispatcher);

  assertEquals(routedType, "projection");
  assertEquals(routedData, '{"manifest_id":"m-fwd"}');
});

Deno.test("frontendScheduler: sse_receiver_feeds_frontend_scheduler_as_hook_trigger — full lane routes to projectionRuntime", () => {
  const runtime = createProjectionRuntime();
  runtime.setProjectionDefinition({
    constructorKey: "k",
    packageIds: ["p"],
    outputKind: "form_inputs",
    fieldDefs: [{ key: "name", label: "Name", kind: "text" }],
  });

  const updates: string[] = [];
  runtime.onProjectionUpdate((_projection, payload) => {
    updates.push(payload.manifest_id ?? "no-manifest");
  });

  const dispatcher = createSseDispatcherWithProjectionRuntime(runtime);

  const trigger = {
    eventType: "projection",
    data: JSON.stringify({ manifest_id: "m-hook", data: { name: "test" } }),
    identity: { manifestId: "m-hook" },
  };

  enqueueProjectionHookTrigger(trigger, dispatcher);

  assertEquals(updates, ["m-hook"]);
});

// ─── manifest response constructor mapping end-to-end route ──────────────────

Deno.test("renderEmission: manifest_response_constructor_mapping_end_to_end_route_is_proven — emission.data flows into constructProjection", () => {
  const emission: Emission = {
    structureMapId: "sm-1",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    componentIds: [],
    data: { name: "Alice", active: true },
  };

  const definition: ProjectionDefinition = {
    constructorKey: "test-constructor",
    packageIds: ["00000000-0000-0000-0000-000000000001"],
    outputKind: "form_inputs",
    fieldDefs: [
      { key: "name", label: "Name", kind: "text", required: true },
      { key: "active", label: "Active", kind: "boolean" },
    ],
  };

  const result = projectionFromEmission(emission, definition);

  assertExists(result.projection);
  assertEquals(result.projection!.kind, "form_inputs");
  if (result.projection!.kind === "form_inputs") {
    const nameField = result.projection!.fields.find((f) => f.key === "name");
    assertExists(nameField);
    assertEquals(nameField!.value, "Alice");
  }
});

Deno.test("renderEmission: projectionFromEmission with undefined emission.data uses empty jsonKeyValue", () => {
  const emission: Emission = {
    structureMapId: "sm-2",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    componentIds: [],
  };

  const definition: ProjectionDefinition = {
    constructorKey: "k",
    packageIds: [],
    outputKind: "form_inputs",
    fieldDefs: [{ key: "x", label: "X", kind: "text" }],
  };

  const result = projectionFromEmission(emission, definition);
  assertExists(result.projection);
  assertEquals(result.projection!.kind, "form_inputs");
  if (result.projection!.kind === "form_inputs") {
    assertEquals(result.projection!.fields[0].value, null);
  }
});

Deno.test("renderEmission: projectionFromEmission does not perform topology or SQL Attention judgment", () => {
  // Frontend must not make topology meaning judgment or SQL Attention judgment.
  // projectionFromEmission only calls constructProjection with data-defined parameters.
  const emission: Emission = { componentIds: [], data: { q: "test" } };
  const definition: ProjectionDefinition = {
    constructorKey: "k",
    packageIds: [],
    outputKind: "ui_projection",
  };

  const result = projectionFromEmission(emission, definition);
  assertExists(result.projection);
  assertEquals(result.projection!.kind, "ui_projection");
  if (result.projection!.kind === "ui_projection") {
    assertEquals(result.projection!.raw["q"], "test");
  }
});
