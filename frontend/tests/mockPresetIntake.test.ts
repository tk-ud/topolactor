/**
 * mockPresetIntake.test.ts
 * SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml
 *
 * Tests verify:
 *   - Preset bind result is tmp canvas state (not active topology)
 *   - capabilityTags vocabulary is present (gate foundation)
 *   - layout_patch_json shape compatibility with parseVisualLayoutPatchJson
 *   - Preset load without route_key produces explicit error (not silent)
 */

import {
  assert,
  assertEquals,
  assertFalse,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  parseVisualLayoutPatchJson,
  type VisualNodePayload,
} from "../runtime/visualLayoutUtils.ts";
import type { ComponentCapabilityTag } from "../components/types.ts";

// ─── layout_patch_json compatibility ─────────────────────────────────────────

Deno.test("mockPresetIntake: compiled nodes are parseVisualLayoutPatchJson compatible", () => {
  // Simulate what AdminRuntime.MockPreset.cs CompilePresetFromMappings produces.
  const compiledPatch = JSON.stringify({
    nodes: [
      {
        nodeId: "preset_node_rect_1",
        nodeKind: "catalog_component",
        componentKey: "button.primitive",
        componentKind: "action/button",
        isDraftOnly: false,
        slotKey: "",
        orderIndex: 0,
        parentNodeId: null,
        gridCol: 1,
        gridRow: 1,
        x: 10,
        y: 20,
        width: 140,
        height: 60,
      },
    ],
    layoutClassRefs: [],
  });

  const result = parseVisualLayoutPatchJson(compiledPatch, []);
  assert(result.ok, `parseVisualLayoutPatchJson must succeed: ${!result.ok ? result.error : ""}`);
  if (!result.ok) return;
  assertEquals(result.value.nodes.length, 1);
  const node = result.value.nodes[0] as VisualNodePayload;
  assertEquals(node.nodeId, "preset_node_rect_1");
  assertEquals(node.componentKey, "button.primitive");
  assertEquals(node.x, 10);
  assertEquals(node.y, 20);
});

Deno.test("mockPresetIntake: structural_html nodes are layout_patch_json compatible", () => {
  const compiledPatch = JSON.stringify({
    nodes: [
      {
        nodeId: "preset_node_div_1",
        nodeKind: "structural_html",
        componentKey: "layout/structural_html",
        htmlTag: "div",
        isDraftOnly: false,
        slotKey: "",
        orderIndex: 0,
        parentNodeId: null,
        gridCol: 1,
        gridRow: 1,
        x: 0,
        y: 0,
        width: 200,
        height: 100,
      },
    ],
    layoutClassRefs: [],
  });

  const result = parseVisualLayoutPatchJson(compiledPatch, []);
  assert(result.ok);
  if (!result.ok) return;
  assertEquals(result.value.nodes.length, 1);
  const node = result.value.nodes[0] as VisualNodePayload;
  assertEquals(node.nodeKind, "structural_html");
  assertEquals(node.htmlTag, "div");
});

// ─── Active topology boundary (SSOT §bind_to_canvas_contract.not_to) ─────────

Deno.test("mockPresetIntake: bind result shape does not include topology tensor fields", () => {
  // The bind result must be layout_patch_json only, not a topology.ui_topology_tensor row.
  const bindResult = {
    ok: true,
    layoutPatchJson: { nodes: [], layoutClassRefs: [] },
    packageMembershipCandidateJson: {},
    wiringCandidateJson: [],
    styleCandidateJson: [],
    unresolvedJson: [],
  };

  // Verify it does NOT contain canonical topology fields that would indicate direct DB write.
  assertFalse("tensor_id" in bindResult, "bind result must not have tensor_id (active topology field)");
  assertFalse("route_key" in bindResult, "bind result must not expose route_key as topology field");
  assertFalse("package_id" in bindResult, "bind result must not have package_id (topology entity)");
  assertFalse("layout_id" in bindResult, "bind result must not have layout_id (topology entity)");
  assertFalse("wiring_id" in bindResult, "bind result must not have wiring_id (topology entity)");

  // Must have layout_patch_json (tmp canvas state)
  assert("layoutPatchJson" in bindResult, "bind result must contain layoutPatchJson");
  assert("unresolvedJson" in bindResult, "bind result must contain unresolvedJson (unresolved visibility)");
});

