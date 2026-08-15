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
 * existed". One documented exception remains, explicit rather than hidden:
 *   - unresolved_gap nodes (topology_ui_unresolved) always render as an explicit error carrying
 *     their authored knownGapRefs, by design — render completion must not paper over a real
 *     unresolved authoring gap.
 * A catalog_component leaf whose authored runtimeInteractions resolve to zero recognized event
 * bindings still fails explicit as RUNTIME_INTERACTION_UNATTRIBUTABLE (see renderEmission.ts) —
 * manifest 092's real json_template_download/json_import Actions no longer hit this path: their
 * actionType "localStateMutation" is now a recognized ui_state_update taxonomy member (SSOT
 * wiring_lane_contract.lanes.internal_instance_wiring), resolved via its ui-local: targetRef.
 *
 * Input boundary: EVERY LayoutNode[] this file dispatches through renderEmission() is real
 * LayoutSchemaTensorComposer output, never a hand-authored LayoutNode literal invented to satisfy
 * renderEmission()'s own expectations by construction. Two different sources of real composer
 * output back this file:
 *   - The first and last tests read frontend/tests/fixtures/manifest_0092_bare_entry_layout_nodes.json,
 *     a checked-in snapshot of the REAL Emission.LayoutNodes produced by an actual bare-entry
 *     dispatch against manifest 00000000-0000-0000-0000-000000000092 (see the fixture's companion
 *     backend assertion in backend/tests/Topolactor.Integration.Tests/
 *     CredentialManagementHubRelationUiProjectionLiveDbTests.cs
 *     DispatchAsync_BareDefaultEntry_NoTargetRef_ResolvesManifest0092ViaCanonicalDefaultEntryRelation,
 *     which fails if this fixture ever drifts from the real seed data) — manifest 092 is kept here
 *     as a legitimate representative regression input, not as this file's sole authority.
 *   - The remaining (generic/common) tests read fixtures under
 *     frontend/tests/fixtures/layout_schema_composed_scenarios/ — each one is the REAL, byte-exact
 *     output of LayoutSchemaTensorComposer.Compose() + StructureMapResolver.ToLayoutNode() for a
 *     small seed-shaped records[] scenario (domain-neutral node names, no manifest-092/credential
 *     specifics), proven by a companion backend test in backend/tests/Topolactor.Runtime.Tests/
 *     LayoutSchemaStructuralCompositionTests.cs (ComposeAndMapToLayoutNode_*_MatchesCheckedInFrontendFixture)
 *     that asserts the SAME byte-exact equality from the backend side — a future composer change
 *     that alters any of these scenarios breaks that backend test too, not just this file. This is
 *     NOT db/seed_empty.sql content and adds no manifest/route/UI — it is a test-only, non-seed
 *     literal records[] JSON scenario, the exact same pattern the rest of
 *     LayoutSchemaStructuralCompositionTests.cs already uses for its own C#-side assertions.
 */
import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h } from "preact";
import { renderToString } from "preact-render-to-string";
import type { Emission, LayoutNode } from "../api/dispatch.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { LayoutProjectionTree } from "../components/LayoutProjectionTree.tsx";

async function loadComposedScenario(fileName: string): Promise<LayoutNode[]> {
  const text = await Deno.readTextFile(
    new URL(`./fixtures/layout_schema_composed_scenarios/${fileName}`, import.meta.url),
  );
  return JSON.parse(text) as LayoutNode[];
}

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

Deno.test("renderEmission: a real LayoutSchemaTensorComposer-composed layout (structural_node category/section/form + resolved catalog_component field/action leaves) produces zero componentType==='error' specs", async () => {
  const layoutNodes = await loadComposedScenario("scenario_structural_catalog_mix.json");
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-000000000101",
    layoutNodes,
    packageId: "00000000-0000-0000-0000-000000000102",
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

Deno.test("renderEmission: structural_node specs carry no componentId/componentKind and render as a distinct componentType from catalog_component", async () => {
  const layoutNodes = await loadComposedScenario("scenario_structural_catalog_mix.json");
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-000000000101",
    layoutNodes,
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

Deno.test("renderEmission: a catalog_component leaf missing componentId (real LayoutSchemaTensorComposer output for an unresolvable field control) still fails explicit (CATALOG_COMPONENT_KIND_REQUIRED-class) — structural authority does not silently paper over an unresolved leaf", async () => {
  const layoutNodes = await loadComposedScenario("scenario_unresolvable_componentid.json");
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-000000000101",
    layoutNodes,
  };
  const specs = renderEmission(emission, defaultComponentRegistry);
  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "error");
});

Deno.test("renderEmission: a catalog_component leaf whose composer-merged runtimeInteractions resolve to zero recognized event bindings fails explicit (RUNTIME_INTERACTION_UNATTRIBUTABLE) — never silently rendered as a normal, unbound component that would only crash later inside the runtime factory", async () => {
  // actionType "localStateMutation" (no target) is outside wiringSettingCategoryOf's recognized
  // taxonomy (ui_state_update / dispatchExternalPort / dispatchInstanceOperation) — no event
  // binding builder attributes this entry to any trigger.
  const layoutNodes = await loadComposedScenario("scenario_unattributable_interaction.json");
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-000000000101",
    layoutNodes,
  };
  const specs = renderEmission(emission, defaultComponentRegistry);
  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "error");
  assertEquals(specs[0].def.code, "RUNTIME_INTERACTION_UNATTRIBUTABLE");
});

