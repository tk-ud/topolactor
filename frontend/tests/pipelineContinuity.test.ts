import { assertEquals, assertExists, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { createSseDispatcher } from "../runtime/sseDispatcher.ts";
import { queueClientCommand } from "../runtime/frontendScheduler.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import type { Emission, LayoutNode } from "../api/dispatch.ts";

// ─── SSE projection lane — dispatcher skeleton ────────────────────────────────

Deno.test("sseDispatcher: registered handler is called on matching eventType", () => {
  const dispatcher = createSseDispatcher();
  let received: string | null = null;

  dispatcher.register("projection", (data) => {
    received = data;
  });
  dispatcher.route("projection", "test-payload");

  assertEquals(received, "test-payload");
});

Deno.test("sseDispatcher: unregistered eventType is ignored without error", () => {
  const dispatcher = createSseDispatcher();

  // Must not throw for unregistered type
  dispatcher.route("unknown-event", "data");
});

Deno.test("sseDispatcher: multiple event types can be registered independently", () => {
  const dispatcher = createSseDispatcher();
  const received: Record<string, string> = {};

  dispatcher.register("projection", (data) => { received["projection"] = data; });
  dispatcher.register("ping", (data) => { received["ping"] = data; });

  dispatcher.route("projection", "proj-data");
  dispatcher.route("ping", "ping-data");

  assertEquals(received["projection"], "proj-data");
  assertEquals(received["ping"], "ping-data");
});

// ─── Frontend scheduler skeleton ─────────────────────────────────────────────

Deno.test("frontendScheduler: queueClientCommand shape matches DispatchRequest contract", async () => {
  // Verifies that queueClientCommand constructs a valid DispatchRequest.
  // No real backend — fetch will fail; we verify the error is explicit (not silent fallback).
  // See docs/design/pipeline-continuity-ssot.yaml api_command_lane.frontend.scheduler.
  //
  // Note: dispatchOperation fetches "/api/dispatch" (relative URL). In Deno,
  // new Request(relativeUrl, init) throws — so we capture input/init directly
  // and parse init.body instead of constructing a Request object.
  let capturedInput: string | URL | Request | null = null;
  let capturedInit: RequestInit | undefined;

  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (input: string | URL | Request, init?: RequestInit) => {
    capturedInput = input;
    capturedInit = init;
    throw new Error("TEST_NO_NETWORK");
  };

  try {
    const result = await queueClientCommand({
      operationType: "Search",
      target: "default",
      layer: "entity",
      action: "Search",
    });

    assertEquals(result.success, false);
    assertExists(result.errors);

    assertExists(capturedInput);
    assertExists(capturedInit);
    const body = JSON.parse(capturedInit!.body as string);
    assertEquals(body.target, "default");
    assertEquals(body.layer, "entity");
    assertEquals(body.action, "Search");
    assertEquals(body.triggerKind, "client", "queueClientCommand must inject triggerKind='client' per client_command_lane SSOT");
    assertEquals("role" in body, false, "role must NOT be in frontend dispatch body");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

// ─── SSE projection lane — pipeline identity ─────────────────────────────────

Deno.test("pipeline identity (SSE lane): component_ids from emission project to non-error specs", () => {
  // Verifies the projection terminal node of the SSE lane:
  // emission.componentIds → renderEmission → ComponentSpec[] with non-error componentType.
  // See docs/design/pipeline-continuity-ssot.yaml sse_projection_lane.required_identity.
  const emission: Emission = {
    structureMapId: "00000000-0000-0000-0000-000000000004",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    componentIds: ["00000000-0000-0000-0000-000000000003"],
  };

  const specs = renderEmission(emission, defaultComponentRegistry);

  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType !== "error", true);
});

// ─── Layout DOM projection — full field coverage ──────────────────────────────

const compAId = "00000000-0000-0000-0000-000000000003";
const compBId = "00000000-0000-0000-0000-000000000099";

const twoCompRegistry = {
  ...defaultComponentRegistry,
  [compAId]: { componentId: compAId, componentType: "default", def: { label: "comp-A" } },
  [compBId]: { componentId: compBId, componentType: "secondary", def: { label: "comp-B" } },
};

Deno.test("layout DOM: structural_html node renders with componentType='structural_html' and htmlTag", () => {
  const layoutNodes: LayoutNode[] = [
    {
      nodeId: "node-section",
      nodeKind: "structural_html",
      htmlTag: "section",
      slotKey: "wrapper",
      orderIndex: 0,
      x: 0,
      y: 0,
      width: 800,
      height: 600,
    },
  ];

  const emission: Emission = {
    structureMapId: "00000000-0000-0000-0000-000000000004",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    layoutId: "layout-dom-test",
    layoutNodes,
  };

  const specs = renderEmission(emission, twoCompRegistry);

  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "structural_html");
  assertEquals(specs[0].htmlTag, "section");
  assertEquals(specs[0].nodeId, "node-section");
  assertEquals(specs[0].nodeKind, "structural_html");
  assertEquals(specs[0].x, 0);
  assertEquals(specs[0].y, 0);
  assertEquals(specs[0].width, 800);
  assertEquals(specs[0].height, 600);
});

Deno.test("layout DOM: catalog_component node carries all layout projection fields", () => {
  const layoutNodes: LayoutNode[] = [
    {
      nodeId: "node-card",
      nodeKind: "catalog_component",
      componentId: compAId,
      parentNodeId: undefined,
      slotKey: "slot_a",
      orderIndex: 0,
      x: 10,
      y: 20,
      width: 300,
      height: 150,
      layoutClassRefs: ["card-primary", "elevated"],
    },
  ];

  const emission: Emission = {
    structureMapId: "00000000-0000-0000-0000-000000000004",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    layoutId: "layout-dom-test",
    layoutNodes,
  };

  const specs = renderEmission(emission, twoCompRegistry);

  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "default");
  assertEquals(specs[0].componentId, compAId);
  assertEquals(specs[0].nodeId, "node-card");
  assertEquals(specs[0].nodeKind, "catalog_component");
  assertEquals(specs[0].slotKey, "slot_a");
  assertEquals(specs[0].orderIndex, 0);
  assertEquals(specs[0].x, 10);
  assertEquals(specs[0].y, 20);
  assertEquals(specs[0].width, 300);
  assertEquals(specs[0].height, 150);
  assertEquals(specs[0].layoutClassRefs?.length, 2);
  assertEquals(specs[0].layoutClassRefs?.[0], "card-primary");
  assertEquals(specs[0].layoutClassRefs?.[1], "elevated");
});

Deno.test("layout DOM: parentNodeId is preserved in ComponentSpec for tree building", () => {
  // Verifies that parentNodeId flows from LayoutNode → ComponentSpec so ProjectionShell
  // can build the DOM tree without additional data from the emission.
  const layoutNodes: LayoutNode[] = [
    {
      nodeId: "node-root",
      nodeKind: "structural_html",
      htmlTag: "div",
      slotKey: "root",
      orderIndex: 0,
      x: 0, y: 0, width: 800, height: 600,
    },
    {
      nodeId: "node-child",
      nodeKind: "catalog_component",
      componentId: compAId,
      parentNodeId: "node-root",
      slotKey: "child",
      orderIndex: 0,
      x: 10, y: 10, width: 200, height: 100,
    },
  ];

  const emission: Emission = {
    structureMapId: "00000000-0000-0000-0000-000000000004",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    layoutId: "layout-dom-test",
    layoutNodes,
  };

  const specs = renderEmission(emission, twoCompRegistry);

  assertEquals(specs.length, 2);

  const rootSpec = specs.find((s) => s.nodeId === "node-root");
  const childSpec = specs.find((s) => s.nodeId === "node-child");

  assertExists(rootSpec);
  assertExists(childSpec);
  assertEquals(rootSpec!.parentNodeId, undefined);
  assertEquals(childSpec!.parentNodeId, "node-root");
  assertEquals(childSpec!.componentId, compAId);
});

Deno.test("layout DOM: DB-equivalent emission with full node fields projects correctly", () => {
  // Closes the layout_patch_json.nodes[] → frontend DOM projection continuity.
  // Mirrors the shape produced by the backend LayoutProjectionContinuityLiveDbEndToEndTests
  // after inserting a tensor row with full nodes[]: nodeId, nodeKind, componentId, x/y/w/h,
  // layoutClassRefs, parentNodeId, slotKey, orderIndex.
  const layoutNodes: LayoutNode[] = [
    // node-slot-b: orderIndex=0, first after sort
    {
      nodeId: "node-slot-b",
      nodeKind: "catalog_component",
      componentKey: "card",
      componentId: compAId,
      slotKey: "slot_b",
      orderIndex: 0,
      x: 10, y: 20, width: 300, height: 150,
    },
    // node-slot-a: orderIndex=1, second after sort
    {
      nodeId: "node-slot-a",
      nodeKind: "catalog_component",
      componentKey: "card",
      componentId: compBId,
      slotKey: "slot_a",
      orderIndex: 1,
      x: 50, y: 200, width: 200, height: 100,
      layoutClassRefs: ["card-ref"],
    },
  ];

  const emission: Emission = {
    structureMapId: "00000000-0000-0000-0000-000000000004",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    layoutId: "layout-dom-e2e",
    layoutNodes,
  };

  const specs = renderEmission(emission, twoCompRegistry);

  assertEquals(specs.length, 2);

  // slot_b: orderIndex=0 → specs[0]
  assertEquals(specs[0].nodeId, "node-slot-b");
  assertEquals(specs[0].componentId, compAId);
  assertEquals(specs[0].componentType, "default");
  assertEquals(specs[0].slotKey, "slot_b");
  assertEquals(specs[0].orderIndex, 0);
  assertEquals(specs[0].x, 10);
  assertEquals(specs[0].y, 20);
  assertEquals(specs[0].width, 300);
  assertEquals(specs[0].height, 150);

  // slot_a: orderIndex=1 → specs[1]
  assertEquals(specs[1].nodeId, "node-slot-a");
  assertEquals(specs[1].componentId, compBId);
  assertEquals(specs[1].componentType, "secondary");
  assertEquals(specs[1].slotKey, "slot_a");
  assertEquals(specs[1].orderIndex, 1);
  assertEquals(specs[1].x, 50);
  assertEquals(specs[1].y, 200);
  assertEquals(specs[1].layoutClassRefs?.[0], "card-ref");
});
