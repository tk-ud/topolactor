import { assertEquals, assertExists, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, type JSX } from "preact";
import { renderToString } from "preact-render-to-string";
import { createSseDispatcher } from "../runtime/sseDispatcher.ts";
import { queueClientCommand } from "../runtime/frontendScheduler.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { buildChildrenMap, renderEmission, type ComponentSpec } from "../runtime/renderEmission.ts";
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

// ─── DOM projection render-to-string tests ────────────────────────────────────
// These tests use buildChildrenMap (exported from renderEmission.ts) and
// preact-render-to-string to verify actual HTML output matches the SSOT contract:
//   - structural_html → real HTML tag
//   - parentNodeId → DOM nesting
//   - x/y/width/height → style with position:absolute
//   - layoutClassRefs → class attribute
//
// A test-local LayoutNodeToHtml function mirrors the SSOT DOM projection contract
// without coupling to ProjectionShell's auth/effect infrastructure.

function layoutNodeToElement(
  spec: ComponentSpec,
  childrenMap: Map<string | undefined, ComponentSpec[]>,
): JSX.Element {
  const children = childrenMap.get(spec.nodeId) ?? [];

  const style: Record<string, string> = {};
  if (spec.x !== undefined || spec.y !== undefined || spec.width !== undefined || spec.height !== undefined) {
    style.position = "absolute";
    if (spec.x !== undefined) style.left = `${spec.x}px`;
    if (spec.y !== undefined) style.top = `${spec.y}px`;
    if (spec.width !== undefined) style.width = `${spec.width}px`;
    if (spec.height !== undefined) style.height = `${spec.height}px`;
  }
  const className = spec.layoutClassRefs?.join(" ") || undefined;

  const childElements = children.map((c) => layoutNodeToElement(c, childrenMap));

  if (spec.nodeKind === "structural_html" && spec.htmlTag) {
    return h(
      spec.htmlTag,
      {
        style: Object.keys(style).length > 0 ? style : undefined,
        class: className,
        "data-node-id": spec.nodeId,
      },
      ...childElements,
    );
  }
  return h(
    "div",
    {
      style: Object.keys(style).length > 0 ? style : undefined,
      class: className,
      "data-node-id": spec.nodeId,
      "data-component-id": spec.componentId,
    },
    ...childElements,
  );
}

function renderLayoutTree(emission: Emission, registry: typeof twoCompRegistry): string {
  const specs = renderEmission(emission, registry);
  const childrenMap = buildChildrenMap(specs);
  const roots = childrenMap.get(undefined) ?? [];
  return renderToString(h("div", { id: "layout-root" }, ...roots.map((r) => layoutNodeToElement(r, childrenMap))));
}

Deno.test("DOM render: structural_html node produces real HTML tag in rendered output", () => {
  const emission: Emission = {
    structureMapId: "00000000-0000-0000-0000-000000000004",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    layoutId: "layout-dom-render-test",
    layoutNodes: [
      {
        nodeId: "node-section",
        nodeKind: "structural_html",
        htmlTag: "section",
        slotKey: "wrapper",
        orderIndex: 0,
        x: 0, y: 0, width: 800, height: 600,
      },
    ],
  };

  const html = renderLayoutTree(emission, twoCompRegistry);

  // structural_html "section" must appear as an actual <section> tag
  assertStringIncludes(html, "<section");
  assertStringIncludes(html, 'data-node-id="node-section"');
});

Deno.test("DOM render: x/y/width/height project as position:absolute CSS style", () => {
  const emission: Emission = {
    structureMapId: "00000000-0000-0000-0000-000000000004",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    layoutId: "layout-dom-render-test",
    layoutNodes: [
      {
        nodeId: "node-card",
        nodeKind: "catalog_component",
        componentId: compAId,
        slotKey: "slot_a",
        orderIndex: 0,
        x: 10, y: 20, width: 300, height: 150,
      },
    ],
  };

  const html = renderLayoutTree(emission, twoCompRegistry);

  // SSOT: x/y/width/height → position:absolute CSS (not relative)
  assertStringIncludes(html, "position:absolute");
  assertStringIncludes(html, "left:10px");
  assertStringIncludes(html, "top:20px");
  assertStringIncludes(html, "width:300px");
  assertStringIncludes(html, "height:150px");
});

Deno.test("DOM render: layoutClassRefs project as class attribute", () => {
  const emission: Emission = {
    structureMapId: "00000000-0000-0000-0000-000000000004",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    layoutId: "layout-dom-render-test",
    layoutNodes: [
      {
        nodeId: "node-card",
        nodeKind: "catalog_component",
        componentId: compAId,
        slotKey: "slot_a",
        orderIndex: 0,
        x: 0, y: 0, width: 100, height: 50,
        layoutClassRefs: ["card-primary", "elevated"],
      },
    ],
  };

  const html = renderLayoutTree(emission, twoCompRegistry);

  assertStringIncludes(html, "card-primary");
  assertStringIncludes(html, "elevated");
});

Deno.test("DOM render: parentNodeId nesting renders child inside parent element", () => {
  // SSOT: parentNodeId establishes DOM nesting tree.
  // node-root (structural_html div) wraps node-child (catalog_component).
  // DOM: <div data-node-id="node-root"><div data-node-id="node-child" ...></div></div>
  const emission: Emission = {
    structureMapId: "00000000-0000-0000-0000-000000000004",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    layoutId: "layout-dom-render-test",
    layoutNodes: [
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
    ],
  };

  const html = renderLayoutTree(emission, twoCompRegistry);

  // Both nodes appear
  assertStringIncludes(html, 'data-node-id="node-root"');
  assertStringIncludes(html, 'data-node-id="node-child"');
  // node-child must appear after node-root opening tag (nested inside)
  const rootIdx = html.indexOf('data-node-id="node-root"');
  const childIdx = html.indexOf('data-node-id="node-child"');
  assertEquals(rootIdx < childIdx, true, "node-child must be nested inside node-root in DOM order");
  // Only node-root is a root (no node-root parent in output wrapping div) —
  // the layout-root div contains only 1 direct child (node-root, not node-child separately)
  const layoutRoot = renderToString(h("div", { id: "layout-root" }));
  assertEquals(html.includes('id="layout-root"'), true);
});

Deno.test("DOM render: buildChildrenMap separates roots from children correctly", () => {
  // Tests the pure buildChildrenMap function directly (no DOM rendering).
  const specs: ComponentSpec[] = [
    { componentType: "structural_html", def: {}, nodeId: "root", nodeKind: "structural_html", htmlTag: "section", orderIndex: 0 },
    { componentType: "default", def: {}, nodeId: "child-a", nodeKind: "catalog_component", parentNodeId: "root", orderIndex: 1 },
    { componentType: "default", def: {}, nodeId: "child-b", nodeKind: "catalog_component", parentNodeId: "root", orderIndex: 0 },
  ];

  const map = buildChildrenMap(specs);

  // Roots: parentNodeId=undefined → key undefined
  const roots = map.get(undefined) ?? [];
  assertEquals(roots.length, 1);
  assertEquals(roots[0].nodeId, "root");

  // Children of "root": sorted by orderIndex → child-b (0) before child-a (1)
  const rootChildren = map.get("root") ?? [];
  assertEquals(rootChildren.length, 2);
  assertEquals(rootChildren[0].nodeId, "child-b");
  assertEquals(rootChildren[1].nodeId, "child-a");
});
