import { assertEquals, assertExists, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildCatalogComponentEventBinding,
  buildRuntimeDispatchSpec,
  mapWiringKindToAction,
  mapWiringKindToLayer,
} from "../runtime/renderEmission.ts";
import { ensureRuntimeComponentRegistryInitialized } from "../runtime/runtimeComponentRegistry.ts";
import { adaptComponentDataHub } from "../runtime/runtimeComponentAdapter.ts";
import type { RuntimeDispatchSpec } from "../runtime/frontendScheduler.ts";

// ─── mapWiringKindToLayer ─────────────────────────────────────────────────────

Deno.test("mapWiringKindToLayer: search → screen_list", () => {
  assertEquals(mapWiringKindToLayer("search"), "screen_list");
});

Deno.test("mapWiringKindToLayer: aggregate → screen_aggregation", () => {
  assertEquals(mapWiringKindToLayer("aggregate"), "screen_aggregation");
});

Deno.test("mapWiringKindToLayer: create/update/delete → entity", () => {
  assertEquals(mapWiringKindToLayer("create"), "entity");
  assertEquals(mapWiringKindToLayer("update"), "entity");
  assertEquals(mapWiringKindToLayer("delete"), "entity");
});

// ─── mapWiringKindToAction ────────────────────────────────────────────────────

Deno.test("mapWiringKindToAction: search/aggregate → Search", () => {
  assertEquals(mapWiringKindToAction("search"), "Search");
  assertEquals(mapWiringKindToAction("aggregate"), "Search");
});

Deno.test("mapWiringKindToAction: create/update/delete map correctly", () => {
  assertEquals(mapWiringKindToAction("create"), "Create");
  assertEquals(mapWiringKindToAction("update"), "diffUpdate");
  assertEquals(mapWiringKindToAction("delete"), "logicalDelete");
});

// ─── buildRuntimeDispatchSpec ─────────────────────────────────────────────────

Deno.test("buildRuntimeDispatchSpec: null when wiringKind absent", () => {
  const spec = buildRuntimeDispatchSpec({ orderIndex: 0 });
  assertEquals(spec, null);
});

Deno.test("buildRuntimeDispatchSpec: search wiring builds screen_list spec", () => {
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "search",
    targetSurface: "screen",
    targetRef: "manifest-001",
    wiringKey: "search_key",
    wiringId: "wiring-001",
  });
  assertExists(spec);
  assertEquals(spec!.operationType, "Search");
  assertEquals(spec!.target, "screen");
  assertEquals(spec!.layer, "screen_list");
  assertEquals(spec!.action, "Search");
  assertEquals(spec!.wiringKey, "search_key");
  assertEquals(spec!.wiringId, "wiring-001");
});

Deno.test("buildRuntimeDispatchSpec: aggregate wiring builds screen_aggregation spec", () => {
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "aggregate",
    targetSurface: "screen",
  });
  assertExists(spec);
  assertEquals(spec!.layer, "screen_aggregation");
  assertEquals(spec!.action, "Search");
});

Deno.test("buildRuntimeDispatchSpec: create wiring defaults to entity layer", () => {
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "create",
    targetSurface: "default",
  });
  assertExists(spec);
  assertEquals(spec!.layer, "entity");
  assertEquals(spec!.action, "Create");
  assertEquals(spec!.target, "default");
});

Deno.test("buildRuntimeDispatchSpec: absent targetSurface defaults to 'default'", () => {
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "search",
  });
  assertExists(spec);
  assertEquals(spec!.target, "default");
  assertEquals(spec!.layer, "screen_list");
});

Deno.test("buildRuntimeDispatchSpec: targetRef forwarded when present", () => {
  const manifestId = "aaaaaaaa-bbbb-cccc-dddd-000000000001";
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "search",
    targetSurface: "screen",
    wiringKey: "search_key",
    wiringId: "wiring-001",
    targetRef: `manifest:${manifestId}:search_key`,
  });
  assertExists(spec);
  assertEquals(spec!.targetRef, `manifest:${manifestId}:search_key`);
  assertEquals(spec!.wiringKey, "search_key");
  assertEquals(spec!.wiringId, "wiring-001");
});

Deno.test("buildRuntimeDispatchSpec: targetRef absent when node has no targetRef", () => {
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "search",
    targetSurface: "screen",
    wiringKey: "search_key",
    wiringId: "wiring-002",
  });
  assertExists(spec);
  assertEquals(spec!.targetRef, undefined);
});

Deno.test("buildRuntimeDispatchSpec: null targetRef coerced to undefined", () => {
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "search",
    targetRef: null,
  });
  assertExists(spec);
  assertEquals(spec!.targetRef, undefined);
});

