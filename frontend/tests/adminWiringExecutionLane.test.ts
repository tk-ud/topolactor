import { assertEquals, assertExists, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildCatalogComponentEventBinding,
  buildRouteNavigationEventBinding,
  buildRuntimeDispatchSpec,
  isNavigationWiringKind,
  mapWiringKindToAction,
  mapWiringKindToLayer,
} from "../runtime/renderEmission.ts";
import { ensureRuntimeComponentRegistryInitialized } from "../runtime/runtimeComponentRegistry.ts";
import { adaptComponentDataHub } from "../runtime/runtimeComponentAdapter.ts";
import type { RuntimeDispatchSpec } from "../runtime/frontendScheduler.ts";
import type { RuntimeComponentSpec } from "../runtime/runtimeComponentAdapter.ts";
import { __testOnly as factoryTestOnly } from "../runtime/runtimeComponentFactory.ts";

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

Deno.test("buildRuntimeDispatchSpec: absent targetSurface returns null (fail-close, no 'default' fallback)", () => {
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "search",
  });
  assertEquals(spec, null);
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

Deno.test("buildRuntimeDispatchSpec: null targetRef coerced to undefined when targetSurface present", () => {
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "search",
    targetSurface: "screen",
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

// ─── parseEventBinding: targetRef preserved through round-trip ───────────────

Deno.test("parseEventBinding: targetRef is preserved in parsed runtimeDispatch", () => {
  const manifestId = "aaaaaaaa-bbbb-cccc-dddd-000000000001";
  const rawBinding = {
    eventType: "click",
    runtimeDispatch: {
      operationType: "Search",
      target: "screen",
      layer: "screen_list",
      action: "Search",
      wiringKey: "search_key",
      wiringId: "wiring-001",
      targetRef: `manifest:${manifestId}:search_key`,
    },
  };
  const parsed = factoryTestOnly.parseEventBinding(rawBinding);
  assertExists(parsed, "parseEventBinding must return a value for valid binding");
  assertExists(parsed!.runtimeDispatch, "runtimeDispatch must be present");
  assertEquals(parsed!.runtimeDispatch!.targetRef, `manifest:${manifestId}:search_key`);
  assertEquals(parsed!.runtimeDispatch!.wiringKey, "search_key");
  assertEquals(parsed!.runtimeDispatch!.wiringId, "wiring-001");
  assertEquals(parsed!.runtimeDispatch!.target, "screen");
  assertEquals(parsed!.runtimeDispatch!.layer, "screen_list");
});

Deno.test("parseEventBinding: absent targetRef produces undefined in runtimeDispatch", () => {
  const rawBinding = {
    eventType: "click",
    runtimeDispatch: {
      operationType: "Search",
      target: "screen",
      layer: "screen_list",
      action: "Search",
      wiringKey: "search_key",
    },
  };
  const parsed = factoryTestOnly.parseEventBinding(rawBinding);
  assertExists(parsed);
  assertExists(parsed!.runtimeDispatch);
  assertEquals(parsed!.runtimeDispatch!.targetRef, undefined);
});

Deno.test("parseEventBinding: non-string targetRef is coerced to undefined", () => {
  const rawBinding = {
    eventType: "click",
    runtimeDispatch: {
      operationType: "Search",
      target: "screen",
      layer: "screen_list",
      action: "Search",
      targetRef: 42,
    },
  };
  const parsed = factoryTestOnly.parseEventBinding(rawBinding);
  assertExists(parsed);
  assertExists(parsed!.runtimeDispatch);
  assertEquals(parsed!.runtimeDispatch!.targetRef, undefined);
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

// ─── isNavigationWiringKind ───────────────────────────────────────────────────

Deno.test("isNavigationWiringKind: navigation returns true", () => {
  assertEquals(isNavigationWiringKind("navigation"), true);
});

Deno.test("isNavigationWiringKind: search/create/update/delete/empty return false", () => {
  assertEquals(isNavigationWiringKind("search"), false);
  assertEquals(isNavigationWiringKind("create"), false);
  assertEquals(isNavigationWiringKind("update"), false);
  assertEquals(isNavigationWiringKind("delete"), false);
  assertEquals(isNavigationWiringKind(""), false);
});

// ─── buildRuntimeDispatchSpec: navigation guard ────────────────────────────────

Deno.test("buildRuntimeDispatchSpec: navigation wiringKind returns null (frontend-local lane)", () => {
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "navigation",
    targetSurface: "route",
    targetRef: "route:/admin/manifests",
  });
  assertEquals(spec, null, "navigation wiringKind must not produce a backend dispatch spec");
});

Deno.test("buildRuntimeDispatchSpec: route: targetRef never appears in dispatch spec for navigation wiring", () => {
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "navigation",
    targetSurface: "route",
    targetRef: "route:/admin",
  });
  assertEquals(spec, null, "no dispatch spec means route: prefix cannot reach ManifestDispatcher");
});

