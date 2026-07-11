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
 * existed". Two documented exceptions exist, both explicit rather than hidden:
 *   - unresolved_gap nodes (topology_ui_unresolved) always render as an explicit error carrying
 *     their authored knownGapRefs, by design — render completion must not paper over a real
 *     unresolved authoring gap.
 *   - a catalog_component leaf whose authored runtimeInteractions resolve to zero recognized
 *     event bindings (RUNTIME_INTERACTION_UNATTRIBUTABLE — e.g. manifest 092's real
 *     json_template_download/json_import Actions, whose actionType "localStateMutation" has no
 *     implemented runtime handler anywhere in this codebase) is an honest, attributable error at
 *     THIS layer — never a silently-unbound component that only fails later, cryptically, deep
 *     inside the runtime factory. This is an out-of-scope business-data/wiring-completeness gap
 *     in the seed's authoring, not a structural-render or projection-identity defect.
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
import { h } from "preact";
import { renderToString } from "preact-render-to-string";
import type { Emission, LayoutNode } from "../api/dispatch.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { LayoutProjectionTree } from "../components/LayoutProjectionTree.tsx";

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
  // The only errors the REAL manifest 092 tree may ever produce are the two known,
  // attributable RUNTIME_INTERACTION_UNATTRIBUTABLE Actions (json_template_download/json_import
  // — see module doc comment above). Any OTHER error is a real render-completion regression.
  const unexpectedErrorSpecs = errorSpecs.filter((s) =>
    (s.def as Record<string, unknown> | undefined)?.code !== "RUNTIME_INTERACTION_UNATTRIBUTABLE"
  );
  assertEquals(
    unexpectedErrorSpecs,
    [],
    `render completion requires zero UNEXPECTED error components for the REAL manifest 0092 bare-entry emission; found: ${
      JSON.stringify(unexpectedErrorSpecs)
    }`,
  );
  assertEquals(
    errorSpecs.length,
    2,
    `expected exactly the two known-gap Actions (json_template_download, json_import) to surface RUNTIME_INTERACTION_UNATTRIBUTABLE; found ${errorSpecs.length} error spec(s): ${
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

Deno.test("renderEmission: a catalog_component leaf whose authored runtimeInteractions resolve to zero recognized event bindings fails explicit (RUNTIME_INTERACTION_UNATTRIBUTABLE) — never silently rendered as a normal, unbound component that would only crash later inside the runtime factory", () => {
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes: [
      {
        nodeId: "json_template_download",
        nodeKind: "catalog_component",
        componentId: "00000000-0000-0000-0001-000000000010",
        componentKind: "action/button",
        orderIndex: 0,
        // actionType "localStateMutation" is outside wiringSettingCategoryOf's recognized
        // taxonomy (ui_state_update / dispatchExternalPort / dispatchInstanceOperation) — no
        // event binding builder attributes this entry to any trigger.
        runtimeInteractions: [
          {
            trigger: "click",
            actionType: "localStateMutation",
          },
        ],
      },
    ],
  };
  const specs = renderEmission(emission, defaultComponentRegistry);
  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "error");
  assertEquals(specs[0].def.code, "RUNTIME_INTERACTION_UNATTRIBUTABLE");
});

Deno.test("renderEmission: a catalog_component leaf whose authored runtimeInteractions DO resolve to a recognized event binding is unaffected by the unattributable check", () => {
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes: [
      {
        nodeId: "validate",
        nodeKind: "catalog_component",
        componentId: "00000000-0000-0000-0001-000000000010",
        componentKind: "action/button",
        label: "Validate",
        orderIndex: 0,
        runtimeInteractions: [
          {
            trigger: "click",
            actionType: "dispatchInstanceOperation",
            instanceTargetRef: "instance-port:db_instance_port:instance_authority_key:operation_binding_key",
          },
        ],
      },
    ],
  };
  const specs = renderEmission(emission, defaultComponentRegistry);
  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "action/button");
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

Deno.test("DOM-connected proof: manifest 0092's REAL bare-entry-resolved LayoutNodes render through renderEmission() -> LayoutProjectionTree -> runtimeComponentFactory into real DOM markup — not just resolved specs", async () => {
  const fixtureText = await Deno.readTextFile(
    new URL("./fixtures/manifest_0092_bare_entry_layout_nodes.json", import.meta.url),
  );
  const layoutNodes = JSON.parse(fixtureText) as LayoutNode[];

  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes,
    packageId: "00000000-0000-0000-0000-0000000cd005",
    manifestId: "00000000-0000-0000-0000-000000000092",
  };

  const specs = renderEmission(emission, defaultComponentRegistry);
  const html = renderToString(
    h(LayoutProjectionTree, { specs, layoutId: emission.layoutId }),
  );

  // The four well-attributed Action leaves (dispatchInstanceOperation, click-triggered —
  // validate/preview/apply/approve) must reach the REAL DOM as their authored label, through the
  // REAL runtime factory (buttonFactory), enabled (no disabled attribute) — proving
  // renderEmission()'s "zero error" claim for these leaves actually holds all the way to markup,
  // not just at the spec layer.
  for (const label of ["Validate", "Preview", "Apply", "Approve"]) {
    assert(html.includes(`>${label}<`), `expected the real DOM markup to contain the button label "${label}"; html: ${html.slice(0, 2000)}`);
  }
  // action/button never renders a native <button disabled> for these leaves — production
  // rendering must not inject the UI-Builder canvas-preview placeholder's forced disabled:true
  // (see buildProductionCatalogComponentProps in renderEmission.ts).
  assert(
    !/<button[^>]*\bdisabled\b[^>]*>(?:Validate|Preview|Apply|Approve)</.test(html),
    "expected validate/preview/apply/approve buttons to render enabled (no disabled attribute) in the real DOM",
  );

  // The real DOM shows MORE error boxes than renderEmission()'s own error-spec count — this is
  // the exact disconnect this proof exists to surface, not hide: renderEmission() catches 2
  // (the documented RUNTIME_INTERACTION_UNATTRIBUTABLE json_template_download/json_import
  // Actions), but LayoutProjectionTree's runtimeComponentFactory pass additionally discovers 4
  // MORE failures for plain, unwired select fields (approval_status x2, port_kind, callable)
  // whose factory unconditionally requires a "change" binding no seed content ever authored —
  // a pre-existing, out-of-scope business-data/seed-authoring-completeness gap (not specific to
  // schema composition; the same factory contract applies to any select field anywhere), never
  // silently absorbed or hidden from the real DOM. A backend-only "zero unresolved leaves" count
  // or renderEmission()'s own error-spec count alone would have missed these 4 entirely.
  const errorSpecCount = specs.filter((s) => s.componentType === "error").length;
  assertEquals(errorSpecCount, 2, "expected exactly the 2 RUNTIME_INTERACTION_UNATTRIBUTABLE Actions at the renderEmission() spec layer");
  const errorBoxMatches = html.match(/rounded border border-red-200/g) ?? [];
  assertEquals(
    errorBoxMatches.length,
    6,
    "expected 6 total visible error boxes in the real DOM: 2 renderEmission()-level (RUNTIME_INTERACTION_UNATTRIBUTABLE) + 4 additional runtime-factory-level (pre-existing unwired select fields) — every one explicit and visible, none silently dropped from the markup",
  );
});
