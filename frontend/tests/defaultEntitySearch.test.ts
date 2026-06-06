import { assertEquals, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { validationErrorText } from "../api/dispatch.ts";
import { summarizeEmission } from "../runtime/emissionSummary.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import type { Emission, ValidationError } from "../api/dispatch.ts";

// Fixtures use backend-shaped data matching the canonical default:entity:search emission.
const successEmission: Emission = {
  structureMapId: "00000000-0000-0000-0000-000000000004",
  packageId: "00000000-0000-0000-0000-000000000001",
  schemaId: "00000000-0000-0000-0000-000000000002",
  componentIds: ["00000000-0000-0000-0000-000000000003"],
};

// Fixture with layoutId — models structure_map with bound admin-authored layout.
const emissionWithLayout: Emission = {
  structureMapId: "00000000-0000-0000-0000-000000000004",
  packageId: "00000000-0000-0000-0000-000000000001",
  schemaId: "00000000-0000-0000-0000-000000000002",
  componentIds: ["00000000-0000-0000-0000-000000000003"],
  layoutId: "aaaaaaaa-0000-0000-0000-000000000001",
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

Deno.test("pipeline identity: componentIds from emission project to non-error specs", () => {
  // Verifies frontend projection identity from docs/design/pipeline-continuity-ssot.yaml
  // api_command_lane.required_identity: emission.componentIds → renderEmission →
  // ComponentSpec[] where all specs resolve to non-error componentType.
  const specs = renderEmission(successEmission, defaultComponentRegistry);

  assertEquals(specs.length, successEmission.componentIds!.length);
  for (const spec of specs) {
    assertEquals(
      spec.componentType !== "error",
      true,
      `componentId "${spec.componentId}" must resolve to non-error type in registry`,
    );
  }
});

Deno.test("validationErrorText: ATTRACTOR_RESOLVE_FAILED error is rendered correctly", () => {
  const text = validationErrorText(attractorFailedError);

  // Broken attractor error must be surfaced — no silent fallback.
  assertStringIncludes(text, "ATTRACTOR_RESOLVE_FAILED");
});

Deno.test("emission layout identity: layoutId absent when structure_map has no layout", () => {
  assertEquals(successEmission.layoutId, undefined);
  const summary = summarizeEmission(successEmission);
  assertEquals(summary.layoutId, undefined);
});

Deno.test("emission layout identity: layoutId preserved when structure_map has bound layout", () => {
  assertEquals(emissionWithLayout.layoutId, "aaaaaaaa-0000-0000-0000-000000000001");
  const summary = summarizeEmission(emissionWithLayout);
  assertEquals(summary.layoutId, "aaaaaaaa-0000-0000-0000-000000000001");
});

Deno.test("emission layout identity: renderEmission works regardless of layoutId presence", () => {
  const specsNoLayout = renderEmission(successEmission, defaultComponentRegistry);
  const specsWithLayout = renderEmission(emissionWithLayout, defaultComponentRegistry);

  assertEquals(specsNoLayout.length, 1);
  assertEquals(specsWithLayout.length, 1);
  assertEquals(specsNoLayout[0].componentId, specsWithLayout[0].componentId);
  assertEquals(specsNoLayout[0].componentType !== "error", true);
  assertEquals(specsWithLayout[0].componentType !== "error", true);
});