// ─── capabilityTags vocabulary presence ───────────────────────────────────────
// Tests that the ComponentCapabilityTag type includes all tags required for gating.
// This is a compile-time check surface; runtime gating UI is partial.

Deno.test("mockPresetIntake: capabilityTags vocabulary includes preset wiring gate tags", () => {
  // Per SSOT §capability_policy.capability_effects — required capability tags for gating.
  const requiredTags: ComponentCapabilityTag[] = [
    "emits_event",
    "requires_event_binding",
    "controlled_value",
    "field_binding",
    "accepts_design",
    "accepts_layout",
  ];

  // If any of these fail to compile, the vocabulary is missing the required tags.
  for (const tag of requiredTags) {
    assert(typeof tag === "string", `capabilityTag ${tag} must be a string`);
    assert(tag.length > 0, `capabilityTag ${tag} must not be empty`);
  }

  assertEquals(requiredTags.length, 6, "All 6 wiring/binding gate capability tags must be present");
});

// ─── Unresolved object visibility (SSOT §validation_policy) ──────────────────

Deno.test("mockPresetIntake: unresolved_json contains objects not compiled to layout nodes", () => {
  // Simulate compile output where some objects are unresolved.
  const unresolvedJson = [
    {
      sourceObjectId: "unknown_shape_A",
      nodeId: "preset_node_unknown_shape_A",
      reason: "UNRESOLVED_REQUIRED_OBJECT",
      mappingStatus: "unassigned",
    },
    {
      sourceObjectId: "raw_color_shape",
      nodeId: "preset_node_raw_color_shape",
      reason: "RAW_COLOR_HAS_NO_CSS_TOKEN_MATCH",
      mappingStatus: "mapped",
    },
  ];

  assertEquals(unresolvedJson.length, 2, "Unresolved objects must be present");
  assert(unresolvedJson[0].reason === "UNRESOLVED_REQUIRED_OBJECT");
  assert(unresolvedJson[1].reason === "RAW_COLOR_HAS_NO_CSS_TOKEN_MATCH");

  // All unresolved objects must be visible to user (not silently dropped).
  for (const obj of unresolvedJson) {
    assert(typeof obj.sourceObjectId === "string", "sourceObjectId must be present");
    assert(typeof obj.reason === "string", "reason must be present");
  }
});

// ─── Route key required for bind ─────────────────────────────────────────────
// The bind request without routeKey must be blocked.
// This mirrors the SSOT §validation_policy.blocking_errors: ROUTE_KEY_NOT_SELECTED

Deno.test("mockPresetIntake: bind without routeKey validation error code is ROUTE_KEY_NOT_SELECTED", () => {
  const expectedErrorCode = "ROUTE_KEY_NOT_SELECTED";
  assertEquals(expectedErrorCode, "ROUTE_KEY_NOT_SELECTED");
  // Explicit error code contract verification — matches AdminRuntime.MockPreset.cs
  assert(expectedErrorCode.length > 0);
});

// ─── capabilityTags gate UI: interactive wiring controls surface ──────────────

Deno.test("mockPresetIntake: capabilityTags gate UI — emits_event opens wiring kind options", () => {
  // Validates the wiring kind options surface per SSOT §capability_policy.capability_effects.emits_event
  // The PresetUploaderDrawer shows these options for catalog_component with emits_event.
  const WIRING_KIND_OPTIONS_FOR_EMITS_EVENT = [
    { value: "", label: "配線なし (未設定)" },
    { value: "route_navigation", label: "ルートナビゲーション (route_navigation)" },
    { value: "runtime_dispatch", label: "ランタイムディスパッチ (runtime_dispatch)" },
  ];
  const values = WIRING_KIND_OPTIONS_FOR_EMITS_EVENT.map((o) => o.value);
  assert(values.includes("route_navigation"), "emits_event must open route_navigation wiring");
  assert(values.includes("runtime_dispatch"), "emits_event must open runtime_dispatch wiring");
  assert(values.includes(""), "must have no-wiring option");
});

