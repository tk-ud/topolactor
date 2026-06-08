/**
 * visualMockParser.test.ts
 * SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml §source_intake_contract
 *
 * Tests verify:
 *   - Parser extracts visual structure only (no AI inference / semantic inference)
 *   - Parser does not infer component identity
 *   - Parser does not infer event semantics
 *   - Parser does not infer data binding semantics
 *   - Unknown objects are preserved as unassigned source_object_id
 *   - Explicit errors on invalid input (no silent fallback)
 *   - Figma JSON returns explicit not_implemented error
 *   - Source hash is deterministic
 */

import {
  assert,
  assertEquals,
  assertFalse,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  computeSourceHash,
  flattenVisualTree,
  parseVisualMockSource,
  type VisualObjectNode,
} from "../runtime/visualMockParser.ts";

// ─── SVG parse happy path ──────────────────────────────────────────────────

const SIMPLE_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="800" height="600">
  <rect id="rect_1" x="10" y="20" width="200" height="100" fill="#fff" />
  <text id="text_1" x="15" y="35">Hello World</text>
  <g id="group_1">
    <rect id="inner_rect" x="50" y="60" width="80" height="40" />
  </g>
</svg>`;

Deno.test("parseVisualMockSource: SVG parse returns ok:true with node tree", () => {
  const result = parseVisualMockSource(SIMPLE_SVG, "svg_xml");
  assert(result.ok, "Expected ok:true");
  if (!result.ok) return;
  assertEquals(result.sourceKind, "svg_xml");
  assert(result.nodeCount > 0, "Expected at least one node");
  assert(result.nodes.length > 0, "Expected at least one root node");
});

Deno.test("parseVisualMockSource: SVG nodes have required fields", () => {
  const result = parseVisualMockSource(SIMPLE_SVG, "svg_xml");
  assert(result.ok);
  if (!result.ok) return;
  const flat = flattenVisualTree(result.nodes);
  for (const node of flat) {
    assert(typeof node.source_object_id === "string", "source_object_id must be string");
    assert(typeof node.object_type === "string", "object_type must be string");
    assert(typeof node.bbox === "object", "bbox must be object");
    assert(typeof node.bbox.x === "number", "bbox.x must be number");
    assert(typeof node.bbox.y === "number", "bbox.y must be number");
    assert(typeof node.bbox.width === "number", "bbox.width must be number");
    assert(typeof node.bbox.height === "number", "bbox.height must be number");
    assert(typeof node.z_index === "number", "z_index must be number");
    assert(typeof node.transform === "string", "transform must be string");
    assert(typeof node.text_content === "string", "text_content must be string");
    assert(typeof node.style_attributes === "object", "style_attributes must be object");
    assert(Array.isArray(node.children), "children must be array");
  }
});

// ─── No AI/semantic inference ──────────────────────────────────────────────

Deno.test("parseVisualMockSource: parser does NOT infer component_key from SVG content", () => {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg">
    <rect id="button_shape" width="100" height="40" fill="blue" />
    <text id="button_label">Submit</text>
  </svg>`;
  const result = parseVisualMockSource(svg, "svg_xml");
  assert(result.ok);
  if (!result.ok) return;
  const flat = flattenVisualTree(result.nodes);
  for (const node of flat) {
    assertFalse(
      "componentKey" in node,
      `Parser must not infer componentKey; found on node ${node.source_object_id}`,
    );
    assertFalse(
      "component_key" in node,
      `Parser must not infer component_key; found on node ${node.source_object_id}`,
    );
  }
});

Deno.test("parseVisualMockSource: parser does NOT infer event semantics", () => {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg">
    <rect id="clickable_btn" onclick="doSomething()" width="120" height="50" />
  </svg>`;
  const result = parseVisualMockSource(svg, "svg_xml");
  assert(result.ok);
  if (!result.ok) return;
  const flat = flattenVisualTree(result.nodes);
  for (const node of flat) {
    assertFalse("event_kind" in node, "Parser must not infer event_kind");
    assertFalse("emits_event" in node, "Parser must not infer emits_event");
    assertFalse("wiring_kind" in node, "Parser must not infer wiring_kind");
  }
});

Deno.test("parseVisualMockSource: parser does NOT infer data binding semantics", () => {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg">
    <text id="user_name_field" x="10" y="30">{{ user.name }}</text>
  </svg>`;
  const result = parseVisualMockSource(svg, "svg_xml");
  assert(result.ok);
  if (!result.ok) return;
  const flat = flattenVisualTree(result.nodes);
  for (const node of flat) {
    assertFalse("binding" in node, "Parser must not infer binding");
    assertFalse("field_binding" in node, "Parser must not infer field_binding");
    assertFalse("controlled_value" in node, "Parser must not infer controlled_value");
  }
  // text_content is preserved as raw string only
  const textNode = flat.find((n) => n.source_object_id === "user_name_field");
  if (textNode) {
    assertEquals(typeof textNode.text_content, "string");
  }
});

// ─── Source object preservation ────────────────────────────────────────────

