/**
 * layoutSchemaStructuralRender.test.ts — render completion proof for the layout-schema
 * structural authority contract (docs/design/runtime-orchestration-ssot.yaml
 * ui_projection_render_reachability_contract.layout_schema_structural_render_contract).
 *
 * Render completion is defined as: renderEmission() on a layout composed from
 * components_layout_design.layout_schema_json.records[] (structural nodes) + resolved
 * catalog leaves (Field/Action/Table/WorkflowStep, componentId/componentKind from
 * ui_component_registry) + merged tensor runtimeInteractions produces ZERO
 * componentType==="error" specs — not merely "some specs came back" and not "the tensor nodes
 * existed". A layout containing an unresolved_gap node (topology_ui_unresolved) is a documented
 * exception: it always renders as an explicit error carrying its authored knownGapRefs, by
 * design — render completion must not paper over a real unresolved authoring gap.
 *
 * The first test below is the representative-scenario proof: it reads
 * frontend/tests/fixtures/manifest_0092_bare_entry_layout_nodes.json, a checked-in snapshot of
 * the REAL Emission.LayoutNodes produced by an actual bare-entry dispatch against manifest
 * 00000000-0000-0000-0000-000000000092 (see the fixture's companion backend assertion in
 * backend/tests/Topolactor.Integration.Tests/CredentialManagementHubRelationUiProjectionLiveDbTests.cs
 * DispatchAsync_BareDefaultEntry_NoTargetRef_ResolvesManifest0092ViaCanonicalDefaultEntryRelation,
 * which fails if this fixture ever drifts from the real seed data) — not a hand-authored
 * approximation. The remaining tests are synthetic micro-checks isolating specific behaviors.
 */
import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import type { Emission, LayoutNode } from "../api/dispatch.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";

Deno.test("renderEmission: manifest 0092's REAL bare-entry-resolved LayoutNodes (checked-in fixture from an actual live dispatch) produce zero componentType==='error' specs", async () => {
  const fixtureText = await Deno.readTextFile(
    new URL("./fixtures/manifest_0092_bare_entry_layout_nodes.json", import.meta.url),
  );
  const layoutNodes = JSON.parse(fixtureText) as LayoutNode[];
  assert(layoutNodes.length > 0, "fixture must contain the real composed LayoutNodes");

  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes,
    packageId: "00000000-0000-0000-0000-0000000cd005",
    manifestId: "00000000-0000-0000-0000-000000000092",
  };

  const specs = renderEmission(emission, defaultComponentRegistry);

  const errorSpecs = specs.filter((s) => s.componentType === "error");
  assertEquals(
    errorSpecs,
    [],
    `render completion requires zero error components for the REAL manifest 0092 bare-entry emission; found: ${
      JSON.stringify(errorSpecs)
    }`,
  );
  assert(specs.length === layoutNodes.length, "every real LayoutNode must produce a rendered spec");
});

/**
 * A layout shape representative of the backend's LayoutSchemaTensorComposer output for
 * manifest 00000000-0000-0000-0000-000000000092: structural_node wrappers (Category/Section/
 * Form) carrying no componentId/componentKind, and catalog_component leaves (Field/Action)
 * with componentId/componentKind resolved from the ui_component_registry preset catalog.
 */
function buildComposedManifest0092LikeLayoutNodes(): LayoutNode[] {
  return [
    {
      nodeId: "instance_settings",
      nodeKind: "structural_node",
      recordType: "topology_ui_category",
      label: "Instance settings",
      orderIndex: 0,
    },
    {
      nodeId: "instance_settings_section",
      nodeKind: "structural_node",
      recordType: "topology_ui_section",
      label: "Instance settings",
      parentNodeId: "instance_settings",
      orderIndex: 1,
    },
    {
      nodeId: "instance_address_form",
      nodeKind: "structural_node",
      recordType: "topology_ui_form",
      label: "Instance address",
      parentNodeId: "instance_settings_section",
      orderIndex: 2,
    },
    {
      nodeId: "instance_authority_key",
      nodeKind: "catalog_component",
      componentId: "00000000-0000-0000-0001-000000000013",
      componentKind: "form_input/form_field",
      parentNodeId: "instance_address_form",
      orderIndex: 3,
    },
    {
      nodeId: "validate",
      nodeKind: "catalog_component",
      componentId: "00000000-0000-0000-0001-000000000010",
      componentKind: "action/button",
      parentNodeId: "instance_address_form",
      orderIndex: 4,
      runtimeInteractions: [
        {
          trigger: "click",
          actionType: "dispatchInstanceOperation",
          instanceTargetRef: "instance-port:db_instance_port:instance_authority_key:operation_binding_key",
        },
      ],
    },
  ];
}