// ─── buildRouteNavigationEventBinding ─────────────────────────────────────────

Deno.test("buildRouteNavigationEventBinding: valid route: targetRef builds routeNavigation binding", () => {
  const binding = buildRouteNavigationEventBinding("route:/admin/manifests");
  const triggers = ["click", "change", "select", "submit", "toggle"];
  for (const trigger of triggers) {
    assertExists(binding[trigger], `trigger '${trigger}' must be present`);
    const val = binding[trigger] as Record<string, unknown>;
    assertEquals(val.eventType, trigger);
    const rn = val.routeNavigation as Record<string, unknown>;
    assertExists(rn, "routeNavigation must be present");
    assertEquals(rn.targetRef, "route:/admin/manifests");
    assertEquals(val.runtimeDispatch, undefined, "runtimeDispatch must NOT be present on navigation binding");
  }
});

Deno.test("buildRouteNavigationEventBinding: manifest: targetRef returns empty binding", () => {
  const binding = buildRouteNavigationEventBinding("manifest:aaaaaaaa-bbbb-cccc-dddd-000000000001:key");
  assertEquals(Object.keys(binding).length, 0);
});

Deno.test("buildRouteNavigationEventBinding: null/undefined/empty targetRef returns empty binding", () => {
  assertEquals(Object.keys(buildRouteNavigationEventBinding(null)).length, 0);
  assertEquals(Object.keys(buildRouteNavigationEventBinding(undefined)).length, 0);
  assertEquals(Object.keys(buildRouteNavigationEventBinding("")).length, 0);
});

// ─── parseEventBinding: routeNavigation ──────────────────────────────────────

Deno.test("parseEventBinding: routeNavigation binding is parsed correctly", () => {
  const rawBinding = {
    eventType: "click",
    routeNavigation: { targetRef: "route:/admin/manifests" },
  };
  const parsed = factoryTestOnly.parseEventBinding(rawBinding);
  assertExists(parsed, "parseEventBinding must return a value");
  assertExists(parsed!.routeNavigation, "routeNavigation must be parsed");
  assertEquals(parsed!.routeNavigation!.targetRef, "route:/admin/manifests");
  assertEquals(parsed!.runtimeDispatch, undefined, "runtimeDispatch must be absent on navigation binding");
});

Deno.test("parseEventBinding: non-route: routeNavigation targetRef is rejected (undefined)", () => {
  const rawBinding = {
    eventType: "click",
    routeNavigation: { targetRef: "screen:some-key" },
  };
  const parsed = factoryTestOnly.parseEventBinding(rawBinding);
  assertExists(parsed, "parseEventBinding must return non-null for valid eventType");
  assertEquals(parsed!.routeNavigation, undefined, "routeNavigation must be rejected for non-route: prefix");
});


Deno.test("emitBoundEvent: routeNavigation click executes frontend-local navigation", () => {
  const originalLocation = globalThis.location;
  const testLocation = { href: "http://localhost/admin/ui-builder" } as Location;
  Object.defineProperty(globalThis, "location", {
    configurable: true,
    value: testLocation,
  });
  try {
    const spec: RuntimeComponentSpec = {
      componentId: "comp-nav-emit-001",
      packageId: null,
      layoutId: "layout-nav-emit-001",
      wiringId: "wiring-nav-emit-001",
      componentType: "action/button",
      props: { data: { label: "Go to manifests" } },
      eventBinding: {
        click: {
          eventType: "click",
          routeNavigation: { targetRef: "route:/admin/manifests" },
        },
      },
    };

    const result = factoryTestOnly.emitBoundEvent(spec, "click", {});

    assertEquals(result, { ok: true });
    assertEquals(globalThis.location.href, "/admin/manifests");
  } finally {
    Object.defineProperty(globalThis, "location", {
      configurable: true,
      value: originalLocation,
    });
  }
});

// ─── manifest wiring regression: unchanged by navigation changes ───────────────

Deno.test("buildRuntimeDispatchSpec: manifest wiring (search) still produces backend dispatch spec", () => {
  const manifestId = "aaaaaaaa-bbbb-cccc-dddd-000000000001";
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "search",
    targetSurface: "screen",
    targetRef: `manifest:${manifestId}:search_key`,
    wiringKey: "search_key",
    wiringId: "wiring-001",
  });
  assertExists(spec);
  assertEquals(spec!.layer, "screen_list");
  assertEquals(spec!.targetRef, `manifest:${manifestId}:search_key`);
  assertEquals(spec!.action, "Search");
});

// ─── renderEmission: navigation wiring uses routeNavigation binding ───────────