// ─── buildCatalogComponentEventBinding ────────────────────────────────────────

Deno.test("buildCatalogComponentEventBinding: null spec returns empty object", () => {
  const binding = buildCatalogComponentEventBinding(null);
  assertEquals(Object.keys(binding).length, 0);
});

Deno.test("buildCatalogComponentEventBinding: Search spec populates all standard triggers with full spec", () => {
  const spec: RuntimeDispatchSpec = {
    operationType: "Search",
    target: "screen",
    layer: "screen_list",
    action: "Search",
  };
  const binding = buildCatalogComponentEventBinding(spec);
  const triggers = ["click", "change", "select", "submit", "toggle"];
  for (const trigger of triggers) {
    assertExists(binding[trigger], `Expected trigger '${trigger}' to be present`);
    const val = binding[trigger] as Record<string, unknown>;
    assertEquals(val.eventType, trigger);
    const rd = val.runtimeDispatch as Record<string, unknown>;
    assertEquals(rd.action, "Search");
    assertEquals(rd.target, "screen");
    assertEquals(rd.layer, "screen_list");
    assertEquals(rd.operationType, "Search");
  }
});

Deno.test("buildCatalogComponentEventBinding: Create spec sets correct dispatch fields", () => {
  const spec: RuntimeDispatchSpec = {
    operationType: "Create",
    target: "default",
    layer: "entity",
    action: "Create",
  };
  const binding = buildCatalogComponentEventBinding(spec);
  const clickBinding = binding.click as Record<string, unknown>;
  const rd = clickBinding.runtimeDispatch as Record<string, unknown>;
  assertEquals(rd.action, "Create");
  assertEquals(rd.target, "default");
  assertEquals(rd.layer, "entity");
});

Deno.test("buildCatalogComponentEventBinding: spec with targetRef carries it to runtimeDispatch", () => {
  const manifestId = "aaaaaaaa-bbbb-cccc-dddd-000000000001";
  const spec: RuntimeDispatchSpec = {
    operationType: "Search",
    target: "screen",
    layer: "screen_list",
    action: "Search",
    wiringKey: "search_key",
    wiringId: "wiring-001",
    targetRef: `manifest:${manifestId}:search_key`,
  };
  const binding = buildCatalogComponentEventBinding(spec);
  const clickBinding = binding.click as Record<string, unknown>;
  const rd = clickBinding.runtimeDispatch as Record<string, unknown>;
  assertEquals(rd.targetRef, `manifest:${manifestId}:search_key`);
  assertEquals(rd.wiringKey, "search_key");
  assertEquals(rd.wiringId, "wiring-001");
});

// ─── adaptComponentDataHub with full runtimeDispatch spec ────────────────────

Deno.test("adaptComponentDataHub: catalog_component with full dispatch spec in eventBinding succeeds", () => {
  ensureRuntimeComponentRegistryInitialized();
  const spec: RuntimeDispatchSpec = {
    operationType: "Search",
    target: "screen",
    layer: "screen_list",
    action: "Search",
  };
  const eventBinding = buildCatalogComponentEventBinding(spec);
  const result = adaptComponentDataHub({
    componentId: "test-component-id-001",
    componentKind: "action/button",
    packageId: null,
    layoutId: null,
    wiringId: null,
    props: { data: { label: "Search" } },
    eventBinding,
  });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.value.componentType, "action/button");
  assertExists(result.value.eventBinding["click"]);
});

Deno.test("adaptComponentDataHub: eventBinding with full runtimeDispatch is preserved in RuntimeComponentSpec", () => {
  ensureRuntimeComponentRegistryInitialized();
  const spec: RuntimeDispatchSpec = {
    operationType: "Create",
    target: "default",
    layer: "entity",
    action: "Create",
    wiringKey: "create_wiring",
  };
  const eventBinding = buildCatalogComponentEventBinding(spec);
  const result = adaptComponentDataHub({
    componentId: "test-component-id-002",
    componentKind: "action/button",
    packageId: null,
    layoutId: null,
    wiringId: null,
    props: { data: { label: "Create" } },
    eventBinding,
  });
  if (!result.ok) throw new Error(`adaptComponentDataHub failed: ${result.error}`);
  const clickBinding = result.value.eventBinding["click"] as Record<string, unknown>;
  assertExists(clickBinding);
  const rd = clickBinding.runtimeDispatch as Record<string, unknown>;
  assertExists(rd);
  assertEquals(rd.action, "Create");
  assertEquals(rd.target, "default");
  assertEquals(rd.layer, "entity");
  assertEquals(rd.wiringKey, "create_wiring");
});