Deno.test("renderEmission: a layout composed from structural_node + resolved catalog_component leaves produces zero componentType==='error' specs", () => {
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes: buildComposedManifest0092LikeLayoutNodes(),
    packageId: "00000000-0000-0000-0000-0000000cd005",
  };

  const specs = renderEmission(emission, defaultComponentRegistry);

  const errorSpecs = specs.filter((s) => s.componentType === "error");
  assertEquals(
    errorSpecs,
    [],
    `render completion requires zero error components; found: ${JSON.stringify(errorSpecs)}`,
  );
  assert(specs.length > 0, "composed layout must still produce specs to render");
});

Deno.test("renderEmission: structural_node specs carry no componentId/componentKind and render as a distinct componentType from catalog_component", () => {
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes: buildComposedManifest0092LikeLayoutNodes(),
  };
  const specs = renderEmission(emission, defaultComponentRegistry);

  const structuralSpecs = specs.filter((s) => s.nodeKind === "structural_node");
  assert(structuralSpecs.length === 3, "expected 3 structural_node specs (category/section/form)");
  for (const spec of structuralSpecs) {
    assertEquals(spec.componentType, "structural_node");
    assertEquals(spec.componentId, undefined);
  }

  const catalogSpecs = specs.filter((s) => s.nodeKind === "catalog_component");
  assert(catalogSpecs.length === 2, "expected 2 catalog_component leaf specs (field + action)");
  for (const spec of catalogSpecs) {
    assert(spec.componentId, "a resolved catalog_component leaf must carry its componentId");
  }
});

Deno.test("renderEmission: a catalog_component leaf missing componentId still fails explicit (CATALOG_COMPONENT_KIND_REQUIRED-class) — structural authority does not silently paper over an unresolved leaf", () => {
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes: [
      {
        nodeId: "unresolvable_field",
        nodeKind: "catalog_component",
        // componentId intentionally absent (e.g. control had no registry mapping).
        orderIndex: 0,
      },
    ],
  };
  const specs = renderEmission(emission, defaultComponentRegistry);
  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "error");
});

Deno.test("renderEmission: a Table (topology_ui_table) or WorkflowStep (topology_ui_workflow_step) composed catalog_component leaf renders exactly like any other resolved catalog_component — not rejected as an unrecognized record type", () => {
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes: [
      {
        nodeId: "results_table",
        nodeKind: "catalog_component",
        recordType: "topology_ui_table",
        componentId: "00000000-0000-0000-0001-000000000014",
        componentKind: "display/card_list",
        orderIndex: 0,
      },
      {
        nodeId: "approval_step",
        nodeKind: "catalog_component",
        recordType: "topology_ui_workflow_step",
        componentId: "00000000-0000-0000-0001-000000000010",
        componentKind: "action/button",
        orderIndex: 1,
      },
    ],
  };
  const specs = renderEmission(emission, defaultComponentRegistry);
  const errorSpecs = specs.filter((s) => s.componentType === "error");
  assertEquals(errorSpecs, []);
  assertEquals(specs.length, 2);
  assertEquals(specs[0].componentType, "display/card_list");
  assertEquals(specs[1].componentType, "action/button");
});

Deno.test("renderEmission: an unresolved_gap node (topology_ui_unresolved) always renders as an explicit error carrying knownGapRefs — never a normal component, never silently dropped", () => {
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes: [
      {
        nodeId: "unresolved_fragment",
        nodeKind: "unresolved_gap",
        recordType: "topology_ui_unresolved",
        label: "Unresolved fragment",
        knownGapRefs: ["table_item_click_wiring_not_yet_expressible"],
        orderIndex: 0,
      },
    ],
  };
  const specs = renderEmission(emission, defaultComponentRegistry);
  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "error");
  assertEquals(specs[0].def.code, "TOPOLOGY_UI_UNRESOLVED_GAP_REF");
  assertEquals(specs[0].def.knownGapRefs, ["table_item_click_wiring_not_yet_expressible"]);
});