Deno.test("renderEmission: catalog_component with navigation wiringKind uses routeNavigation binding not runtimeDispatch", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-nav-001",
    layoutNodes: [
      {
        nodeId: "node-nav-1",
        nodeKind: "catalog_component",
        componentId: "comp-nav-001",
        componentKind: "action/button",
        componentKey: "GoToManifests",
        wiringKind: "navigation",
        targetSurface: "route",
        targetRef: "route:/admin/manifests",
        orderIndex: 0,
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry);
  assertEquals(specs.length, 1);
  assertExists(specs[0].runtimeSpec, "runtimeSpec must be present");
  const clickBinding = specs[0].runtimeSpec!.eventBinding["click"] as Record<string, unknown>;
  assertExists(clickBinding, "click binding must be present");
  const rn = clickBinding.routeNavigation as Record<string, unknown>;
  assertExists(rn, "routeNavigation must be present for navigation wiring");
  assertEquals(rn.targetRef, "route:/admin/manifests");
  assertEquals(clickBinding.runtimeDispatch, undefined, "runtimeDispatch must NOT be present for navigation wiring");
});

// ─── mergeNodeLocalProps / propsJson / stateJson pipeline ────────────────────

import { mergeNodeLocalProps } from "../runtime/renderEmission.ts";

Deno.test("mergeNodeLocalProps: no overrides returns base props unchanged", () => {
  const base = { data: { label: "Base" } };
  const result = mergeNodeLocalProps(base, undefined, undefined);
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.props, { data: { label: "Base" } });
});

Deno.test("mergeNodeLocalProps: valid propsJson is shallow-merged over base", () => {
  const base = { data: { label: "Default" } };
  const result = mergeNodeLocalProps(base, '{"data": {"label": "Custom", "variant": "primary"}}', undefined);
  assertEquals(result.ok, true);
  if (!result.ok) return;
  const data = result.props.data as Record<string, unknown>;
  assertEquals(data.label, "Custom");
  assertEquals(data.variant, "primary");
});

Deno.test("mergeNodeLocalProps: valid stateJson is merged into props.data", () => {
  const base = { data: { label: "Modal", title: "Test" } };
  const result = mergeNodeLocalProps(base, undefined, '{"open": true}');
  assertEquals(result.ok, true);
  if (!result.ok) return;
  const data = result.props.data as Record<string, unknown>;
  assertEquals(data.open, true);
  assertEquals(data.label, "Modal");
});

Deno.test("mergeNodeLocalProps: invalid propsJson returns explicit LAYOUT_NODE_PROPS_JSON_INVALID error", () => {
  const base = { data: { label: "Default" } };
  const result = mergeNodeLocalProps(base, "{not valid json}", undefined);
  assertEquals(result.ok, false);
  if (result.ok) return;
  assertStringIncludes(result.error, "LAYOUT_NODE_PROPS_JSON_INVALID");
});

Deno.test("mergeNodeLocalProps: invalid stateJson returns explicit LAYOUT_NODE_STATE_JSON_INVALID error", () => {
  const base = { data: { label: "Default" } };
  const result = mergeNodeLocalProps(base, undefined, "{bad json}");
  assertEquals(result.ok, false);
  if (result.ok) return;
  assertStringIncludes(result.error, "LAYOUT_NODE_STATE_JSON_INVALID");
});

Deno.test("mergeNodeLocalProps: propsJson that is not an object returns explicit error", () => {
  const base = { data: { label: "Default" } };
  const result = mergeNodeLocalProps(base, '"just a string"', undefined);
  assertEquals(result.ok, false);
  if (result.ok) return;
  assertStringIncludes(result.error, "LAYOUT_NODE_PROPS_JSON_INVALID");
});

Deno.test("renderEmission: node with propsJson overrides default label in runtimeSpec", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-props-001",
    layoutNodes: [
      {
        nodeId: "node-props-1",
        nodeKind: "catalog_component",
        componentId: "comp-props-001",
        componentKind: "action/button",
        componentKey: "btn.primitive",
        wiringKind: "search",
        targetSurface: "screen",
        orderIndex: 0,
        propsJson: '{"data": {"label": "カスタムボタン", "variant": "primary"}}',
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry);
  assertEquals(specs.length, 1);
  assertExists(specs[0].runtimeSpec, "runtimeSpec must be present");
  const props = specs[0].runtimeSpec!.props as Record<string, unknown>;
  const data = props.data as Record<string, unknown>;
  assertEquals(data.label, "カスタムボタン");
  assertEquals(data.variant, "primary");
});

