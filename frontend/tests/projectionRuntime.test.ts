import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { createProjectionRuntime } from "../runtime/projectionRuntime.ts";
import { renderRuntimeComponents } from "../runtime/renderEmission.ts";

Deno.test("projectionRuntime: projection event drives runtime component lane for component_projection", () => {
  const runtime = createProjectionRuntime();
  let receivedKinds: string[] = [];

  runtime.setProjectionDefinition({
    constructorKey: "k",
    packageIds: ["p"],
    outputKind: "component_projection",
    componentId: "cmp-1",
    componentDefinition: {
      componentId: "cmp-1",
      component_kind: "form_input/search_input",
      default_parameters: { value: "", label: "Search" },
      event_binding: { change: { eventType: "change", actorOrSource: "sse" } },
    },
  });

  runtime.onProjectionUpdate((projection) => {
    if (projection.kind !== "component_projection") return;
    const specs = renderRuntimeComponents([projection.componentDataHub]);
    receivedKinds = specs.map((s) => s.componentType);
  });

  runtime.handleProjectionEvent(JSON.stringify({ manifest_id: "m-1", data: { value: "query" } }));

  assertEquals(receivedKinds, ["form_input/search_input"]);
});
