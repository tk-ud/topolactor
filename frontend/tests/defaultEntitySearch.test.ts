import { assertEquals, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { validationErrorText } from "../api/dispatch.ts";
import { summarizeEmission } from "../runtime/emissionSummary.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import type { Emission, LayoutNode, ValidationError } from "../api/dispatch.ts";

// Fixtures use backend-shaped data matching the canonical default:entity:search emission.
const successEmission: Emission = {
  structureMapId: "00000000-0000-0000-0000-000000000004",
  packageId: "00000000-0000-0000-0000-000000000001",
  schemaId: "00000000-0000-0000-0000-000000000002",
  componentIds: ["00000000-0000-0000-0000-000000000003"],
};

// Fixture: layoutId set but layoutNodes absent — explicit broken-layout state.
const emissionWithLayoutIdOnly: Emission = {
  structureMapId: "00000000-0000-0000-0000-000000000004",
  packageId: "00000000-0000-0000-0000-000000000001",
  schemaId: "00000000-0000-0000-0000-000000000002",
  componentIds: ["00000000-0000-0000-0000-000000000003"],
  layoutId: "aaaaaaaa-0000-0000-0000-000000000001",
};

// Two-component registry for layout ordering tests.
const twoComponentRegistry = {
  ...defaultComponentRegistry,
  "00000000-0000-0000-0000-000000000003": {
    componentId: "00000000-0000-0000-0000-000000000003",
    componentType: "default",
    def: { label: "component-A" },
  },
  "00000000-0000-0000-0000-000000000099": {
    componentId: "00000000-0000-0000-0000-000000000099",
    componentType: "secondary",
    def: { label: "component-B" },
  },
};

// Layout nodes: slot_b (orderIndex=0) → component-B, slot_a (orderIndex=1) → component-A.
// This reversal verifies that renderEmission respects tensor ordering, not componentIds order.
const layoutNodesReversed: LayoutNode[] = [
  { slotKey: "slot_b", orderIndex: 0, componentId: "00000000-0000-0000-0000-000000000099" },
  { slotKey: "slot_a", orderIndex: 1, componentId: "00000000-0000-0000-0000-000000000003" },
];

// Fixture: emission with layoutId AND layoutNodes — full layout-aware state.
const emissionWithLayout: Emission = {
  structureMapId: "00000000-0000-0000-0000-000000000004",
  packageId: "00000000-0000-0000-0000-000000000001",
  schemaId: "00000000-0000-0000-0000-000000000002",
  componentIds: ["00000000-0000-0000-0000-000000000003", "00000000-0000-0000-0000-000000000099"],
  layoutId: "aaaaaaaa-0000-0000-0000-000000000001",
  layoutNodes: layoutNodesReversed,
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

Deno.test("emission layout identity: layoutId preserved in summary when structure_map has bound layout", () => {
  assertEquals(emissionWithLayout.layoutId, "aaaaaaaa-0000-0000-0000-000000000001");
  const summary = summarizeEmission(emissionWithLayout);
  assertEquals(summary.layoutId, "aaaaaaaa-0000-0000-0000-000000000001");
});

Deno.test("layout projection: layoutId set but layoutNodes absent → explicit error spec (no silent fallback)", () => {
  // SSOT contract: layoutId present without layoutNodes is a broken-layout failure.
  // renderEmission must NOT fall back to flat componentIds rendering.
  const specs = renderEmission(emissionWithLayoutIdOnly, defaultComponentRegistry);

  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "error");
  assertStringIncludes(
    String(specs[0].def.error),
    "LAYOUT_NODES_NOT_FOUND",
  );
});

Deno.test("layout projection: layoutNodes ordering drives ComponentSpec order (not componentIds order)", () => {
  // Core contract: when layoutNodes are present, renderEmission MUST use tensor slot ordering.
  // emissionWithLayout has layoutNodes with slot_b (orderIndex=0) → component-B, slot_a (orderIndex=1) → component-A.
  // componentIds order is [component-A, component-B] — the opposite.
  // This test verifies that slot ordering wins over componentIds position.
  const specs = renderEmission(emissionWithLayout, twoComponentRegistry);

  assertEquals(specs.length, 2);

  // First spec must be component-B (orderIndex=0, slotKey="slot_b"), not component-A
  assertEquals(specs[0].componentId, "00000000-0000-0000-0000-000000000099");
  assertEquals(specs[0].componentType, "secondary");
  assertEquals(specs[0].slotKey, "slot_b");
  assertEquals(specs[0].orderIndex, 0);

  // Second spec must be component-A (orderIndex=1, slotKey="slot_a")
  assertEquals(specs[1].componentId, "00000000-0000-0000-0000-000000000003");
  assertEquals(specs[1].componentType, "default");
  assertEquals(specs[1].slotKey, "slot_a");
  assertEquals(specs[1].orderIndex, 1);
});

Deno.test("layout projection: ComponentSpec carries slotKey and orderIndex from tensor nodes", () => {
  const specs = renderEmission(emissionWithLayout, twoComponentRegistry);

  for (const spec of specs) {
    assertEquals(spec.componentType !== "error", true);
    assertEquals(typeof spec.slotKey, "string");
    assertEquals(typeof spec.orderIndex, "number");
  }
});

Deno.test("layout projection: flat renderEmission has no slotKey or orderIndex", () => {
  // Verifies flat path does not leak layout-only fields.
  const specs = renderEmission(successEmission, defaultComponentRegistry);

  assertEquals(specs.length, 1);
  assertEquals(specs[0].slotKey, undefined);
  assertEquals(specs[0].orderIndex, undefined);
});
