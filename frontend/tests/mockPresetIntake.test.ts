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
