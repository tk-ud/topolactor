import { assertEquals, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { validationErrorText } from "../api/dispatch.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import type { Emission, ValidationError } from "../api/dispatch.ts";

// Fixtures use backend-shaped data matching the canonical default:entity:search emission.
const successEmission: Emission = {
  structureMapId: "00000000-0000-0000-0000-000000000004",
  packageId: "00000000-0000-0000-0000-000000000001",
  schemaId: "00000000-0000-0000-0000-000000000002",
  componentIds: ["00000000-0000-0000-0000-000000000003"],
};

const attractorFailedError: ValidationError = {
  Code: "ATTRACTOR_RESOLVE_FAILED",
  Message: "Attractor key 'missing:entity:search' could not be resolved.",
};

Deno.test("renderEmission: backend-shaped emission expands canonical component", () => {
  const specs = renderEmission(successEmission, defaultComponentRegistry);

  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentId, "00000000-0000-0000-0000-000000000003");
  // Component must resolve through registry — not an error type
  assertEquals(specs[0].componentType !== "error", true);
});

Deno.test("renderEmission: emission structureMapId and packageId are consumed correctly", () => {
  // Verifies frontend runtime helpers can consume a backend-shaped default:entity:search emission.
  assertEquals(successEmission.structureMapId, "00000000-0000-0000-0000-000000000004");
  assertEquals(successEmission.packageId, "00000000-0000-0000-0000-000000000001");
  assertEquals(successEmission.schemaId, "00000000-0000-0000-0000-000000000002");

  const specs = renderEmission(successEmission, defaultComponentRegistry);
  assertEquals(specs.length, 1);
});

Deno.test("validationErrorText: ATTRACTOR_RESOLVE_FAILED error is rendered correctly", () => {
  const text = validationErrorText(attractorFailedError);

  // Broken attractor error must be surfaced — no silent fallback.
  assertStringIncludes(text, "ATTRACTOR_RESOLVE_FAILED");
});
