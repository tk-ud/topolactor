import { assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { buildCatalogComponentEventBinding } from "../runtime/renderEmission.ts";
import { ensureRuntimeComponentRegistryInitialized } from "../runtime/runtimeComponentRegistry.ts";
import { adaptComponentDataHub } from "../runtime/runtimeComponentAdapter.ts";

// ─── buildCatalogComponentEventBinding ────────────────────────────────────────

Deno.test("buildCatalogComponentEventBinding: null action returns empty object", () => {
  const binding = buildCatalogComponentEventBinding(null);
  assertEquals(Object.keys(binding).length, 0);
});

Deno.test("buildCatalogComponentEventBinding: Search action populates all standard triggers", () => {
  const binding = buildCatalogComponentEventBinding("Search");
  const triggers = ["click", "change", "select", "submit", "toggle"];
  for (const trigger of triggers) {
    assertExists(binding[trigger], `Expected trigger '${trigger}' to be present`);
    const val = binding[trigger] as Record<string, unknown>;
    assertEquals(val.eventType, trigger);
    const rd = val.runtimeDispatch as Record<string, unknown>;
    assertEquals(rd.action, "Search");
  }
});

Deno.test("buildCatalogComponentEventBinding: Create action sets correct action on runtimeDispatch", () => {
  const binding = buildCatalogComponentEventBinding("Create");
  const clickBinding = binding.click as Record<string, unknown>;
  const rd = clickBinding.runtimeDispatch as Record<string, unknown>;
  assertEquals(rd.action, "Create");
});

Deno.test("buildCatalogComponentEventBinding: diffUpdate action sets correct action", () => {
  const binding = buildCatalogComponentEventBinding("diffUpdate");
  const submitBinding = binding.submit as Record<string, unknown>;
  const rd = submitBinding.runtimeDispatch as Record<string, unknown>;
  assertEquals(rd.action, "diffUpdate");
});

// ─── adaptComponentDataHub with runtimeDispatch eventBinding ──────────────────

Deno.test("adaptComponentDataHub: catalog_component with runtimeDispatch eventBinding succeeds", () => {
  ensureRuntimeComponentRegistryInitialized();
  const eventBinding = buildCatalogComponentEventBinding("Search");
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

Deno.test("adaptComponentDataHub: eventBinding with runtimeDispatch is preserved in RuntimeComponentSpec", () => {
  ensureRuntimeComponentRegistryInitialized();
  const eventBinding = buildCatalogComponentEventBinding("Create");
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

Deno.test("renderEmission: catalog_component with componentKind produces runtimeSpec", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-001",
    layoutNodes: [
      {
        nodeId: "node-1",
        nodeKind: "catalog_component",
        componentId: "comp-001",
        componentKind: "action/button",
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

Deno.test("renderEmission: catalog_component without componentKind falls back to registry lookup", () => {
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
});

Deno.test("renderEmission: catalog_component with componentKind but unknown factory returns error runtimeSpec", () => {
  ensureRuntimeComponentRegistryInitialized();
  const emission: Emission = {
    layoutId: "layout-003",
    layoutNodes: [
      {
        nodeId: "node-3",
        nodeKind: "catalog_component",
        componentId: "comp-003",
        componentKind: "totally/unknown/kind",
        runtimeDispatchAction: "Search",
        orderIndex: 0,
      },
    ],
  };
  const specs = renderEmission(emission, emptyRegistry);
  assertEquals(specs.length, 1);
  // adaptComponentDataHub fails because the kind has no registered factory
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