Deno.test("adaptComponentDataHub: missing componentId returns explicit error", () => {
  const result = adaptComponentDataHub({
    componentId: "",
    componentKind: "action/button",
    packageId: null,
    layoutId: null,
    wiringId: null,
    props: {},
    eventBinding: {},
  });
  assertEquals(result.ok, false);
  if (result.ok) return;
  assertEquals(result.error, "RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_ID");
});

Deno.test("adaptComponentDataHub: missing componentKind returns explicit error", () => {
  const result = adaptComponentDataHub({
    componentId: "test-id",
    componentKind: "",
    packageId: null,
    layoutId: null,
    wiringId: null,
    props: {},
    eventBinding: {},
  });
  assertEquals(result.ok, false);
  if (result.ok) return;
  assertEquals(result.error, "RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_KIND");
});

// ─── renderEmission: catalog_component path ───────────────────────────────────

import { renderEmission } from "../runtime/renderEmission.ts";
import type { Emission } from "../api/dispatch.ts";

const emptyRegistry = {};

Deno.test("renderEmission: catalog_component with componentKind and wiringKind produces runtimeSpec", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-001",
    layoutNodes: [
      {
        nodeId: "node-1",
        nodeKind: "catalog_component",
        componentId: "comp-001",
        componentKind: "action/button",
        componentKey: "Search",
        wiringKind: "search",
        targetSurface: "screen",
        wiringKey: "search_key",
        wiringId: "wiring-001",
        runtimeDispatchAction: "Search",
        orderIndex: 0,
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry);
  assertEquals(specs.length, 1);
  const spec = specs[0];
  assertEquals(spec.componentType, "action/button");
  assertExists(spec.runtimeSpec, "runtimeSpec must be present when componentKind is known");
  assertEquals(spec.componentId, "comp-001");
});

Deno.test("renderEmission: catalog_component without componentKind returns CATALOG_COMPONENT_KIND_REQUIRED error", () => {
  const emission: Emission = {
    layoutId: "layout-002",
    layoutNodes: [
      {
        nodeId: "node-2",
        nodeKind: "catalog_component",
        componentId: "unknown-comp-id",
        orderIndex: 0,
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry);
  assertEquals(specs.length, 1);
  const spec = specs[0];
  assertEquals(spec.componentType, "error");
  assertEquals(spec.runtimeSpec, undefined);
  assertStringIncludes(
    JSON.stringify(spec.def),
    "CATALOG_COMPONENT_KIND_REQUIRED",
    "Error must carry CATALOG_COMPONENT_KIND_REQUIRED code",
  );
});

Deno.test("renderEmission: catalog_component with componentKind but unknown factory returns error spec", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-003",
    layoutNodes: [
      {
        nodeId: "node-3",
        nodeKind: "catalog_component",
        componentId: "comp-003",
        componentKind: "totally/unknown/kind",
        componentKey: "Test",
        wiringKind: "search",
        targetSurface: "screen",
        runtimeDispatchAction: "Search",
        orderIndex: 0,
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry);
  assertEquals(specs.length, 1);
  const spec = specs[0];
  assertEquals(spec.componentType, "error");
});

Deno.test("renderEmission: structural_html node is unaffected by catalog_component changes", () => {
  const emission: Emission = {
    layoutId: "layout-004",
    layoutNodes: [
      {
        nodeId: "node-4",
        nodeKind: "structural_html",
        htmlTag: "div",
        orderIndex: 0,
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry);
  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "structural_html");
  assertEquals(specs[0].runtimeSpec, undefined);
});

Deno.test("renderEmission: catalog_component dispatch spec uses screen_list layer for search wiring", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-005",
    layoutNodes: [
      {
        nodeId: "node-5",
        nodeKind: "catalog_component",
        componentId: "comp-005",
        componentKind: "action/button",
        componentKey: "Search",
        wiringKind: "search",
        targetSurface: "screen",
        wiringId: "wiring-005",
        wiringKey: "search_005",
        orderIndex: 0,
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry);
  assertEquals(specs.length, 1);
  assertExists(specs[0].runtimeSpec);
  const clickBinding = specs[0].runtimeSpec!.eventBinding["click"] as Record<string, unknown>;
  assertExists(clickBinding);
  const rd = clickBinding.runtimeDispatch as Record<string, unknown>;
  assertExists(rd);
  assertEquals(rd.layer, "screen_list");
  assertEquals(rd.target, "screen");
  assertEquals(rd.action, "Search");
});