Deno.test("renderEmission: a catalog_component leaf whose composer-merged runtimeInteractions DO resolve to a recognized event binding is unaffected by the unattributable check", async () => {
  const layoutNodes = await loadComposedScenario("scenario_attributable_interaction.json");
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-000000000101",
    layoutNodes,
  };
  const specs = renderEmission(emission, defaultComponentRegistry);
  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType, "action/button");
});

Deno.test("renderEmission: a Table (topology_ui_table) or WorkflowStep (topology_ui_workflow_step) composed catalog_component leaf renders exactly like any other resolved catalog_component — not rejected as an unrecognized record type", async () => {
  const layoutNodes = await loadComposedScenario("scenario_table_workflow_step.json");
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-000000000101",
    layoutNodes,
  };
  const specs = renderEmission(emission, defaultComponentRegistry);
  const errorSpecs = specs.filter((s) => s.componentType === "error");
  assertEquals(errorSpecs, []);
  assertEquals(specs.length, 2);
  assertEquals(specs[0].componentType, "display/card_list");
  assertEquals(specs[1].componentType, "action/button");
});

Deno.test("renderEmission: an unresolved_gap node (topology_ui_unresolved) always renders as an explicit error carrying knownGapRefs — never a normal component, never silently dropped", async () => {
  const layoutNodes = await loadComposedScenario("scenario_unresolved_gap.json");
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-000000000101",
    layoutNodes,
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

  // The six well-attributed Action leaves — dispatchInstanceOperation, click-triggered
  // (validate/preview/apply/approve) plus localStateMutation, click-triggered
  // (json_template_download, json_import) — must reach the REAL DOM as their authored label,
  // through the REAL runtime factory (buttonFactory), enabled (no disabled attribute) — proving
  // renderEmission()'s "zero error" claim for these leaves actually holds all the way to markup,
  // not just at the spec layer. json_import's authored trigger is "click" (its actionRef targets
  // a dedicated ui-local trigger key, template_import_trigger — mirroring its sibling
  // json_template_download — never the template_file field's own change event), matching
  // buttonFactory's requireBinding("click") exactly; no componentKind/trigger mismatch.
  for (const label of ["Validate", "Preview", "Apply", "Approve", "Download JSON template", "Import JSON template"]) {
    assert(html.includes(`>${label}<`), `expected the real DOM markup to contain the button label "${label}"; html: ${html.slice(0, 2000)}`);
  }
  // action/button never renders a native <button disabled> for these leaves — production
  // rendering must not inject the UI-Builder canvas-preview placeholder's forced disabled:true
  // (see buildProductionCatalogComponentProps in renderEmission.ts).
  assert(
    !/<button[^>]*\bdisabled\b[^>]*>(?:Validate|Preview|Apply|Approve|Download JSON template|Import JSON template)</.test(html),
    "expected validate/preview/apply/approve/json_template_download/json_import buttons to render enabled (no disabled attribute) in the real DOM",
  );

  // renderEmission() itself produces zero error specs for the real manifest 092 tree.
  const errorSpecCount = specs.filter((s) => s.componentType === "error").length;
  assertEquals(errorSpecCount, 0, "expected zero errors at the renderEmission() spec layer");

  // The real DOM ALSO shows zero error boxes — the fourteen plain select fields (approval_status
  // x2, port_kind, callable, external_api_credential_form_record_kind_input/active_input,
  // credential_category_filter/credential_filter_active_input/credential_filter_record_kind_input
  // (round 5 shared search block), credentials_users_approve_input/role_name_input/active_input
  // (round 5 credentials.users CRUD), eic_record_kind_input/active_input (round 5
  // external_instance_credential CRUD)) have no authored "change" binding at all, so
  // selectFactory renders them within the existing runtime adapter contract without requiring one
  // (the SAME no-binding-required posture formFieldFactory already has for form_input/form_field),
  // closing the gap renderEmission()'s error-spec count alone could not see. Never a substitute
  // proof: this test exists specifically to catch the case where renderEmission() says zero but
  // the real DOM still shows an error box, and it now proves that case does not occur for this
  // scenario.
  const errorBoxMatches = html.match(/rounded border border-red-200/g) ?? [];
  assertEquals(
    errorBoxMatches.length,
    0,
    "expected zero visible error boxes in the real DOM for the manifest 092 bare-entry representative scenario",
  );

  // The fourteen unwired selects render as real <select> elements (not error boxes) — no
  // Field-level read-only/editable authority is introduced; they render the same way an unwired
  // form_input/form_field already does.
  const selectMatches = html.match(/<select\b[^>]*>/g) ?? [];
  assertEquals(
    selectMatches.length,
    14,
    "expected the 14 unwired select fields (approval_status x2, port_kind, callable, " +
      "external_api_credential_form_record_kind_input, external_api_credential_form_active_input, " +
      "credential_category_filter, credential_filter_active_input, credential_filter_record_kind_input, " +
      "credentials_users_approve_input, credentials_users_role_name_input, credentials_users_active_input, " +
      "eic_record_kind_input, eic_active_input) to render as <select> elements in the real DOM",
  );
});