Deno.test("renderEmission: node with stateJson merges open state into modal props", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-state-001",
    layoutNodes: [
      {
        nodeId: "node-state-1",
        nodeKind: "catalog_component",
        componentId: "comp-state-001",
        componentKind: "disclosure/modal",
        componentKey: "modal.template",
        orderIndex: 0,
        stateJson: '{"open": true}',
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry);
  assertEquals(specs.length, 1);
  assertExists(specs[0].runtimeSpec, "runtimeSpec must be present");
  const props = specs[0].runtimeSpec!.props as Record<string, unknown>;
  const data = props.data as Record<string, unknown>;
  assertEquals(data.open, true);
});

Deno.test("renderEmission: node with invalid propsJson returns error spec not silent fallback", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-invalid-props-001",
    layoutNodes: [
      {
        nodeId: "node-invalid-1",
        nodeKind: "catalog_component",
        componentId: "comp-invalid-001",
        componentKind: "action/button",
        componentKey: "btn.primitive",
        orderIndex: 0,
        propsJson: '{invalid json}',
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry);
  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "error");
  assertStringIncludes(JSON.stringify(specs[0].def), "LAYOUT_NODE_PROPS_JSON_INVALID");
});

Deno.test("renderEmission: node with invalid stateJson returns error spec not silent fallback", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-invalid-state-001",
    layoutNodes: [
      {
        nodeId: "node-invalid-2",
        nodeKind: "catalog_component",
        componentId: "comp-invalid-002",
        componentKind: "disclosure/modal",
        componentKey: "modal.template",
        orderIndex: 0,
        stateJson: 'not-json-at-all',
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry);
  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "error");
  assertStringIncludes(JSON.stringify(specs[0].def), "LAYOUT_NODE_STATE_JSON_INVALID");
});

// ─── buildRuntimeDispatchSpec: fail-close on absent targetSurface ─────────────

Deno.test("buildRuntimeDispatchSpec: wiringKind set + empty targetSurface returns null (fail-close)", () => {
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "search",
    targetSurface: "",
  });
  assertEquals(spec, null, "empty targetSurface must not silently fall back to 'default'");
});

Deno.test("buildRuntimeDispatchSpec: wiringKind set + whitespace targetSurface returns null", () => {
  const spec = buildRuntimeDispatchSpec({
    orderIndex: 0,
    wiringKind: "create",
    targetSurface: "   ",
  });
  assertEquals(spec, null, "whitespace targetSurface must not silently fall back to 'default'");
});

// ─── runtimeInteractions: input trigger + setActiveKey actionType ─────────────

Deno.test("renderEmission: runtimeInteractions with input trigger and setActiveKey builds localStateMutation", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-input-setactivekey-001",
    layoutNodes: [
      {
        nodeId: "node-tabs",
        nodeKind: "catalog_component",
        componentId: "comp-tabs-001",
        componentKind: "disclosure/tabs",
        componentKey: "tabs.main",
        orderIndex: 0,
        runtimeInteractions: [
          {
            trigger: "input",
            actionType: "setActiveKey",
            targetNodeId: "tabs_content_node",
            value: "tab_1",
          },
        ],
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry, { localStateStore: { get: () => undefined, set: () => {} } });
  assertEquals(specs.length, 1);
  assertExists(specs[0].runtimeSpec, "runtimeSpec must be present");
  const inputBinding = specs[0].runtimeSpec!.eventBinding["input"] as Record<string, unknown> | undefined;
  assertExists(inputBinding, "input trigger binding must be present");
  const mutation = inputBinding.localStateMutation as Record<string, unknown> | undefined;
  assertExists(mutation, "localStateMutation must be present for setActiveKey");
  assertEquals(mutation.targetNodeId, "tabs_content_node");
  assertEquals(mutation.statePath, "activeKey");
  assertEquals(mutation.action, "set");
  assertEquals(mutation.value, "tab_1");
});

Deno.test("renderEmission: runtimeInteractions with onInput trigger normalizes to input", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-oninput-001",
    layoutNodes: [
      {
        nodeId: "node-onInput",
        nodeKind: "catalog_component",
        componentId: "comp-oninput-001",
        componentKind: "disclosure/tabs",
        componentKey: "tabs.main",
        orderIndex: 0,
        runtimeInteractions: [
          {
            trigger: "onInput",
            actionType: "setActiveKey",
            targetNodeId: "tabs_content_node",
            value: "tab_2",
          },
        ],
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry, { localStateStore: { get: () => undefined, set: () => {} } });
  assertEquals(specs.length, 1);
  assertExists(specs[0].runtimeSpec);
  const inputBinding = specs[0].runtimeSpec!.eventBinding["input"] as Record<string, unknown> | undefined;
  assertExists(inputBinding, "onInput must normalize to input binding key");
  const mutation = inputBinding.localStateMutation as Record<string, unknown>;
  assertEquals(mutation.statePath, "activeKey");
});
