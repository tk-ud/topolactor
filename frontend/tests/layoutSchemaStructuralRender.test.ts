/**
 * layoutSchemaStructuralRender.test.ts — render completion proof for the layout-schema
 * structural authority contract (docs/design/runtime-orchestration-ssot.yaml
 * ui_projection_render_reachability_contract structural authority contract).
 *
 * Render completion is defined as: renderEmission() on a layout composed from
 * components_layout_design.layout_schema_json.records[] (structural nodes) + resolved
 * catalog leaves (Field/Action, componentId/componentKind from ui_component_registry) +
 * merged tensor runtimeInteractions produces ZERO componentType==="error" specs — not
 * merely "some specs came back" and not "the tensor nodes existed".
 */
import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import type { Emission, LayoutNode } from "../api/dispatch.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";

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