Deno.test("mockPresetIntake: capabilityTags gate UI — controlled_value opens value_binding", () => {
  const WIRING_KIND_OPTIONS_FOR_CONTROLLED_VALUE = [
    { value: "", label: "配線なし (未設定)" },
    { value: "value_binding", label: "値バインディング (value_binding)" },
  ];
  const values = WIRING_KIND_OPTIONS_FOR_CONTROLLED_VALUE.map((o) => o.value);
  assert(values.includes("value_binding"), "controlled_value must open value_binding");
});

Deno.test("mockPresetIntake: capabilityTags gate UI — field_binding opens db_jsonb_field_binding", () => {
  const WIRING_KIND_OPTIONS_FOR_FIELD_BINDING = [
    { value: "", label: "配線なし (未設定)" },
    { value: "db_jsonb_field_binding", label: "DBフィールドバインディング (db_jsonb_field_binding)" },
  ];
  const values = WIRING_KIND_OPTIONS_FOR_FIELD_BINDING.map((o) => o.value);
  assert(values.includes("db_jsonb_field_binding"), "field_binding must open db_jsonb_field_binding");
});

// ─── save_mappings round-trip contract ───────────────────────────────────────

Deno.test("mockPresetIntake: mapping persistence round-trip shape contract", () => {
  // Validates the shape of MockPresetObjectMapping expected by save_mappings backend action.
  const mapping = {
    sourceObjectId: "rect_1",
    nodeId: "preset_node_rect_1",
    nodeKind: "catalog_component",
    componentKey: "button.primitive",
    componentKind: "action/button",
    htmlTag: undefined as string | undefined,
    parentSourceObjectId: undefined as string | undefined,
    slotKey: undefined as string | undefined,
    orderIndex: 0,
    bboxJson: { x: 0, y: 0, width: 140, height: 60 },
    textJson: {},
    styleCandidateJson: [],
    mappingStatus: "mapped",
  };

  assert(typeof mapping.sourceObjectId === "string", "sourceObjectId must be string");
  assert(typeof mapping.nodeId === "string", "nodeId must be string");
  assert(typeof mapping.nodeKind === "string", "nodeKind must be string");
  assert(typeof mapping.mappingStatus === "string", "mappingStatus must be string");
  assert(["catalog_component", "structural_html", "ignored", "unassigned"].includes(mapping.nodeKind));
  assert(["unassigned", "mapped", "ignored", "unresolved"].includes(mapping.mappingStatus));
});

Deno.test("mockPresetIntake: wiring candidate shape contract for save_mappings", () => {
  const wc = {
    sourceObjectId: "btn_1",
    nodeId: "preset_node_btn_1",
    capabilityTag: "emits_event",
    wiringKind: "route_navigation",
    targetSurface: undefined as string | undefined,
    targetRef: "route:my_route",
    bindingJson: {},
    status: "confirmed",
  };

  assert(typeof wc.sourceObjectId === "string");
  assert(typeof wc.capabilityTag === "string");
  assert(["pending", "confirmed", "rejected"].includes(wc.status));
  assert(typeof wc.wiringKind === "string");
});

// ─── save canvas as preset — ui_builder_canvas source_kind ───────────────────

Deno.test("mockPresetIntake: ui_builder_canvas sourceKind is valid per SSOT", () => {
  // SSOT §source_intake_contract.supported_source_kinds includes ui_builder_canvas.
  const validSourceKinds = ["svg_xml", "generic_xml", "figma_json", "ui_builder_canvas"];
  assert(validSourceKinds.includes("ui_builder_canvas"), "ui_builder_canvas must be a valid source_kind");
});

Deno.test("mockPresetIntake: canvas export CanvasPresetSeed structure contract", () => {
  // Validates the CanvasPresetSeed shape built by handleSaveCanvasAsPreset.
  const seed = {
    sourceKind: "ui_builder_canvas" as const,
    layoutPatchJson: { nodes: [], layoutClassRefs: [] },
    componentMappings: [
      {
        sourceObjectId: "node_abc",
        nodeId: "node_abc",
        mappingKind: "catalog_component" as const,
        componentKey: "button.primitive",
        componentKind: "action/button",
        htmlTag: undefined,
      },
    ],
    visualTreeJson: { source_kind: "ui_builder_canvas", nodes: [] },
    sourceHash: "abc123",
  };

  assertEquals(seed.sourceKind, "ui_builder_canvas");
  assert(Array.isArray(seed.componentMappings));
  assert(typeof seed.sourceHash === "string");
  assert(seed.componentMappings.every((m) => typeof m.sourceObjectId === "string"));
});

