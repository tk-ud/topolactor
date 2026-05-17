import { assert, assertEquals } from "@std/assert";
import { validationErrorText, type DispatchResponse, type Emission } from "../api/dispatch.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import type { ComponentRegistry } from "../registry/componentRegistry.ts";

Deno.test("default:entity:search emission fixture expands to non-error component", () => {
  const response: DispatchResponse = {
    success: true,
    emission: {
      structureMapId: "00000000-0000-0000-0000-000000000004",
      packageId: "00000000-0000-0000-0000-000000000001",
      schemaId: "00000000-0000-0000-0000-000000000002",
      componentIds: ["00000000-0000-0000-0000-000000000003"],
      errors: [],
    } as Emission,
    errors: [],
  };

  const componentRegistry: ComponentRegistry = {
    "00000000-0000-0000-0000-000000000003": {
      componentId: "00000000-0000-0000-0000-000000000003",
      componentType: "table",
      def: { title: "dummy" },
    },
  };

  const expanded = renderEmission(response.emission as Emission, componentRegistry);

  assertEquals(expanded.length, 1);
  assertEquals(expanded[0].componentId, "00000000-0000-0000-0000-000000000003");
  assert(expanded[0].componentType !== "error");
});

Deno.test("validationErrorText preserves ATTRACTOR_RESOLVE_FAILED visibility", () => {
  const text = validationErrorText({
    Code: "ATTRACTOR_RESOLVE_FAILED",
    Message: "attractor lookup failed",
  });

  assert(text.includes("ATTRACTOR_RESOLVE_FAILED"));
});