Deno.test("parseVisualMockSource: preserves all source objects as unassigned (no silent drop)", () => {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg">
    <rect id="unknown_shape_A" width="50" height="50" />
    <circle id="unknown_circle_B" cx="100" cy="100" r="30" />
  </svg>`;
  const result = parseVisualMockSource(svg, "svg_xml");
  assert(result.ok);
  if (!result.ok) return;
  const flat = flattenVisualTree(result.nodes);
  const ids = flat.map((n) => n.source_object_id);
  assert(ids.includes("unknown_shape_A"), "unknown_shape_A must be preserved");
  assert(ids.includes("unknown_circle_B"), "unknown_circle_B must be preserved");
});

Deno.test("parseVisualMockSource: assigns source_object_id from element id attribute", () => {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg">
    <rect id="my_rect_123" x="0" y="0" width="100" height="50" />
  </svg>`;
  const result = parseVisualMockSource(svg, "svg_xml");
  assert(result.ok);
  if (!result.ok) return;
  const flat = flattenVisualTree(result.nodes);
  const node = flat.find((n) => n.source_object_id === "my_rect_123");
  assert(node !== undefined, "Node with id my_rect_123 must be found");
});

// ─── Generic XML ──────────────────────────────────────────────────────────

Deno.test("parseVisualMockSource: generic XML parse returns ok:true", () => {
  const xml = `<?xml version="1.0"?>
<layout>
  <panel id="main_panel" x="0" y="0" width="400" height="300">
    <button id="submit_btn" x="10" y="10" width="100" height="40">Submit</button>
  </panel>
</layout>`;
  const result = parseVisualMockSource(xml, "generic_xml");
  assert(result.ok);
  if (!result.ok) return;
  assertEquals(result.sourceKind, "generic_xml");
  assert(result.nodeCount >= 2, "Expected at least 2 nodes (panel + button)");
});

// ─── Error paths — explicit, not silent ───────────────────────────────────

Deno.test("parseVisualMockSource: empty source returns explicit error (no silent fallback)", () => {
  const result = parseVisualMockSource("");
  assertFalse(result.ok);
  if (result.ok) return;
  assertEquals(result.errorCode, "SOURCE_EMPTY");
  assert(result.message.length > 0);
});

Deno.test("parseVisualMockSource: malformed XML returns explicit error", () => {
  const result = parseVisualMockSource("<unclosed_tag>", "svg_xml");
  assertFalse(result.ok);
  if (result.ok) return;
  assert(result.errorCode.length > 0, "Must return a non-empty errorCode");
  assert(result.message.length > 0, "Must return a non-empty message");
});

Deno.test("parseVisualMockSource: Figma JSON returns explicit not_implemented error", () => {
  const figmaJson = '{"document": {"children": []}}';
  const result = parseVisualMockSource(figmaJson, "figma_json");
  assertFalse(result.ok);
  if (result.ok) return;
  assertEquals(result.errorCode, "FIGMA_JSON_NOT_IMPLEMENTED");
  assert(result.message.length > 0);
});

// ─── flattenVisualTree ─────────────────────────────────────────────────────

Deno.test("flattenVisualTree: returns all nodes in depth-first order", () => {
  const nodes: VisualObjectNode[] = [
    {
      source_object_id: "root",
      object_type: "g",
      parent_source_object_id: null,
      z_index: 1,
      bbox: { x: 0, y: 0, width: 100, height: 100 },
      transform: "",
      text_content: "",
      style_attributes: {},
      children: [
        {
          source_object_id: "child_a",
          object_type: "rect",
          parent_source_object_id: "root",
          z_index: 2,
          bbox: { x: 0, y: 0, width: 50, height: 50 },
          transform: "",
          text_content: "",
          style_attributes: {},
          children: [],
        },
      ],
    },
  ];
  const flat = flattenVisualTree(nodes);
  assertEquals(flat.length, 2);
  assertEquals(flat[0].source_object_id, "root");
  assertEquals(flat[1].source_object_id, "child_a");
});

// ─── computeSourceHash ────────────────────────────────────────────────────

Deno.test("computeSourceHash: same input produces same hash", () => {
  const source = "<svg><rect/></svg>";
  assertEquals(computeSourceHash(source), computeSourceHash(source));
});

Deno.test("computeSourceHash: different inputs produce different hashes", () => {
  const hash1 = computeSourceHash("<svg><rect id='a'/></svg>");
  const hash2 = computeSourceHash("<svg><rect id='b'/></svg>");
  assert(hash1 !== hash2, "Hashes must differ for different inputs");
});

Deno.test("computeSourceHash: returns non-empty string", () => {
  const hash = computeSourceHash("any content");
  assert(hash.length > 0);
  assert(typeof hash === "string");
});

// ─── source kind auto-detection ───────────────────────────────────────────

Deno.test("parseVisualMockSource: auto-detects svg_xml from <svg> prefix", () => {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg"><rect id="r" width="10" height="10"/></svg>`;
  const result = parseVisualMockSource(svg);
  assert(result.ok);
  if (!result.ok) return;
  assertEquals(result.sourceKind, "svg_xml");
});

Deno.test("parseVisualMockSource: auto-detects figma_json from JSON object start", () => {
  const json = '{"document":{}}';
  const result = parseVisualMockSource(json);
  assertFalse(result.ok);
  if (result.ok) return;
  assertEquals(result.errorCode, "FIGMA_JSON_NOT_IMPLEMENTED");
});