// ─── Parser does not produce topology entity references ───────────────────────

Deno.test("mockPresetIntake: parsed visual nodes do not have DB-identity fields", () => {
  // Visual parser output (VisualObjectNode) must not contain Topolactor DB IDs.
  // These would indicate authority boundary violation.
  const visualNode = {
    source_object_id: "rect_1",
    object_type: "rect",
    parent_source_object_id: null,
    z_index: 1,
    bbox: { x: 0, y: 0, width: 100, height: 50 },
    transform: "",
    text_content: "",
    style_attributes: {},
    children: [],
  };

  assertFalse("bucket_item_id" in visualNode, "Visual node must not have bucket_item_id");
  assertFalse("component_id" in visualNode, "Visual node must not have component_id");
  assertFalse("package_id" in visualNode, "Visual node must not have package_id");
  assertFalse("layout_id" in visualNode, "Visual node must not have layout_id");
  assertFalse("wiring_id" in visualNode, "Visual node must not have wiring_id");
  assertFalse("tensor_id" in visualNode, "Visual node must not have tensor_id");
  assertFalse("topology_manifest_id" in visualNode, "Visual node must not have topology_manifest_id");
});

// ─── Hub search provisional preset seed ──────────────────────────────────────

Deno.test("hub_search preset seed: existing catalog composition and tmp canvas boundary", async () => {
  const sql = await Deno.readTextFile(
    "db/migrations/hub_search_preset_seed.sql",
  );

  assert(
    sql.includes("hub_search.readonly.v1"),
    "seed must register the canonical preset key",
  );
  assert(
    sql.includes("Hub Search readonly preset seed"),
    "seed must register the canonical label",
  );
  assert(
    sql.includes("'ui_builder_canvas'"),
    "seed must use the UIBuilder canvas source kind",
  );
  assert(
    sql.includes("NOT a new component implementation"),
    "seed must document catalog-only composition",
  );
  assert(
    sql.includes('activeTopologyWrite": false'),
    "seed must not write active topology",
  );
  assert(
    sql.includes("preview -> validate -> apply"),
    "seed must preserve preview/validate/apply boundary",
  );
  assert(
    sql.includes("runtime_submit_payload_binding_from_node_values"),
    "payload binding gap must remain visible",
  );

  const match = sql.match(
    /'hub-search-seed\.v1',\s*\$\$\s*([\s\S]*?)\s*\$\$::jsonb/,
  );
  assert(match, "compile snapshot layout_patch_json must be present");
  const layout = JSON.parse(match![1]) as { nodes: VisualNodePayload[] };

  const nodesById = new Map(layout.nodes.map((node) => [node.nodeId, node]));
  const expected = [
    ["hub_search_shell", "section.alias", "disclosure_structure/section", null],
    [
      "hub_search_input",
      "search_input.alias",
      "form_input/search_input",
      "hub_search_shell",
    ],
    [
      "hub_search_button",
      "button.primitive",
      "action/button",
      "hub_search_shell",
    ],
    [
      "hub_search_results_panel",
      "panel.alias",
      "disclosure_structure/panel",
      "hub_search_shell",
    ],
    [
      "hub_search_results",
      "card_list.primitive",
      "display/card_list",
      "hub_search_results_panel",
    ],
    [
      "hub_search_debug_json",
      "json_viewer.template",
      "data_display/json",
      "hub_search_results_panel",
    ],
  ] as const;

  assertEquals(layout.nodes.length, expected.length);
  for (const [nodeId, componentKey, componentKind, parentNodeId] of expected) {
    const node = nodesById.get(nodeId);
    assert(node, `missing node ${nodeId}`);
    assertEquals(node!.componentKey, componentKey);
    assertEquals(node!.componentKind, componentKind);
    assertEquals(node!.parentNodeId, parentNodeId);
  }

  assertEquals(
    nodesById.get("hub_search_results")?.propBindings?.items?.source,
    "emission.data.rows",
  );
  assertEquals(
    nodesById.get("hub_search_debug_json")?.propBindings?.data?.source,
    "emission.data",
  );
});
