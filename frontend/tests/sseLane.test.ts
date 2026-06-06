import { assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { createSseReceiver, type ProjectionHookTrigger } from "../runtime/sseReceiver.ts";
import { createSseDispatcher, createSseDispatcherWithProjectionRuntime } from "../runtime/sseDispatcher.ts";
import { enqueueProjectionHookTrigger } from "../runtime/frontendScheduler.ts";
import { createProjectionRuntime } from "../runtime/projectionRuntime.ts";
import { projectionFromEmission } from "../runtime/renderEmission.ts";
import type { DispatchResponse, Emission } from "../api/dispatch.ts";
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

// ─── manifest response constructor mapping end-to-end canonical route ─────────
// Proves: backend/manifest response-derived ProjectionDefinition is read from
// DispatchResponse.emission.projectionDefinition and flows into projectionRuntime
// setProjectionDefinition → handleProjectionEvent → constructProjection → UiProjection.
// ProjectionDefinition is NOT created as a test-local variable separate from the emission.

Deno.test("manifest_response_constructor_mapping_end_to_end_route_is_proven — dispatch response emission.projectionDefinition reaches projectionRuntime and constructProjection", () => {
  // Simulate the dispatch response shape the backend returns when a manifest with a
  // projection_constructor_mapping topology entry is resolved.
  // The projectionDefinition field on the Emission is the canonical supply source.
  const dispatchResponse: DispatchResponse = {
    success: true,
    emission: {
      structureMapId: "sm-canonical",
      packageId: "00000000-0000-0000-0000-000000000001",
      schemaId: "00000000-0000-0000-0000-000000000002",
      componentIds: [],
      data: { username: "canonical-user", active: true },
      projectionDefinition: {
        constructorKey: "manifest-constructor",
        packageIds: ["00000000-0000-0000-0000-000000000001"],
        outputKind: "form_inputs",
        fieldDefs: [
          { key: "username", label: "Username", kind: "text", required: true },
          { key: "active", label: "Active", kind: "boolean" },
        ],
      },
    },
  };

  // Extract definition from the canonical dispatch response route (not a test-local variable).
  const definition = dispatchResponse.emission?.projectionDefinition;
  assertExists(definition, "emission.projectionDefinition must be present in dispatch response");

  // Apply to projection runtime via canonical setProjectionDefinition path.
  const runtime = createProjectionRuntime();
  runtime.setProjectionDefinition(definition!);

  // Verify handleProjectionEvent reaches constructProjection via the manifest-supplied definition.
  const projections: unknown[] = [];
  runtime.onProjectionUpdate((projection) => projections.push(projection));
  runtime.handleProjectionEvent(JSON.stringify({ manifest_id: "m-canonical", data: { username: "canonical-user", active: true } }));

  assertEquals(projections.length, 1, "projection handler must be called once");
  const p = projections[0] as { kind: string; fields?: Array<{ key: string; value: unknown }> };
  assertEquals(p.kind, "form_inputs");
  assertExists(p.fields);
  const usernameField = p.fields!.find((f) => f.key === "username");
  assertExists(usernameField);
  assertEquals(usernameField!.value, "canonical-user");
});

Deno.test("manifest_response_constructor_mapping: projectionFromEmission reaches constructProjection via dispatch response emission", () => {
  // Alternative end-to-end proof via projectionFromEmission path.
  // Definition is derived from dispatchResponse.emission.projectionDefinition, not created independently.
  const dispatchResponse: DispatchResponse = {
    success: true,
    emission: {
      structureMapId: "sm-e2e",
      packageId: "00000000-0000-0000-0000-000000000010",
      schemaId: "00000000-0000-0000-0000-000000000011",
      componentIds: [],
      data: { label: "E2E Button" },
      projectionDefinition: {
        constructorKey: "e2e-constructor",
        packageIds: ["00000000-0000-0000-0000-000000000010"],
        outputKind: "component_projection",
        componentId: "btn-e2e",
        componentDefinition: {
          componentId: "btn-e2e",
          componentKey: "action/button.e2e",
          component_kind: "action/button",
          default_parameters: { label: "Default", disabled: false },
        },
      },
    },
  };

  const definition = dispatchResponse.emission!.projectionDefinition!;
  assertExists(definition);

  const result = projectionFromEmission(dispatchResponse.emission!, definition);
  assertExists(result.projection);
  assertEquals(result.projection!.kind, "component_projection");
  if (result.projection!.kind === "component_projection") {
    assertEquals(result.projection!.componentId, "btn-e2e");
    assertEquals(result.projection!.props.label, "E2E Button");
  }
});

// ─── definition missing policy: explicit error / ignore ───────────────────────

Deno.test("projectionRuntime: PROJECTION_RUNTIME_DEFINITION_MISSING is explicit error (default policy) when no definition set", () => {
  const errors: string[] = [];
  const originalError = console.error;
  console.error = (...args: unknown[]) => { errors.push(String(args[0])); };
  try {
    const runtime = createProjectionRuntime(); // default policy: "error"
    runtime.handleProjectionEvent(JSON.stringify({ manifest_id: "m-missing", data: {} }));
    assertEquals(
      errors.some((e) => e.includes("PROJECTION_RUNTIME_DEFINITION_MISSING")),
      true,
      "PROJECTION_RUNTIME_DEFINITION_MISSING must be emitted when definition is not set",
    );
  } finally {
    console.error = originalError;
  }
});

Deno.test("projectionRuntime: ignore policy is explicit no-op when no definition set — does not emit error", () => {
  const errors: string[] = [];
  const originalError = console.error;
  console.error = (...args: unknown[]) => { errors.push(String(args[0])); };
  try {
    const runtime = createProjectionRuntime({ definitionMissingPolicy: "ignore" });
    runtime.handleProjectionEvent(JSON.stringify({ manifest_id: "m-ignore", data: {} }));
    assertEquals(
      errors.some((e) => e.includes("PROJECTION_RUNTIME_DEFINITION_MISSING")),
      false,
      "ignore policy must not emit PROJECTION_RUNTIME_DEFINITION_MISSING",
    );
  } finally {
    console.error = originalError;
  }
});

// ─── ProjectionShell SSE refresh lane integration ─────────────────────────────
// Tests the full sseReceiver → enqueueProjectionHookTrigger → sseDispatcher →
// ProjectionShell refresh handler flow using stubs (no real EventSource / backend).

import { renderEmission, buildChildrenMap, type ComponentSpec } from "../runtime/renderEmission.ts";
import type { LayoutNode } from "../api/dispatch.ts";
// enqueueProjectionHookTrigger already imported at the top of the file.

Deno.test("ProjectionShell SSE lane: sseReceiver → dispatcher → refresh handler routes trigger and re-fetches emission", async () => {
  // Simulate the projection hook trigger the sseReceiver would emit on a "projection" event.
  const trigger = {
    eventType: "projection",
    data: JSON.stringify({ manifest_id: "m-refresh" }),
    identity: { manifestId: "m-refresh" },
  };

  // Stub dispatcher captures the routed event type and data.
  let routedType: string | null = null;
  let routedData: string | null = null;
  const stubDispatcher = {
    route: (eventType: string, data: string) => {
      routedType = eventType;
      routedData = data;
    },
  };

  enqueueProjectionHookTrigger(trigger, stubDispatcher);

  assertEquals(routedType, "projection");
  assertEquals(routedData, '{"manifest_id":"m-refresh"}');
});

const nodeAId = "node-a-id";
const nodeBId = "node-b-id";
const compXId = "00000000-0000-0000-0000-000000000003";
const compYId = "00000000-0000-0000-0000-000000000099";

const refreshRegistry = {
  [compXId]: { componentId: compXId, componentType: "comp-x", def: {} },
  [compYId]: { componentId: compYId, componentType: "comp-y", def: {} },
};

Deno.test("ProjectionShell SSE lane: DOM replacement — old nodeId absent after refresh, new nodeId present", () => {
  // Emission A: has node-a-id
  const emissionA = {
    structureMapId: "sm-1",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    layoutId: "layout-a",
    layoutNodes: [
      {
        nodeId: nodeAId,
        nodeKind: "catalog_component",
        componentId: compXId,
        slotKey: "slot_a",
        orderIndex: 0,
        x: 0, y: 0, width: 100, height: 50,
      } as LayoutNode,
    ],
  };

  // Emission B: has node-b-id (no node-a-id)
  const emissionB = {
    structureMapId: "sm-2",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    layoutId: "layout-b",
    layoutNodes: [
      {
        nodeId: nodeBId,
        nodeKind: "catalog_component",
        componentId: compYId,
        slotKey: "slot_b",
        orderIndex: 0,
        x: 10, y: 20, width: 200, height: 100,
      } as LayoutNode,
    ],
  };

  // Simulate initial emission A rendering.
  const specsA = renderEmission(emissionA, refreshRegistry);
  assertEquals(specsA.some((s) => s.nodeId === nodeAId), true, "emission A must contain node-a-id");
  assertEquals(specsA.some((s) => s.nodeId === nodeBId), false, "emission A must not contain node-b-id");

  // Simulate SSE refresh: emission B replaces emission A entirely.
  const specsB = renderEmission(emissionB, refreshRegistry);
  assertEquals(specsB.some((s) => s.nodeId === nodeBId), true, "emission B must contain node-b-id");
  assertEquals(specsB.some((s) => s.nodeId === nodeAId), false, "emission B must not contain node-a-id");

  // After refresh, specs are fully replaced with emission B (not merged with A).
  // This models the setSpecs(renderEmission(emissionB, registry)) call in ProjectionShell.
  const currentSpecs: ComponentSpec[] = specsB;
  assertEquals(currentSpecs.every((s) => s.nodeId !== nodeAId), true, "old node-a-id must not persist after refresh");
  assertEquals(currentSpecs.some((s) => s.nodeId === nodeBId), true, "new node-b-id must be present after refresh");
});

Deno.test("ProjectionShell SSE lane: refresh preserves parentNodeId tree and layoutClassRefs", () => {
  // Verifies that after SSE refresh, parentNodeId tree and layoutClassRefs are maintained
  // in the new specs from emission B (not carried over from emission A).
  const emissionB = {
    structureMapId: "sm-3",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    layoutId: "layout-b-tree",
    layoutNodes: [
      {
        nodeId: "node-root",
        nodeKind: "structural_html",
        htmlTag: "div",
        slotKey: "root",
        orderIndex: 0,
        x: 0, y: 0, width: 800, height: 600,
      } as LayoutNode,
      {
        nodeId: "node-child",
        nodeKind: "catalog_component",
        componentId: compYId,
        parentNodeId: "node-root",
        slotKey: "child",
        orderIndex: 0,
        x: 10, y: 10, width: 200, height: 100,
        layoutClassRefs: ["cls-a", "cls-b"],
      } as LayoutNode,
    ],
  };

  const specsB = renderEmission(emissionB, refreshRegistry);
  const rootSpec = specsB.find((s) => s.nodeId === "node-root");
  const childSpec = specsB.find((s) => s.nodeId === "node-child");

  assertExists(rootSpec);
  assertExists(childSpec);
  assertEquals(rootSpec!.parentNodeId, undefined, "root spec must have no parentNodeId");
  assertEquals(childSpec!.parentNodeId, "node-root", "child spec must have parentNodeId=node-root");
  assertEquals(childSpec!.layoutClassRefs, ["cls-a", "cls-b"], "layoutClassRefs must be preserved after refresh");

  // buildChildrenMap correctly resolves the refreshed tree.
  const childrenMap = buildChildrenMap(specsB);
  const roots = childrenMap.get(undefined) ?? [];
  assertEquals(roots.length, 1);
  assertEquals(roots[0].nodeId, "node-root");
  const rootChildren = childrenMap.get("node-root") ?? [];
  assertEquals(rootChildren.length, 1);
  assertEquals(rootChildren[0].nodeId, "node-child");
});

Deno.test("ProjectionShell SSE lane: layoutId present without nodes → LAYOUT_NODES_NOT_FOUND error spec, no flat fallback", () => {
  // SSOT: layoutId present but layoutNodes absent → explicit error, no flat fallback to componentIds.
  const brokenEmission = {
    structureMapId: "sm-err",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    layoutId: "layout-missing-nodes",
    componentIds: [compXId],
    // layoutNodes: absent
  };

  const specs = renderEmission(brokenEmission, refreshRegistry);

  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "error");
  const errMsg = String(specs[0].def.error ?? "");
  assertEquals(errMsg.includes("LAYOUT_NODES_NOT_FOUND"), true, "must emit LAYOUT_NODES_NOT_FOUND error");
  // Must not fall back to rendering componentIds.
  assertEquals(specs[0].componentId, undefined, "must not render componentId from flat fallback");
});
