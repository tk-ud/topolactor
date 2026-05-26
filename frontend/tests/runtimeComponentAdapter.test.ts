import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  constructProjection,
  type ProjectionDefinition,
} from "../runtime/projectionConstructor.ts";
import { adaptComponentDataHub } from "../runtime/runtimeComponentAdapter.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";

Deno.test("runtimeComponentAdapter: missing componentId is explicit error", () => {
  const result = adaptComponentDataHub({
    componentKind: "action/button",
    props: { label: "x" },
    eventBinding: {},
  });
  assertEquals(result, {
    ok: false,
    error: "RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_ID",
  });
});

Deno.test("runtimeComponentAdapter: unsupported kind is explicit error", () => {
  const result = adaptComponentDataHub({
    componentId: "c1",
    componentKind: "x/unknown",
    props: {},
    eventBinding: {},
  });
  assertEquals(result, {
    ok: false,
    error: "RUNTIME_COMPONENT_ADAPTER_UNSUPPORTED_COMPONENT_KIND: x/unknown",
  });
});

Deno.test("runtimeComponentAdapter: projection constructor hub converts to button runtime spec", () => {
  const def: ProjectionDefinition = {
    constructorKey: "k",
    packageIds: ["p"],
    outputKind: "component_projection",
    componentId: "cmp-1",
    componentDefinition: {
      componentId: "cmp-1",
      packageId: "pkg-1",
      layoutId: "lay-1",
      wiringId: "wire-1",
      component_kind: "action/button",
      default_parameters: { label: "Run" },
      event_binding: {
        click: { eventType: "click", actorOrSource: "ui_test" },
      },
    },
  };
  const projection = constructProjection({}, def);
  if (
    !projection.projection ||
    projection.projection.kind !== "component_projection"
  ) throw new Error("unexpected");
  const result = adaptComponentDataHub(projection.projection.componentDataHub);
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.value.componentType, "action/button");
  assertEquals(result.value.componentId, "cmp-1");
  assertEquals(result.value.packageId, "pkg-1");
  assertEquals(result.value.layoutId, "lay-1");
  assertEquals(result.value.wiringId, "wire-1");
});

Deno.test("runtimeComponentAdapter: classname/className/tailwind are merged to spec.className", () => {
  const result = adaptComponentDataHub({
    componentId: "c",
    componentKind: "display/card",
    props: {},
    eventBinding: {},
    design: {
      classname: "db-class",
      className: "camel-class",
      tailwind: "tw-p-2",
      style: "color:red",
    },
  });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.value.className, "db-class camel-class tw-p-2");
});

Deno.test("runtimeComponentAdapter: runtimeConnected catalog entries are factory-reachable and adapter-supported", () => {
  for (
    const entry of COMPONENT_CATALOG_ENTRIES.filter((e) => e.runtimeConnected)
  ) {
    const result = adaptComponentDataHub({
      componentId: "c",
      componentKind: entry.componentKind,
      props: {},
      eventBinding: {},
    });
    assertEquals(
      result.ok,
      true,
      `runtimeConnected catalog kind must be adapter-supported: ${entry.componentKind}`,
    );
  }
});
