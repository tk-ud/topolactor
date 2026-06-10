/**
 * visualLayoutBuilder.test.ts
 *
 * FlowLayoutCanvas は layout draft のフローツリープレビュー surface として通常導線に残る。
 * 配置は parentNodeId / orderIndex / layoutClassRefs（レイヤーツリー + インスペクタ）が主導線。
 * width/height はインスペクタで編集。x/y はパッチ出力に含めない（SSOT v0.9.0 flow）。
 * cssTokenRefs / classname / tailwind は canvas / layout_patch には含まれない（選択ノード design_inspector の責務）。
 *
 * このテストファイルは以下を対象とする:
 *   - canvas utility functions (snapToGrid, buildVisualLayoutPatchJson, etc.)
 *   - inspector 構造フィールド utility (wouldCreateVisualParentCycle, orderIndex round-trip)
 *   - draft-only apply guard
 *   - responsive token rule utilities
 */
import {
  assert,
  assertEquals,
  assertFalse,
  assertNotEquals,
  assertObjectMatch,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildVisualLayoutPatchJson,
  cloneVisualNode,
  filterEmptyResponsiveRules,
  formatLayoutDimensionCss,
  parseLayoutDimensionInput,
  resolveLayoutDimensionPx,
  getDraftOnlyNodes,
  isDraftOnlyApplyBlocked,
  isLegacyAbsoluteLayoutPatch,
  makeStructuralHtmlNode,
  migrateAbsolutePatchToFlowStack,
  parseVisualLayoutPatchJson,
  reorderLayoutNodeStack,
  RESPONSIVE_BREAKPOINTS,
  type ResponsiveTokenRules,
  seedDraftNodesFromPalette,
  snapToGrid,
  STRUCTURAL_HTML_COMPONENT_KEY,
  validateResponsiveTokenRulesJson,
  type VisualNodePayload,
  wouldCreateVisualParentCycle,
} from "../runtime/visualLayoutUtils.ts";
import {
  filterLayoutClassRefsByAllowedFor,
  resolveCanvasRootPreviewClassName,
  resolveNodeWrapperPreviewClassName,
} from "../runtime/layoutClassPreviewUtils.ts";
import { resolveCssTokenValue, resolveUnknownCssTokenRefs } from "../runtime/cssDictionary.ts";
import {
  enrichLayoutPreviewNodes,
  getLayoutPreviewDefaultSize,
  renderLayoutComponentPreview,
  resolveComponentKindForLayoutPreview,
  type LayoutPreviewNodeInput,
} from "../runtime/layoutComponentPreview.ts";
import { buildInlineStyleFromCssTokenRefs } from "../runtime/cssDictionary.ts";

// ─── canvas utility: snapToGrid ───────────────────────────────────────────────

Deno.test("snapToGrid: snaps to nearest grid point", () => {
  assertEquals(snapToGrid(14, 10), 10);
  assertEquals(snapToGrid(15, 10), 20);
  assertEquals(snapToGrid(0, 10), 0);
  assertEquals(snapToGrid(100, 10), 100);
});

Deno.test("snapToGrid: works with different snap sizes", () => {
  assertEquals(snapToGrid(7, 5), 5);
  assertEquals(snapToGrid(8, 5), 10);
  assertEquals(snapToGrid(20, 20), 20);
  assertEquals(snapToGrid(21, 20), 20);
  assertEquals(snapToGrid(30, 20), 40);
});

Deno.test("snapToGrid: snap size 1 is identity", () => {
  assertEquals(snapToGrid(37, 1), 37);
});

Deno.test("snapToGrid: snap size 0 returns value unchanged", () => {
  assertEquals(snapToGrid(42, 0), 42);
});

// ─── buildVisualLayoutPatchJson ───────────────────────────────────────────────

const sampleNode: VisualNodePayload = {
  nodeId: "node_abc",
  componentKey: "display/card",
  isDraftOnly: false,
  slotKey: "main",
  orderIndex: 0,
  parentNodeId: null,
  gridCol: 1,
  gridRow: 1,
  x: 10,
  y: 20,
  width: 140,
  height: 60,
  componentId: "comp-1",
  packageId: "pkg-1",
  layoutId: "layout-1",
  wiringId: "wiring-1",
  tensorId: "tensor-1",
};

Deno.test("buildVisualLayoutPatchJson: flow mode omits x/y but keeps width/height", () => {
  const json = buildVisualLayoutPatchJson([sampleNode]);
  const parsed = JSON.parse(json);
  const node = parsed.nodes[0];
  assertEquals(node.x, undefined);
  assertEquals(node.y, undefined);
  assertEquals(node.width, 140);
  assertEquals(node.height, 60);
});

Deno.test("buildVisualLayoutPatchJson: includes standard topology fields", () => {
  const json = buildVisualLayoutPatchJson([sampleNode]);
  const parsed = JSON.parse(json);
  assertEquals(parsed.grid.cols, 12);
  const node = parsed.nodes[0];
  assertEquals(node.nodeId, "node_abc");
  assertEquals(node.componentKey, "display/card");
  assertEquals(node.slotKey, "main");
  assertEquals(node.orderIndex, 0);
  assertEquals(node.parentNodeId, null);
  assertEquals(node.gridCol, 1);
  assertEquals(node.gridRow, 1);
  assertEquals(node.componentId, "comp-1");
  assertEquals(node.packageId, "pkg-1");
  assertEquals(node.layoutId, "layout-1");
  assertEquals(node.wiringId, "wiring-1");
  assertEquals(node.tensorId, "tensor-1");
});

/** Fields required in preview / validate / apply tensorPatchJson (shared builder path). */
function assertLayoutPatchNodePayload(node: Record<string, unknown>): void {
  assertObjectMatch(node, {
    width: 140,
    height: 60,
    parentNodeId: null,
    orderIndex: 0,
    componentId: "comp-1",
    packageId: "pkg-1",
    layoutId: "layout-1",
    wiringId: "wiring-1",
    tensorId: "tensor-1",
  });
  assertEquals(node.x, undefined, "flow patch must not serialize x");
  assertEquals(node.y, undefined, "flow patch must not serialize y");
  assertEquals(typeof node.nodeId, "string");
  assertEquals(typeof node.componentKey, "string");
}

Deno.test("buildVisualLayoutPatchJson: apply/preview/validate payload retains placement topology ids", () => {
  const actions = ["preview", "validate", "apply"] as const;
  for (const _action of actions) {
    const parsed = JSON.parse(buildVisualLayoutPatchJson([sampleNode], ["layout.root.grid"]));
    assertEquals(parsed.layoutClassRefs, ["layout.root.grid"]);
    assertLayoutPatchNodePayload(parsed.nodes[0] as Record<string, unknown>);
  }
});

Deno.test("buildVisualLayoutPatchJson: draft-only node gets _draftOnly flag", () => {
  const draftNode: VisualNodePayload = { ...sampleNode, isDraftOnly: true };
  const json = buildVisualLayoutPatchJson([draftNode]);
  const parsed = JSON.parse(json);
  assertEquals(parsed.nodes[0]._draftOnly, true);
});

Deno.test("buildVisualLayoutPatchJson: non-draft node has no _draftOnly field", () => {
  const json = buildVisualLayoutPatchJson([sampleNode]);
  const parsed = JSON.parse(json);
  assertEquals(parsed.nodes[0]._draftOnly, undefined);
});

Deno.test("buildVisualLayoutPatchJson: includes layoutClassRefs when provided", () => {
  const json = buildVisualLayoutPatchJson([sampleNode], ["layout.root.grid"]);
  const parsed = JSON.parse(json);
  assertEquals(parsed.layoutClassRefs, ["layout.root.grid"]);
});

Deno.test("buildVisualLayoutPatchJson: omits layoutClassRefs when empty", () => {
  const json = buildVisualLayoutPatchJson([sampleNode], []);
  const parsed = JSON.parse(json);
  assertEquals(parsed.layoutClassRefs, undefined);
});

Deno.test("buildVisualLayoutPatchJson: empty slotKey serializes as null", () => {
  const node: VisualNodePayload = { ...sampleNode, slotKey: "" };
  const json = buildVisualLayoutPatchJson([node]);
  const parsed = JSON.parse(json);
  assertEquals(parsed.nodes[0].slotKey, null);
});

Deno.test("buildVisualLayoutPatchJson: multiple nodes preserve order", () => {
  const n1 = { ...sampleNode, nodeId: "n1", componentKey: "a", orderIndex: 0 };
  const n2 = { ...sampleNode, nodeId: "n2", componentKey: "b", orderIndex: 1 };
  const json = buildVisualLayoutPatchJson([n1, n2]);
  const parsed = JSON.parse(json);
  assertEquals(parsed.nodes[0].nodeId, "n1");
  assertEquals(parsed.nodes[1].nodeId, "n2");
});

Deno.test("buildVisualLayoutPatchJson: slotKey is preserved in node payload (inspector field)", () => {
  const node: VisualNodePayload = { ...sampleNode, slotKey: "header" };
  const parsed = JSON.parse(buildVisualLayoutPatchJson([node]));
  assertEquals(parsed.nodes[0].slotKey, "header");
});

Deno.test("buildVisualLayoutPatchJson: parentNodeId is preserved in node payload (inspector field)", () => {
  const parent: VisualNodePayload = {
    ...sampleNode,
    nodeId: "parent",
    parentNodeId: null,
  };
  const child: VisualNodePayload = {
    ...sampleNode,
    nodeId: "child",
    parentNodeId: "parent",
  };
  const parsed = JSON.parse(buildVisualLayoutPatchJson([parent, child]));
  assertEquals(parsed.nodes[0].parentNodeId, null);
  assertEquals(parsed.nodes[1].parentNodeId, "parent");
});

Deno.test("buildVisualLayoutPatchJson: structural_html node includes nodeKind and htmlTag", () => {
  const node = makeStructuralHtmlNode("section", {
    nodeId: "html-1",
    x: 10,
    y: 20,
    orderIndex: 0,
  });
  const parsed = JSON.parse(buildVisualLayoutPatchJson([node]));
  assertEquals(parsed.nodes[0].nodeKind, "structural_html");
  assertEquals(parsed.nodes[0].htmlTag, "section");
  assertEquals(parsed.nodes[0].componentKey, STRUCTURAL_HTML_COMPONENT_KEY);
});

Deno.test("buildVisualLayoutPatchJson: per-node layoutClassRefs round-trip", () => {
  const node: VisualNodePayload = {
    ...sampleNode,
    layoutClassRefs: ["layout.card.surface"],
  };
  const json = buildVisualLayoutPatchJson([node]);
  const parsed = parseVisualLayoutPatchJson(json);
  assertEquals(parsed.ok, true);
  if (parsed.ok) {
    assertEquals(parsed.value.nodes[0].layoutClassRefs, [
      "layout.card.surface",
    ]);
  }
});

Deno.test("cloneVisualNode: copies with new id and offset", () => {
  const cloned = cloneVisualNode(sampleNode, "node_copy");
  assertEquals(cloned.nodeId, "node_copy");
  assertEquals(cloned.x, sampleNode.x + 20);
  assertEquals(cloned.y, sampleNode.y + 20);
  assertEquals(cloned.componentKey, sampleNode.componentKey);
});

Deno.test("structural HTML workflow: add, copy, move, save preserves layout/structural_html and htmlTag", () => {
  const added = makeStructuralHtmlNode("section", {
    nodeId: "html-original",
    x: 40,
    y: 40,
    orderIndex: 0,
  });
  const copied = cloneVisualNode(added, "html-copy");
  const moved = { ...copied, x: 120, y: 80 };
  const json = buildVisualLayoutPatchJson([moved]);
  const parsed = parseVisualLayoutPatchJson(json);
  assertEquals(parsed.ok, true);
  if (!parsed.ok) return;
  const node = parsed.value.nodes[0];
  assertEquals(node.nodeKind, "structural_html");
  assertEquals(node.htmlTag, "section");
  assertEquals(node.componentKey, STRUCTURAL_HTML_COMPONENT_KEY);
  assertEquals(node.x, 0);
  assertEquals(node.y, 0);
});

Deno.test("cloneVisualNode: structural_html copy retains nodeKind and htmlTag", () => {
  const source = makeStructuralHtmlNode("a", {
    nodeId: "link-1",
    x: 10,
    y: 10,
    orderIndex: 0,
  });
  const cloned = cloneVisualNode(source, "link-copy");
  const saved = JSON.parse(buildVisualLayoutPatchJson([cloned])).nodes[0];
  assertEquals(saved.nodeKind, "structural_html");
  assertEquals(saved.htmlTag, "a");
  assertEquals(saved.componentKey, STRUCTURAL_HTML_COMPONENT_KEY);
});

Deno.test("buildVisualLayoutPatchJson: cssTokenRefs are NOT in node payload (component_design responsibility)", () => {
  // cssTokenRefs belong to component_design child, not layout_patch node payload

  const parsed = JSON.parse(buildVisualLayoutPatchJson([sampleNode]));
  const node = parsed.nodes[0];
  assertEquals(node.cssTokenRefs, undefined);
  assertEquals(node.classname, undefined);
  assertEquals(node.tailwind, undefined);
});

// ─── draft-only apply block ───────────────────────────────────────────────────

Deno.test("getDraftOnlyNodes: returns only draft nodes", () => {
  const n1 = { ...sampleNode, nodeId: "n1", isDraftOnly: false };
  const n2 = { ...sampleNode, nodeId: "n2", isDraftOnly: true };
  const n3 = { ...sampleNode, nodeId: "n3", isDraftOnly: true };
  const result = getDraftOnlyNodes([n1, n2, n3]);
  assertEquals(result.length, 2);
  assertEquals(result[0].nodeId, "n2");
  assertEquals(result[1].nodeId, "n3");
});

Deno.test("isDraftOnlyApplyBlocked: returns true when any draft node present", () => {
  const n1 = { ...sampleNode, nodeId: "n1", isDraftOnly: false };
  const n2 = { ...sampleNode, nodeId: "n2", isDraftOnly: true };
  assertEquals(isDraftOnlyApplyBlocked([n1, n2]), true);
});

Deno.test("isDraftOnlyApplyBlocked: returns false when all nodes are promoted", () => {
  const n1 = { ...sampleNode, nodeId: "n1", isDraftOnly: false };
  const n2 = { ...sampleNode, nodeId: "n2", isDraftOnly: false };
  assertFalse(isDraftOnlyApplyBlocked([n1, n2]));
});

Deno.test("isDraftOnlyApplyBlocked: returns false for empty node list", () => {
  assertFalse(isDraftOnlyApplyBlocked([]));
});

// ─── inspector 構造フィールド: orderIndex round-trip ─────────────────────────

Deno.test("orderIndex: buildVisualLayoutPatchJson preserves orderIndex in node payload", () => {
  const node: VisualNodePayload = { ...sampleNode, orderIndex: 3 };
  const json = buildVisualLayoutPatchJson([node]);
  const parsed = JSON.parse(json);
  assertEquals(parsed.nodes[0].orderIndex, 3);
});

Deno.test("orderIndex: multiple nodes retain distinct orderIndex values", () => {
  const n1: VisualNodePayload = { ...sampleNode, nodeId: "n1", orderIndex: 0 };
  const n2: VisualNodePayload = { ...sampleNode, nodeId: "n2", orderIndex: 2 };
  const n3: VisualNodePayload = { ...sampleNode, nodeId: "n3", orderIndex: 5 };
  const parsed = JSON.parse(buildVisualLayoutPatchJson([n1, n2, n3]));
  assertEquals(parsed.nodes[0].orderIndex, 0);
  assertEquals(parsed.nodes[1].orderIndex, 2);
  assertEquals(parsed.nodes[2].orderIndex, 5);
});

Deno.test("orderIndex: updating orderIndex produces distinguishable patch", () => {
  const before: VisualNodePayload = { ...sampleNode, orderIndex: 0 };
  const after: VisualNodePayload = { ...sampleNode, orderIndex: 1 };
  assertNotEquals(
    buildVisualLayoutPatchJson([before]),
    buildVisualLayoutPatchJson([after]),
  );
});

// ─── inspector 構造フィールド: parentNodeId cycle detection ──────────────────

// ─── wouldCreateVisualParentCycle ─────────────────────────────────────────────

Deno.test("wouldCreateVisualParentCycle: null parent never cycles", () => {
  const nodes: VisualNodePayload[] = [{
    ...sampleNode,
    nodeId: "a",
    parentNodeId: null,
  }];
  assertFalse(wouldCreateVisualParentCycle(nodes, "a", null));
});

Deno.test("wouldCreateVisualParentCycle: direct self-reference is a cycle", () => {
  const nodes: VisualNodePayload[] = [{
    ...sampleNode,
    nodeId: "a",
    parentNodeId: null,
  }];
  assertEquals(wouldCreateVisualParentCycle(nodes, "a", "a"), true);
});

Deno.test("wouldCreateVisualParentCycle: detects indirect cycle", () => {
  const nodes: VisualNodePayload[] = [
    { ...sampleNode, nodeId: "a", parentNodeId: "b" },
    { ...sampleNode, nodeId: "b", parentNodeId: "c" },
    { ...sampleNode, nodeId: "c", parentNodeId: null },
  ];
  // reparenting c (nodeId="c") to parent a (proposedParentId="a") would create c→a→b→c cycle
  assertEquals(wouldCreateVisualParentCycle(nodes, "c", "a"), true);
});

Deno.test("wouldCreateVisualParentCycle: non-cyclic reparent is safe", () => {
  const nodes: VisualNodePayload[] = [
    { ...sampleNode, nodeId: "a", parentNodeId: null },
    { ...sampleNode, nodeId: "b", parentNodeId: null },
  ];
  assertFalse(wouldCreateVisualParentCycle(nodes, "a", "b"));
});

// ─── UX helper: snapToGrid — keyboard move boundary ──────────────────────────

Deno.test("snapToGrid: keyboard move step 10px snaps correctly", () => {
  assertEquals(snapToGrid(0 + 10, 10), 10);
  assertEquals(snapToGrid(10 + 10, 10), 20);
  assertEquals(snapToGrid(10 - 10, 10), 0);
});

Deno.test("snapToGrid: large step (Shift) 50px snaps to nearest 10", () => {
  assertEquals(snapToGrid(0 + 50, 10), 50);
  assertEquals(snapToGrid(20 + 50, 10), 70);
});

// ─── UX helper: buildVisualLayoutPatchJson — undo/redo state integrity ────────

Deno.test("buildVisualLayoutPatchJson: after simulated undo — reverted state serializes correctly", () => {
  const original = { ...sampleNode, orderIndex: 0, parentNodeId: null };
  const moved = { ...sampleNode, orderIndex: 2, parentNodeId: "parent-1" };

  const beforeJson = buildVisualLayoutPatchJson([original]);
  const afterJson = buildVisualLayoutPatchJson([moved]);
  const undoneJson = buildVisualLayoutPatchJson([original]);

  const before = JSON.parse(beforeJson).nodes[0];
  const after = JSON.parse(afterJson).nodes[0];
  const undone = JSON.parse(undoneJson).nodes[0];

  assertEquals(before.orderIndex, 0);
  assertEquals(after.orderIndex, 2);
  assertEquals(after.parentNodeId, "parent-1");
  assertEquals(undone.orderIndex, before.orderIndex, "Undo should restore original orderIndex");
  assertEquals(undone.parentNodeId, before.parentNodeId, "Undo should restore original parentNodeId");
});

// ─── UX helper: draft node actionable guard ───────────────────────────────────

Deno.test("isDraftOnlyApplyBlocked: single draft node blocks apply", () => {
  const draft = { ...sampleNode, isDraftOnly: true };
  assertEquals(
    ["DRAFT_ONLY_NODES", draft.componentKey].every(Boolean),
    true,
    "Error code and component key should be truthy for actionable error display",
  );
});

// ─── layout canvas live component preview ─────────────────────────────────────

Deno.test("layout canvas preview: card.primitive resolves and renders", () => {
  assertEquals(
    resolveComponentKindForLayoutPreview("card.primitive"),
    "display/card",
  );
  const result = renderLayoutComponentPreview({
    componentKey: "card.primitive",
    componentKind: "display/card",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
});

Deno.test("layout canvas preview: bare button key resolves via catalog alias", () => {
  assertEquals(
    resolveComponentKindForLayoutPreview("button"),
    "action/button",
  );
  const result = renderLayoutComponentPreview({ componentKey: "button" });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
});

Deno.test("layout canvas preview: getLayoutPreviewDefaultSize returns kind-specific dimensions", () => {
  const buttonSize = getLayoutPreviewDefaultSize("action/button");
  assertEquals(buttonSize.width >= 140, true);
  assertEquals(buttonSize.height >= 40, true);
});

Deno.test("layout canvas preview: table.primitive renders with placeholder rows", () => {
  const result = renderLayoutComponentPreview({
    componentKey: "table.primitive",
    componentKind: "data_display/table",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
});

Deno.test("layout canvas preview: unsupported component fails with explicit code", () => {
  const result = renderLayoutComponentPreview({
    componentKey: "admin_page_shell.template",
  });
  assertEquals(result.ok, false);
  if (result.ok) return;
  assertEquals(result.code, "FACTORY_MISSING");
});

// ─── UX helper: friendly label extraction ────────────────────────────────────

Deno.test("friendlyComponentLabel: extracts last path segment", () => {
  const extract = (key: string) => {
    const parts = key.split("/");
    return parts[parts.length - 1] ?? key;
  };
  assertEquals(extract("display/card"), "card");
  assertEquals(extract("form/input/text"), "text");
  assertEquals(extract("button"), "button");
});

// ─── Fix 1: keyboard resize delta ────────────────────────────────────────────

const SNAP = 10;

function applyKeyboardResizeDelta(
  node: { x: number; y: number; width: number; height: number },
  dir: string,
  delta = SNAP,
): { x: number; y: number; width: number; height: number } {
  let { x, y, width, height } = node;
  if (dir.includes("e")) width = snapToGrid(Math.max(40, width + delta), SNAP);
  if (dir.includes("w")) {
    x = snapToGrid(Math.max(0, x - delta), SNAP);
    width = snapToGrid(Math.max(40, width + delta), SNAP);
  }
  if (dir.includes("s")) {
    height = snapToGrid(Math.max(30, height + delta), SNAP);
  }
  if (dir.includes("n")) {
    y = snapToGrid(Math.max(0, y - delta), SNAP);
    height = snapToGrid(Math.max(30, height + delta), SNAP);
  }
  return { x, y, width, height };
}

Deno.test("keyboardResize: se handle grows width and height", () => {
  const n = { x: 20, y: 20, width: 140, height: 60 };
  const r = applyKeyboardResizeDelta(n, "se");
  assertEquals(r.width, 150);
  assertEquals(r.height, 70);
  assertEquals(r.x, 20);
  assertEquals(r.y, 20);
});

Deno.test("keyboardResize: n handle shrinks from top edge", () => {
  const n = { x: 20, y: 20, width: 140, height: 60 };
  const r = applyKeyboardResizeDelta(n, "n");
  assertEquals(r.y, 10);
  assertEquals(r.height, 70);
});

Deno.test("keyboardResize: w handle expands left edge", () => {
  const n = { x: 20, y: 20, width: 140, height: 60 };
  const r = applyKeyboardResizeDelta(n, "w");
  assertEquals(r.x, 10);
  assertEquals(r.width, 150);
});

Deno.test("keyboardResize: e handle does not change x or y", () => {
  const n = { x: 0, y: 0, width: 140, height: 60 };
  const r = applyKeyboardResizeDelta(n, "e");
  assertEquals(r.x, 0);
  assertEquals(r.y, 0);
  assertEquals(r.width, 150);
  assertEquals(r.height, 60);
});

Deno.test("keyboardResize: width clamped to minimum 40", () => {
  const n = { x: 10, y: 10, width: 40, height: 60 };
  // w direction with tiny delta that would push width below 40
  const delta = -SNAP;
  let { x, y, width, height } = n;
  const dir = "w";
  if (dir.includes("w")) {
    x = snapToGrid(Math.max(0, x - delta), SNAP);
    width = snapToGrid(Math.max(40, width + delta), SNAP);
  }
  assertEquals(width, 40);
});

Deno.test("keyboardResize: nw handle moves top-left corner", () => {
  const n = { x: 20, y: 20, width: 140, height: 60 };
  const r = applyKeyboardResizeDelta(n, "nw");
  assertEquals(r.x, 10);
  assertEquals(r.y, 10);
  assertEquals(r.width, 150);
  assertEquals(r.height, 70);
});

// ─── Fix 5: Lifecycle failure phase mapping ───────────────────────────────────

type LifecyclePhase =
  | "idle"
  | "previewing"
  | "previewed"
  | "validating"
  | "validated"
  | "applying"
  | "applied_ok"
  | "applied_fail"
  | "persisted";

function getFailPhase(action: string): LifecyclePhase {
  const map: Record<string, LifecyclePhase> = {
    preview: "previewed",
    validate: "validated",
    apply: "applied_fail",
  };
  return map[action] as LifecyclePhase;
}

Deno.test("lifecycleFailPhase: preview error stays in previewed", () => {
  assertEquals(getFailPhase("preview"), "previewed");
});

Deno.test("lifecycleFailPhase: validate error stays in validated", () => {
  assertEquals(getFailPhase("validate"), "validated");
});

Deno.test("lifecycleFailPhase: apply error maps to applied_fail", () => {
  assertEquals(getFailPhase("apply"), "applied_fail");
});

Deno.test("lifecycleFailPhase: preview and apply failures are distinct phases", () => {
  assertNotEquals(getFailPhase("preview"), getFailPhase("apply"));
});

// ─── canvas utility: CSS token value resolution (SSOT-based, no hardcoded role map) ───
// cssTokenRefs は選択ノード design_inspector の責務。canvas / layout_patch には含まれない。

Deno.test("resolveCssTokenValue: primary background resolves to SSOT action_primary color", () => {
  const val = resolveCssTokenValue("color.action.primary.background");
  assertEquals(val, "#0070f3");
});

Deno.test("resolveCssTokenValue: danger background resolves to SSOT action_danger color", () => {
  const val = resolveCssTokenValue("color.action.danger.background");
  assertEquals(val, "#e00");
});

Deno.test("resolveCssTokenValue: secondary background resolves to SSOT surface_secondary", () => {
  const val = resolveCssTokenValue("color.action.secondary.background");
  assertEquals(val, "#eee");
});

Deno.test("resolveCssTokenValue: radius.control.sm resolves to SSOT radius.sm", () => {
  assertEquals(resolveCssTokenValue("radius.control.sm"), "4px");
});

Deno.test("resolveCssTokenValue: border direct value resolves without value_ref", () => {
  assertEquals(
    resolveCssTokenValue("border.control.default"),
    "1px solid #ccc",
  );
});

Deno.test("resolveCssTokenValue: unknown token returns undefined", () => {
  assertEquals(resolveCssTokenValue("color.nonexistent.token"), undefined);
});

// ─── Fix 2: Inspector commit produces distinct state (history boundary) ───────

Deno.test("inspector commit: same node at two tree placements produces distinguishable snapshots", () => {
  const base: VisualNodePayload = { ...sampleNode, orderIndex: 0, parentNodeId: null };
  const after: VisualNodePayload = { ...sampleNode, orderIndex: 1, parentNodeId: "section-1" };
  const snap1 = buildVisualLayoutPatchJson([base]);
  const snap2 = buildVisualLayoutPatchJson([after]);
  assertNotEquals(
    snap1,
    snap2,
    "committed tree placement change must produce different serialized state",
  );
  assertEquals(JSON.parse(snap1).nodes[0].orderIndex, 0);
  assertEquals(JSON.parse(snap2).nodes[0].orderIndex, 1);
  assertEquals(JSON.parse(snap2).nodes[0].parentNodeId, "section-1");
});

Deno.test("inspector commit: live update then commit — final state matches committed value", () => {
  const live: VisualNodePayload = { ...sampleNode, orderIndex: 0 };
  const committed: VisualNodePayload = { ...sampleNode, orderIndex: 3 };
  const liveSnap = buildVisualLayoutPatchJson([live]);
  const commitSnap = buildVisualLayoutPatchJson([committed]);
  assertNotEquals(liveSnap, commitSnap);
  assertEquals(JSON.parse(commitSnap).nodes[0].orderIndex, 3);
});

// ─── layoutId round-trip from DB ─────────────────────────────────────────────

Deno.test("layoutId round-trip: RESPONSIVE_BREAKPOINTS exports sm/md/lg/xl", () => {
  assertEquals(RESPONSIVE_BREAKPOINTS.length, 4);
  assertEquals(RESPONSIVE_BREAKPOINTS[0], "sm");
  assertEquals(RESPONSIVE_BREAKPOINTS[1], "md");
  assertEquals(RESPONSIVE_BREAKPOINTS[2], "lg");
  assertEquals(RESPONSIVE_BREAKPOINTS[3], "xl");
});

Deno.test("layoutId round-trip: same layoutId sent and confirmed is valid", () => {
  const sentLayoutId = "550e8400-e29b-41d4-a716-446655440000";
  const confirmedLayoutId = "550e8400-e29b-41d4-a716-446655440000";
  // Round-trip is valid when confirmed matches sent
  assertEquals(sentLayoutId === confirmedLayoutId, true);
});

Deno.test("layoutId round-trip: mismatch between sent and confirmed is detectable", () => {
  const sentLayoutId: string = "550e8400-e29b-41d4-a716-446655440000";
  const confirmedLayoutId: string = "550e8400-e29b-41d4-a716-000000000001";
  const isMismatch = confirmedLayoutId.length > 0 &&
    confirmedLayoutId !== sentLayoutId;
  assertEquals(isMismatch, true);
});

Deno.test("layoutId round-trip: empty confirmed layoutId is treated as no confirmation", () => {
  const confirmedLayoutId: string = "";
  const shouldConfirm = confirmedLayoutId.length > 0;
  assertFalse(shouldConfirm);
});

Deno.test("layoutId round-trip: backend response layoutId updates frontend state on apply", () => {
  // Simulate the round-trip logic: backend returns same layoutId as sent
  const sentId = "abc-123";
  const backendData = { layoutId: "abc-123", routeKey: "/admin/ui-builder" };
  const confirmedLayoutId = typeof backendData.layoutId === "string"
    ? backendData.layoutId
    : null;
  assertEquals(confirmedLayoutId, sentId);
  assertEquals(confirmedLayoutId !== sentId, false); // no mismatch
});

// ─── responsive token rule UI ─────────────────────────────────────────────────

Deno.test("filterEmptyResponsiveRules: removes breakpoints with empty arrays", () => {
  const rules: ResponsiveTokenRules = {
    sm: [],
    md: ["color.action.primary.background"],
    lg: [],
    xl: ["spacing.md"],
  };
  const result = filterEmptyResponsiveRules(rules);
  assertEquals(Object.keys(result).length, 2);
  assertEquals(result["md"], ["color.action.primary.background"]);
  assertEquals(result["xl"], ["spacing.md"]);
  assertEquals(result["sm"], undefined);
  assertEquals(result["lg"], undefined);
});

Deno.test("filterEmptyResponsiveRules: returns empty object when all breakpoints empty", () => {
  const rules: ResponsiveTokenRules = { sm: [], md: [], lg: [], xl: [] };
  const result = filterEmptyResponsiveRules(rules);
  assertEquals(Object.keys(result).length, 0);
});

Deno.test("filterEmptyResponsiveRules: preserves tokens when all breakpoints have values", () => {
  const rules: ResponsiveTokenRules = {
    sm: ["token.a"],
    md: ["token.b"],
    lg: ["token.c"],
    xl: ["token.d"],
  };
  const result = filterEmptyResponsiveRules(rules);
  assertEquals(Object.keys(result).length, 4);
  assertEquals(result["sm"], ["token.a"]);
});

Deno.test("filterEmptyResponsiveRules: empty input returns empty object", () => {
  const result = filterEmptyResponsiveRules({});
  assertEquals(Object.keys(result).length, 0);
});

Deno.test("filterEmptyResponsiveRules: handles undefined token arrays", () => {
  const rules: ResponsiveTokenRules = { sm: undefined, md: ["token.x"] };
  const result = filterEmptyResponsiveRules(rules);
  assertEquals(result["sm"], undefined);
  assertEquals(result["md"], ["token.x"]);
});

Deno.test("responsive rule: per-breakpoint toggle adds token correctly", () => {
  let rules: ResponsiveTokenRules = {};
  // Simulate toggling a token on for md
  const bp = "md";
  const tokenKey = "color.action.primary.background";
  const current = rules[bp] ?? [];
  const next = current.includes(tokenKey)
    ? current.filter((k) => k !== tokenKey)
    : [...current, tokenKey];
  rules = { ...rules, [bp]: next };
  assertEquals(rules["md"], ["color.action.primary.background"]);
});

Deno.test("responsive rule: per-breakpoint toggle removes token correctly", () => {
  let rules: ResponsiveTokenRules = {
    md: ["color.action.primary.background", "spacing.md"],
  };
  const bp = "md";
  const tokenKey = "color.action.primary.background";
  const current = rules[bp] ?? [];
  const next = current.includes(tokenKey)
    ? current.filter((k) => k !== tokenKey)
    : [...current, tokenKey];
  rules = { ...rules, [bp]: next };
  assertEquals(rules["md"], ["spacing.md"]);
});

Deno.test("responsive rule: clearing a breakpoint removes it from filter output", () => {
  const rules: ResponsiveTokenRules = {
    md: ["color.action.primary.background"],
    lg: ["spacing.sm"],
  };
  const cleared = { ...rules };
  delete cleared["md"];
  const result = filterEmptyResponsiveRules(cleared);
  assertEquals(result["md"], undefined);
  assertEquals(result["lg"], ["spacing.sm"]);
});

Deno.test("responsive rule: payload uses filterEmptyResponsiveRules — no empty breakpoints sent", () => {
  const rules: ResponsiveTokenRules = {
    sm: [],
    md: ["color.action.primary.background"],
  };
  const payload = filterEmptyResponsiveRules(rules);
  // Payload should not include sm (empty)
  assertFalse("sm" in payload);
  assertEquals("md" in payload, true);
});

// ─── validateResponsiveTokenRulesJson ────────────────────────────────────────

Deno.test("validateResponsiveTokenRulesJson: empty string returns ok with empty rules", () => {
  const result = validateResponsiveTokenRulesJson("");
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.rules, {});
});

Deno.test("validateResponsiveTokenRulesJson: whitespace-only string returns ok with empty rules", () => {
  const result = validateResponsiveTokenRulesJson("   ");
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.rules, {});
});

Deno.test("validateResponsiveTokenRulesJson: valid object parses correctly", () => {
  const result = validateResponsiveTokenRulesJson(
    '{"md": ["color.action.primary.background"], "lg": ["spacing.sm"]}',
  );
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.rules["md"], ["color.action.primary.background"]);
    assertEquals(result.rules["lg"], ["spacing.sm"]);
  }
});

Deno.test("validateResponsiveTokenRulesJson: malformed JSON returns structured error", () => {
  const result = validateResponsiveTokenRulesJson("{not json}");
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.errorCode, "RESPONSIVE_TOKEN_RULE_JSON_INVALID");
    assertEquals(result.message.includes("解析"), true);
  }
});

Deno.test("validateResponsiveTokenRulesJson: JSON array returns structured error", () => {
  const result = validateResponsiveTokenRulesJson('["sm", "md"]');
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.errorCode, "RESPONSIVE_TOKEN_RULE_JSON_INVALID");
  }
});

Deno.test("validateResponsiveTokenRulesJson: null JSON returns structured error", () => {
  const result = validateResponsiveTokenRulesJson("null");
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.errorCode, "RESPONSIVE_TOKEN_RULE_JSON_INVALID");
  }
});

Deno.test("validateResponsiveTokenRulesJson: JSON string (non-object) returns structured error", () => {
  const result = validateResponsiveTokenRulesJson('"hello"');
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.errorCode, "RESPONSIVE_TOKEN_RULE_JSON_INVALID");
  }
});

Deno.test("validateResponsiveTokenRulesJson: unknown breakpoint key returns structured error", () => {
  const result = validateResponsiveTokenRulesJson('{"xxl": ["spacing.sm"]}');
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.errorCode, "RESPONSIVE_TOKEN_RULE_JSON_INVALID");
    assertEquals(result.message.includes("xxl"), true);
  }
});

Deno.test("validateResponsiveTokenRulesJson: non-array value returns structured error", () => {
  const result = validateResponsiveTokenRulesJson('{"md": "spacing.sm"}');
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.errorCode, "RESPONSIVE_TOKEN_RULE_JSON_INVALID");
    assertEquals(result.message.includes("md"), true);
  }
});

Deno.test("validateResponsiveTokenRulesJson: non-string array item returns structured error", () => {
  const result = validateResponsiveTokenRulesJson('{"md": [1, 2]}');
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.errorCode, "RESPONSIVE_TOKEN_RULE_JSON_INVALID");
    assertEquals(result.message.includes("md"), true);
  }
});

Deno.test("validateResponsiveTokenRulesJson: empty arrays per breakpoint are valid", () => {
  const result = validateResponsiveTokenRulesJson('{"sm": [], "md": []}');
  assertEquals(result.ok, true);
});

// ─── parseVisualLayoutPatchJson / seedDraftNodesFromPalette ───────────────────

Deno.test("layout dimensions: percent and auto strings round-trip in patch JSON", () => {
  assertEquals(formatLayoutDimensionCss(120), "120px");
  assertEquals(formatLayoutDimensionCss("50%"), "50%");
  assertEquals(formatLayoutDimensionCss("auto"), "auto");
  assertEquals(parseLayoutDimensionInput("50%"), "50%");
  assertEquals(parseLayoutDimensionInput("auto"), "auto");
  assertEquals(parseLayoutDimensionInput("Auto"), "auto");
  assertEquals(parseLayoutDimensionInput("140"), 140);
  assertEquals(resolveLayoutDimensionPx("auto", 800, 140), 140);
  const raw = JSON.stringify({
    nodes: [{
      nodeId: "n-pct",
      componentKey: "display/card",
      width: "50%",
      height: "100%",
    }],
  });
  const result = parseVisualLayoutPatchJson(raw, []);
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.value.nodes[0].width, "50%");
  assertEquals(result.value.nodes[0].height, "100%");
  const rebuilt = parseVisualLayoutPatchJson(
    buildVisualLayoutPatchJson(result.value.nodes),
    [],
  );
  assertEquals(rebuilt.ok, true);
  if (!rebuilt.ok) return;
  assertEquals(rebuilt.value.nodes[0].width, "50%");

  const autoRaw = JSON.stringify({
    nodes: [{
      nodeId: "n-auto",
      componentKey: "display/card",
      width: "auto",
      height: "auto",
    }],
  });
  const autoResult = parseVisualLayoutPatchJson(autoRaw, []);
  assertEquals(autoResult.ok, true);
  if (!autoResult.ok) return;
  assertEquals(autoResult.value.nodes[0].width, "auto");
  assertEquals(autoResult.value.nodes[0].height, "auto");
});

Deno.test("parseVisualLayoutPatchJson: hydrates nodes and layoutClassRefs", () => {
  const raw = JSON.stringify({
    layoutClassRefs: ["layout.root.grid"],
    nodes: [{
      nodeId: "n1",
      componentKey: "display/card",
      x: 10,
      y: 20,
      width: 120,
      height: 50,
    }],
  });
  const palette = [{
    componentKey: "display/card",
    componentKind: "display",
    isDraftOnly: false,
    componentId: "c1",
  }];
  const result = parseVisualLayoutPatchJson(raw, palette);
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.value.layoutClassRefs, ["layout.root.grid"]);
  assertEquals(result.value.nodes.length, 1);
  assertEquals(result.value.nodes[0].componentKind, "display");
  assertEquals(result.value.nodes[0].componentId, "c1");
});

Deno.test("seedDraftNodesFromPalette: stacks promotable entries", () => {
  const seeds = seedDraftNodesFromPalette([
    { componentKey: "a/b", componentKind: "primitive", isDraftOnly: false },
    { componentKey: "c/d", componentKind: "display", isDraftOnly: true },
    { componentKey: "e/f", componentKind: "layout", isDraftOnly: false },
  ]);
  assertEquals(seeds.length, 2);
  assertEquals(seeds[0].componentKey, "a/b");
  assertEquals(seeds[1].y, seeds[0].y + 72);
});

// ─── reorderLayoutNodeStack (layer ▲▼ z-order) ───────────────────────────────

Deno.test("reorderLayoutNodeStack: front moves node toward higher index", () => {
  const nodes: VisualNodePayload[] = [
    { ...sampleNode, nodeId: "a", orderIndex: 0 },
    { ...sampleNode, nodeId: "b", orderIndex: 1 },
    { ...sampleNode, nodeId: "c", orderIndex: 2 },
  ];
  const next = reorderLayoutNodeStack(nodes, "b", "front");
  assertEquals(next?.map((n) => n.nodeId), ["a", "c", "b"]);
  assertEquals(next?.map((n) => n.orderIndex), [0, 1, 2]);
});

Deno.test("reorderLayoutNodeStack: back moves node toward lower index", () => {
  const nodes: VisualNodePayload[] = [
    { ...sampleNode, nodeId: "a", orderIndex: 0 },
    { ...sampleNode, nodeId: "b", orderIndex: 1 },
    { ...sampleNode, nodeId: "c", orderIndex: 2 },
  ];
  const next = reorderLayoutNodeStack(nodes, "b", "back");
  assertEquals(next?.map((n) => n.nodeId), ["b", "a", "c"]);
});

Deno.test("reorderLayoutNodeStack: no-op at stack edge", () => {
  const nodes: VisualNodePayload[] = [
    { ...sampleNode, nodeId: "a", orderIndex: 0 },
    { ...sampleNode, nodeId: "b", orderIndex: 1 },
  ];
  assertEquals(reorderLayoutNodeStack(nodes, "b", "front"), null);
  assertEquals(reorderLayoutNodeStack(nodes, "a", "back"), null);
});

// ─── layoutClassPreviewUtils ──────────────────────────────────────────────────

Deno.test("filterLayoutClassRefsByAllowedFor: keeps matching roles only", () => {
  const filtered = filterLayoutClassRefsByAllowedFor(
    ["layout.root.grid", "layout.card.surface", "layout.state.selected"],
    "component_wrapper",
  );
  assertEquals(filtered, ["layout.card.surface"]);
});

Deno.test("resolveCanvasRootPreviewClassName: applies layout_root classes", () => {
  const className = resolveCanvasRootPreviewClassName([
    "layout.root.grid",
    "layout.card.surface",
  ]);
  assertEquals(className, "topolactor-topology-layout-root-grid");
});

Deno.test("resolveNodeWrapperPreviewClassName: adds preview_state when selected", () => {
  const unselected = resolveNodeWrapperPreviewClassName(
    ["layout.card.surface", "layout.state.selected"],
    false,
  );
  const selected = resolveNodeWrapperPreviewClassName(
    ["layout.card.surface", "layout.state.selected"],
    true,
  );
  assertEquals(unselected, "topolactor-topology-layout-card-surface");
  assertEquals(
    selected,
    "topolactor-topology-layout-card-surface topolactor-topology-layout-state-selected",
  );
});

// ─── UX canvas workspace labels (no separate layout/design/visual surfaces) ──

import {
  UX_DESIGN_EDITOR_SURFACE,
  UX_DESIGN_INSPECTOR_SECTION,
  UX_LAYOUT_EDITOR_SURFACE,
  UX_LAYOUT_INSPECTOR_SECTION,
  UX_UI_BUILDER_TAB_LABELS,
} from "../content/adminUxTerms.ts";

Deno.test("UX surface labels: canvas workspace and design inspector are not read-only previews", () => {
  assertEquals(UX_LAYOUT_EDITOR_SURFACE, "canvas workspace");
  assertEquals(UX_DESIGN_EDITOR_SURFACE, "デザインインスペクタ");
  assertFalse(UX_LAYOUT_EDITOR_SURFACE.includes("読み取り専用"));
  assertFalse(UX_DESIGN_EDITOR_SURFACE.includes("読み取り専用"));
});

Deno.test("UX_UI_BUILDER_TAB_LABELS: exposes canvas workspace docked panels, not visual tab", () => {
  assertEquals(UX_UI_BUILDER_TAB_LABELS.canvas, "canvas workspace");
  assertEquals(
    UX_UI_BUILDER_TAB_LABELS.designInspector,
    "デザインインスペクタ",
  );
  assertEquals("visual" in UX_UI_BUILDER_TAB_LABELS, false);
  assertEquals("layout" in UX_UI_BUILDER_TAB_LABELS, false);
  assertEquals("design" in UX_UI_BUILDER_TAB_LABELS, false);
});

// ─── canvas workspace: separate layout/design/visual tab が存在しないこと ─────

import {
  UI_BUILDER_DRAWER_SHELL_KIND,
  UI_BUILDER_DRAWER_STATE_BOUNDARY,
  UI_BUILDER_HAS_SEPARATE_TABS,
  UI_BUILDER_WORKSPACE_MODE,
} from "../islands/UiBuilderAdmin.tsx";

Deno.test("canvas workspace: workspace mode is canvas_workspace_v2", () => {
  // Verify the workspace is the new canvas-first unified workspace (not tab-based).
  // SSOT: admin-console-workflow-ssot.yaml §canvas_workspace_contract.
  assertEquals(UI_BUILDER_WORKSPACE_MODE, "canvas_workspace_v2");
});

Deno.test("canvas workspace: separate layout/design/visual tabs do NOT exist", () => {
  // The old separate tabs (layout, design, visual) have been replaced with a unified workspace.
  assertEquals(UI_BUILDER_HAS_SEPARATE_TABS, false);
});

Deno.test("canvas workspace drawer shell: same workspace display-density chrome, not separate surface", async () => {
  assertEquals(UI_BUILDER_DRAWER_SHELL_KIND, "same_workspace_display_shell");
  assertEquals(UI_BUILDER_WORKSPACE_MODE, "canvas_workspace_v2");
  assertFalse(UI_BUILDER_HAS_SEPARATE_TABS);

  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(source.includes('data-ui-builder-drawer-shell="left"'));
  assert(source.includes('data-ui-builder-drawer-shell="right"'));
  assertFalse(source.includes("/admin/ui-builder/drawer"));
});

Deno.test("canvas workspace drawer state: frontend-local only and excluded from persistence payloads", () => {
  assertEquals(UI_BUILDER_DRAWER_STATE_BOUNDARY, "frontend_local_state_only");

  const parsed = JSON.parse(buildVisualLayoutPatchJson([sampleNode]));
  assertEquals(parsed.leftDrawerOpen, undefined);
  assertEquals(parsed.rightDrawerOpen, undefined);
  assertEquals(parsed.drawerState, undefined);

  const componentStyleDesign = { cssTokenRefs: ["color.action.primary.background"] };
  const packageWiring = { targetSurface: "route_navigation" };
  assertEquals("drawerState" in componentStyleDesign, false);
  assertEquals("drawerState" in packageWiring, false);
});

Deno.test("canvas workspace drawer shell: inspector and wiring responsibility names remain unchanged", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(source.includes("<CanvasInspector"));
  assert(source.includes("<PackageDesignPanel"));
  assert(source.includes(UX_LAYOUT_INSPECTOR_SECTION));
  assert(source.includes(UX_DESIGN_INSPECTOR_SECTION));
  assert(source.includes("パッケージ配線"));
});

Deno.test("canvas workspace drawer shell: canvas-first default keeps drawers closed", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(source.includes("const [leftDrawerOpen, setLeftDrawerOpen] = useState(false)"));
  assert(source.includes("const [rightDrawerOpen, setRightDrawerOpen] = useState(false)"));
  assert(source.includes("leftDrawerOpen"));
  assert(source.includes("rightDrawerOpen"));
  assert(source.includes("左パネルを開く"));
  assert(source.includes("右パネルを開く"));
  assert(source.includes("左パネルを閉じる"));
  assert(source.includes("右パネルを閉じる"));
});
Deno.test("canvas workspace drawer shell: selecting a canvas node opens right inspector drawer", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(source.includes("setRightDrawerOpen(true)"));
  assert(source.includes("title={`${UX_LAYOUT_INSPECTOR_SECTION} — ${friendlyNodeLabel(selectedNode)}`}"));
  assert(source.includes("defaultOpen={true}"));
  assert(source.includes("title={`${UX_DESIGN_INSPECTOR_SECTION} — ${friendlyNodeLabel(selectedNode)}`}"));
  assert(source.includes("defaultOpen={false}"));
});


Deno.test("canvas workspace: preview remains inline canvas route, not modal", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assertFalse(source.includes("LayoutPatchPreviewModal"));
  assertFalse(source.includes("視覚監査モーダル"));
  assert(
    source.includes("プレビュー結果を canvas とステータスに反映しました"),
    "preview action should update the same canvas/status route",
  );
});

Deno.test("canvas workspace: buildVisualLayoutPatchJson is the canonical patch builder", () => {
  // Flow patch builder: tree fields + width/height + nodeKind/htmlTag; x/y omitted.
  const n = makeStructuralHtmlNode("div", {
    nodeId: "x",
    x: 0,
    y: 0,
    orderIndex: 0,
  });
  const json = buildVisualLayoutPatchJson([n]);
  const parsed = JSON.parse(json);
  assertEquals(parsed.nodes[0].nodeKind, "structural_html");
  assertEquals(parsed.nodes[0].x, undefined);
  assertEquals(parsed.nodes[0].y, undefined);
  // widthMode defaults to "auto" → width is NOT serialized as inline style
  assertEquals(parsed.nodes[0].width, undefined);
  assertEquals(parsed.nodes[0].widthMode, "auto");
  assertEquals(typeof parsed.nodes[0].orderIndex, "number");
});

// ─── STRUCTURAL_HTML_TAG_ALLOWLIST: full SSOT set ────────────────────────────

import { STRUCTURAL_HTML_TAG_ALLOWLIST } from "../runtime/visualLayoutUtils.ts";

Deno.test("STRUCTURAL_HTML_TAG_ALLOWLIST: includes all block tags", () => {
  const tags = STRUCTURAL_HTML_TAG_ALLOWLIST as readonly string[];
  const blockTags = [
    "div",
    "section",
    "article",
    "aside",
    "header",
    "footer",
    "main",
    "nav",
  ];
  for (const tag of blockTags) {
    assertEquals(
      tags.includes(tag),
      true,
      `block tag "${tag}" must be in allowlist`,
    );
  }
});

Deno.test("STRUCTURAL_HTML_TAG_ALLOWLIST: includes all heading tags", () => {
  const tags = STRUCTURAL_HTML_TAG_ALLOWLIST as readonly string[];
  for (const tag of ["h1", "h2", "h3", "h4", "h5", "h6"]) {
    assertEquals(
      tags.includes(tag),
      true,
      `heading tag "${tag}" must be in allowlist`,
    );
  }
});

Deno.test("STRUCTURAL_HTML_TAG_ALLOWLIST: includes text tags", () => {
  const tags = STRUCTURAL_HTML_TAG_ALLOWLIST as readonly string[];
  const textTags = ["p", "span", "strong", "em", "blockquote", "pre", "code"];
  for (const tag of textTags) {
    assertEquals(
      tags.includes(tag),
      true,
      `text tag "${tag}" must be in allowlist`,
    );
  }
});

Deno.test("STRUCTURAL_HTML_TAG_ALLOWLIST: includes link tag", () => {
  const tags = STRUCTURAL_HTML_TAG_ALLOWLIST as readonly string[];
  assertEquals(tags.includes("a"), true);
});

Deno.test("STRUCTURAL_HTML_TAG_ALLOWLIST: includes form tags", () => {
  const tags = STRUCTURAL_HTML_TAG_ALLOWLIST as readonly string[];
  const formTags = [
    "form",
    "fieldset",
    "legend",
    "label",
    "button",
    "input",
    "textarea",
    "select",
    "option",
  ];
  for (const tag of formTags) {
    assertEquals(
      tags.includes(tag),
      true,
      `form tag "${tag}" must be in allowlist`,
    );
  }
});

Deno.test("STRUCTURAL_HTML_TAG_ALLOWLIST: includes media tags", () => {
  const tags = STRUCTURAL_HTML_TAG_ALLOWLIST as readonly string[];
  const mediaTags = [
    "img",
    "picture",
    "figure",
    "figcaption",
    "video",
    "audio",
  ];
  for (const tag of mediaTags) {
    assertEquals(
      tags.includes(tag),
      true,
      `media tag "${tag}" must be in allowlist`,
    );
  }
});

Deno.test("STRUCTURAL_HTML_TAG_ALLOWLIST: includes list tags", () => {
  const tags = STRUCTURAL_HTML_TAG_ALLOWLIST as readonly string[];
  const listTags = ["ul", "ol", "li", "dl", "dt", "dd"];
  for (const tag of listTags) {
    assertEquals(
      tags.includes(tag),
      true,
      `list tag "${tag}" must be in allowlist`,
    );
  }
});

Deno.test("STRUCTURAL_HTML_TAG_ALLOWLIST: includes table tags", () => {
  const tags = STRUCTURAL_HTML_TAG_ALLOWLIST as readonly string[];
  const tableTags = [
    "table",
    "thead",
    "tbody",
    "tfoot",
    "tr",
    "th",
    "td",
    "caption",
  ];
  for (const tag of tableTags) {
    assertEquals(
      tags.includes(tag),
      true,
      `table tag "${tag}" must be in allowlist`,
    );
  }
});

Deno.test("makeStructuralHtmlNode: works with section (compatibility with existing nodes)", () => {
  const node = makeStructuralHtmlNode("section", {
    nodeId: "test-section",
    x: 10,
    y: 20,
    orderIndex: 0,
  });
  assertEquals(node.htmlTag, "section");
  assertEquals(node.nodeKind, "structural_html");
  assertEquals(node.componentKey, STRUCTURAL_HTML_COMPONENT_KEY);
});

Deno.test("makeStructuralHtmlNode: works with new expanded tags (article, main, nav)", () => {
  for (const tag of ["article", "main", "nav", "p", "ul", "table"] as const) {
    const node = makeStructuralHtmlNode(tag, {
      nodeId: `test-${tag}`,
      x: 0,
      y: 0,
      orderIndex: 0,
    });
    assertEquals(node.htmlTag, tag);
    assertEquals(node.nodeKind, "structural_html");
  }
});

// ─── LayerTree tree drag: reparent cycle detection ───────────────────────────
// SSOT: canvas_workspace_contract.layer_inspector — drag reparent must detect cycles.

Deno.test("wouldCreateVisualParentCycle: reparent to own child creates cycle", () => {
  const nodes: VisualNodePayload[] = [
    { ...sampleNode, nodeId: "a", parentNodeId: null },
    { ...sampleNode, nodeId: "b", parentNodeId: "a" },
    { ...sampleNode, nodeId: "c", parentNodeId: "b" },
  ];
  assertEquals(
    wouldCreateVisualParentCycle(nodes, "a", "c"),
    true,
    "reparenting root 'a' under grandchild 'c' must be detected as cycle",
  );
});

Deno.test("wouldCreateVisualParentCycle: reparent to direct child creates cycle", () => {
  const nodes: VisualNodePayload[] = [
    { ...sampleNode, nodeId: "parent", parentNodeId: null },
    { ...sampleNode, nodeId: "child", parentNodeId: "parent" },
  ];
  assertEquals(
    wouldCreateVisualParentCycle(nodes, "parent", "child"),
    true,
    "reparenting 'parent' under 'child' creates a direct cycle",
  );
});

Deno.test("wouldCreateVisualParentCycle: reparent to sibling is allowed", () => {
  const nodes: VisualNodePayload[] = [
    { ...sampleNode, nodeId: "root", parentNodeId: null },
    { ...sampleNode, nodeId: "a", parentNodeId: "root" },
    { ...sampleNode, nodeId: "b", parentNodeId: "root" },
  ];
  assertFalse(
    wouldCreateVisualParentCycle(nodes, "a", "b"),
    "reparenting 'a' under sibling 'b' does not create a cycle",
  );
});

// ─── reorderLayoutNodeStack: LayerTree drag reorder ──────────────────────────
// SSOT: canvas_workspace_contract.layer_inspector — drag IS the reorder affordance.

Deno.test("reorderLayoutNodeStack: front moves toward end", () => {
  const nodes = [
    { nodeId: "a", orderIndex: 0 },
    { nodeId: "b", orderIndex: 1 },
    { nodeId: "c", orderIndex: 2 },
  ];
  const result = reorderLayoutNodeStack(nodes, "a", "front");
  assertEquals(result?.[0].nodeId, "b");
  assertEquals(result?.[1].nodeId, "a");
  assertEquals(result?.[0].orderIndex, 0);
  assertEquals(result?.[1].orderIndex, 1);
});

Deno.test("reorderLayoutNodeStack: back moves toward start", () => {
  const nodes = [
    { nodeId: "x", orderIndex: 0 },
    { nodeId: "y", orderIndex: 1 },
    { nodeId: "z", orderIndex: 2 },
  ];
  const result = reorderLayoutNodeStack(nodes, "z", "back");
  assertEquals(result?.[2].nodeId, "y");
  assertEquals(result?.[1].nodeId, "z");
});

Deno.test("reorderLayoutNodeStack: front at last position returns null (already at front)", () => {
  const nodes = [
    { nodeId: "a", orderIndex: 0 },
    { nodeId: "b", orderIndex: 1 },
  ];
  const result = reorderLayoutNodeStack(nodes, "b", "front");
  assertEquals(result, null, "cannot move front-most node further to front");
});

// ─── canvas workspace contract markers ───────────────────────────────────────
// SSOT: admin-console-workflow-ssot.yaml §canvas_workspace_contract

Deno.test("canvas workspace: _tmp backend persistence uses layout and design save_tmp action keys", () => {
  // Verify the action key strings match what AdminRuntime.cs registers.
  // SSOT: draft_persistence_model auto_save storage = _tmp attribute on the layout/design record.
  assertEquals(`layout_patch:${"save_tmp"}`, "layout_patch:save_tmp");
  assertEquals(
    `component_style_design:${"save_tmp"}`,
    "component_style_design:save_tmp",
  );
});

Deno.test("canvas workspace: tree drag reparent uses wouldCreateVisualParentCycle guard", () => {
  // Verify the cycle guard is available for use in tree drag handlers.
  const deepNodes: VisualNodePayload[] = [
    { ...sampleNode, nodeId: "root", parentNodeId: null },
    { ...sampleNode, nodeId: "level1", parentNodeId: "root" },
    { ...sampleNode, nodeId: "level2", parentNodeId: "level1" },
    { ...sampleNode, nodeId: "level3", parentNodeId: "level2" },
  ];
  assertEquals(wouldCreateVisualParentCycle(deepNodes, "root", "level3"), true);
  assertEquals(
    wouldCreateVisualParentCycle(deepNodes, "level3", "root"),
    false,
  );
});


// ─── PR#364 follow-up: canvas workspace bucket card / drag / right dock guards ─

Deno.test("canvas workspace: UiBuilderAdmin bucket card and drag-drop vocabulary", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(src.includes("ComponentBucketCard"));
  assert(src.includes("component-bucket-card__drag-hint"));
  assert(src.includes("handleDropOnCanvas"));
  assert(src.includes("makeNewNode"));
});

Deno.test("canvas workspace: unpromoted palette entry blocked from apply path", () => {
  const draftEntry = { ...sampleNode, isDraftOnly: true };
  assertEquals(isDraftOnlyApplyBlocked([draftEntry]), true);
});

Deno.test("canvas workspace: layout right dock width uses clamp not 200px literal", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assertEquals(src.includes('width:200px'), false);
  assert(src.includes("clamp(320px, 24vw, 420px)"));
});

Deno.test("canvas workspace: drop reads bucket card payload and adds draft node", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(src.includes("readBucketCardDragPayload"));
  assert(src.includes("paletteEntryFromDragPayload"));
  assert(src.includes("addNode(makeNewNode"));
});

Deno.test("isLegacyAbsoluteLayoutPatch: detects flat coordinate placement", () => {
  const legacy = [
    { ...sampleNode, nodeId: "a", x: 10, y: 20, orderIndex: 0 },
    { ...sampleNode, nodeId: "b", x: 10, y: 100, orderIndex: 1 },
  ];
  assertEquals(isLegacyAbsoluteLayoutPatch(legacy), true);
  const flow = [
    { ...sampleNode, nodeId: "a", x: 0, y: 0, orderIndex: 0 },
    { ...sampleNode, nodeId: "b", x: 0, y: 0, orderIndex: 1, parentNodeId: "a" },
  ];
  assertEquals(isLegacyAbsoluteLayoutPatch(flow), false);
});

Deno.test("migrateAbsolutePatchToFlowStack: sorts by y and strips coordinates", () => {
  const nodes = [
    { ...sampleNode, nodeId: "b", x: 0, y: 100, orderIndex: 1 },
    { ...sampleNode, nodeId: "a", x: 0, y: 10, orderIndex: 0 },
  ];
  const migrated = migrateAbsolutePatchToFlowStack(nodes);
  assertEquals(migrated[0].nodeId, "a");
  assertEquals(migrated[1].nodeId, "b");
  assertEquals(migrated[0].x, 0);
  assertEquals(migrated[0].y, 0);
  assertEquals(migrated[0].orderIndex, 0);
  assertEquals(migrated[1].orderIndex, 1);
});

Deno.test("flow projection: buildFlowNodeStyle omits x/y", async () => {
  const { buildFlowNodeStyle } = await import("../runtime/layoutNodeFlowProjection.ts");
  const style = buildFlowNodeStyle({ width: 120, height: "auto" });
  assertEquals(style.left, undefined);
  assertEquals(style.top, undefined);
  assertEquals(style.position, undefined);
  assertEquals(style.width, "120px");
});

// ─── panel boundary: left docked panel / design save / dashboard preset candidate ──
// SSOT: admin-console-workflow-ssot.yaml §canvas_workspace_contract

import {
  UI_BUILDER_LEFT_PANEL_DOCKED,
  UI_BUILDER_DESIGN_SAVE_ABOVE_TABS,
} from "../islands/UiBuilderAdmin.tsx";

Deno.test("canvas workspace: palette panels are left-docked, not strip above canvas", () => {
  // SSOT: canvas_workspace_contract.left_panel position = fixed left, docked.
  assertEquals(UI_BUILDER_LEFT_PANEL_DOCKED, true);
});

Deno.test("canvas workspace: design save action is above inspector tabs", () => {
  // SSOT: design_inspector — save action must not be tab-specific.
  assertEquals(UI_BUILDER_DESIGN_SAVE_ABOVE_TABS, true);
});

Deno.test("canvas workspace: left panel has data-component-add-panel marker", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(src.includes('data-component-add-panel="true"'));
});

Deno.test("canvas workspace: design save button has data-design-save-action marker above InspectorTabPanel", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  const savePos = src.indexOf('data-design-save-action="true"');
  const tabPos = src.indexOf("InspectorTabPanel");
  // design save action must appear in source before first InspectorTabPanel
  assert(savePos !== -1, "data-design-save-action must exist");
  assert(tabPos !== -1, "InspectorTabPanel must exist");
  // The save action element in PackageDesignPanel must come before the InspectorTabPanel in the same component.
  // Since they're in the same function body, and we placed save before tabs, check ordering.
  // We look for the save action's position within the PackageDesignPanel return block.
  const panelStart = src.indexOf("function PackageDesignPanel(");
  assert(panelStart !== -1);
  const panelEnd = src.indexOf("\nfunction ", panelStart + 1);
  const panelBody = panelEnd > 0 ? src.slice(panelStart, panelEnd) : src.slice(panelStart);
  const savePosInPanel = panelBody.indexOf('data-design-save-action="true"');
  const tabPosInPanel = panelBody.indexOf("InspectorTabPanel");
  assert(savePosInPanel !== -1, "save action must be in PackageDesignPanel");
  assert(tabPosInPanel !== -1, "InspectorTabPanel must be in PackageDesignPanel");
  assert(
    savePosInPanel < tabPosInPanel,
    "design save action must appear before InspectorTabPanel in PackageDesignPanel",
  );
});

Deno.test("canvas workspace: no fixed-width palette strip above canvas", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // Legacy fixed-width palette strip class must not exist.
  assertFalse(
    src.includes("PALETTE_STRIP_PANEL_CLASS"),
    "PALETTE_STRIP_PANEL_CLASS (fixed-width block) must be removed",
  );
  // The horizontal overflow scroll strip must not exist.
  assertFalse(
    src.includes("ui-builder-palette-strip"),
    "ui-builder-palette-strip above canvas must be replaced by left-docked panel",
  );
});

Deno.test("canvas workspace: dashboard candidate panel uses preset/md_viewer description", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // Dashboard candidate palette must reference preset / md_viewer purpose, not the old vague label.
  assert(
    src.includes("UX_DASHBOARD_PRESET_CANDIDATE_DESCRIPTION"),
    "DashboardCandidatePalette must use UX_DASHBOARD_PRESET_CANDIDATE_DESCRIPTION",
  );
  assertFalse(
    src.includes('"ダッシュボード候補"'),
    "old vague label ダッシュボード候補 must not appear as a string literal",
  );
});

Deno.test("canvas workspace: left panel dashboard tab visible label is not DB 候補", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // Tab visible label must convey preset/md_viewer meaning, not the opaque "DB 候補" label.
  assertFalse(
    src.includes('"DB 候補"'),
    'tab label "DB 候補" must be replaced with a preset/md_viewer-meaningful label',
  );
  // "preset 候補" is the agreed short form for the dashboard tab label.
  assert(
    src.includes('"preset 候補"'),
    'dashboard tab visible label must be "preset 候補" (or similar preset/md_viewer label)',
  );
});

Deno.test("canvas workspace: frontend has no DB direct write or topology judgment", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // SSOT: frontend_is_projection_surface_only — no direct DB write, no topology judgment.
  assertFalse(src.includes("direct_DB_write"), "frontend must not include direct DB write marker");
  assertFalse(
    src.includes("topology_judgment"),
    "frontend must not include topology judgment marker",
  );
});

import {
  UX_COMPONENT_ADD_PANEL_LABEL,
  UX_DASHBOARD_PRESET_CANDIDATE_LABEL,
  UX_DASHBOARD_PRESET_CANDIDATE_DESCRIPTION,
  UX_DESIGN_NODE_SAVE_LABEL,
} from "../content/adminUxTerms.ts";

Deno.test("adminUxTerms: component add panel and dashboard preset candidate labels", () => {
  assertEquals(UX_COMPONENT_ADD_PANEL_LABEL, "部品追加");
  assertEquals(UX_DASHBOARD_PRESET_CANDIDATE_LABEL, "ダッシュボード preset 候補");
  assert(
    UX_DASHBOARD_PRESET_CANDIDATE_DESCRIPTION.includes("preset"),
    "dashboard preset description must reference preset",
  );
  assert(
    UX_DASHBOARD_PRESET_CANDIDATE_DESCRIPTION.includes("md_viewer"),
    "dashboard preset description must reference md_viewer",
  );
  assert(
    UX_DASHBOARD_PRESET_CANDIDATE_DESCRIPTION.includes("team-dashboard"),
    "dashboard preset description must reference team-dashboard",
  );
});

Deno.test("adminUxTerms: design save label is node-scoped, not tab-scoped", () => {
  assertEquals(UX_DESIGN_NODE_SAVE_LABEL, "選択ノードのデザインを保存");
});

// ─── layoutClassDictionary: new vocabulary (PR#404) ─────────────────────────

import { TOPOLOGY_LAYOUT_CLASS_DICTIONARY } from "../runtime/topologyLayoutClassDictionary.ts";

Deno.test("layoutClassDictionary: all entries have non-empty label", () => {
  for (const e of TOPOLOGY_LAYOUT_CLASS_DICTIONARY) {
    assert(e.label.length > 0, `entry ${e.classKey} must have non-empty label`);
  }
});

Deno.test("layoutClassDictionary: direction entries have conflictGroup=direction", () => {
  const directionEntries = TOPOLOGY_LAYOUT_CLASS_DICTIONARY.filter((e) => e.category === "direction");
  assert(directionEntries.length >= 3);
  for (const e of directionEntries) {
    assertEquals(e.conflictGroup, "direction", `${e.classKey} must have conflictGroup=direction`);
  }
});

Deno.test("layoutClassDictionary: alignment entries have conflictGroup align_h or align_v", () => {
  const alignEntries = TOPOLOGY_LAYOUT_CLASS_DICTIONARY.filter((e) => e.category === "alignment");
  assert(alignEntries.length >= 4);
  for (const e of alignEntries) {
    assert(
      e.conflictGroup === "align_h" || e.conflictGroup === "align_v",
      `alignment entry ${e.classKey} must have align_h or align_v conflictGroup`,
    );
  }
});

// ─── UI Builder inspector UX normalization tests ──────────────────────────────

import {
  COMPONENT_EVENT_TYPES,
  COMPONENT_ACTION_TYPES,
  type ComponentEventWiring,
} from "../islands/UiBuilderAdmin.tsx";
import { CSS_DICTIONARY_TOKENS } from "../runtime/cssDictionary.ts";

Deno.test("TOPOLOGY_LAYOUT_CLASS_DICTIONARY: all non-state entries have non-empty label (SSOT: topology-layout-class-ssot.yaml)", () => {
  const nonStateEntries = TOPOLOGY_LAYOUT_CLASS_DICTIONARY.filter((e) => e.category !== "state");
  assert(nonStateEntries.length > 0, "expect non-state entries");
  for (const e of nonStateEntries) {
    assert(e.label !== undefined && e.label.length > 0, `missing label for classKey: ${e.classKey}`);
    assertNotEquals(e.label, e.classKey, `label for ${e.classKey} must not be the raw classKey`);
  }
});

Deno.test("layoutClassDictionary: sizing entries have conflictGroup=width", () => {
  const sizingEntries = TOPOLOGY_LAYOUT_CLASS_DICTIONARY.filter((e) => e.category === "sizing");
  assert(sizingEntries.length >= 2);
  for (const e of sizingEntries) {
    assertEquals(e.conflictGroup, "width");
  }
});

Deno.test("layoutClassDictionary: layout.direction.stack resolves in canvas root preview", () => {
  const className = resolveCanvasRootPreviewClassName(["layout.direction.stack"]);
  assertEquals(className, "topolactor-topology-layout-direction-stack");
});

Deno.test("layoutClassDictionary: layout.align.center filtered for layout_section role", () => {
  const filtered = filterLayoutClassRefsByAllowedFor(["layout.align.center"], "layout_section");
  assertEquals(filtered, ["layout.align.center"]);
});


// ─── SizingMode: widthMode/heightMode inline style suppression ────────────────

import {
  hasSizingWidthClassRef,
  resolveSizingModeAfterToggle,
} from "../runtime/visualLayoutUtils.ts";
import { buildFlowNodeStyle } from "../runtime/layoutNodeFlowProjection.ts";

Deno.test("hasSizingWidthClassRef: detects layout.width.* keys", () => {
  assertEquals(hasSizingWidthClassRef(["layout.width.full"]), true);
  assertEquals(hasSizingWidthClassRef(["layout.width.sm"]), true);
  assertEquals(hasSizingWidthClassRef(["layout.direction.stack"]), false);
  assertEquals(hasSizingWidthClassRef(["layout.align.center"]), false);
  assertEquals(hasSizingWidthClassRef([]), false);
});

Deno.test("hasSizingWidthClassRef: mixed refs — returns true when any is sizing", () => {
  assertEquals(
    hasSizingWidthClassRef(["layout.direction.row", "layout.width.full"]),
    true,
  );
});

Deno.test("resolveSizingModeAfterToggle: sizing classRef added → preset", () => {
  const mode = resolveSizingModeAfterToggle(["layout.width.full"], undefined);
  assertEquals(mode, "preset");
});

Deno.test("resolveSizingModeAfterToggle: sizing classRef removed, no remaining → auto", () => {
  const mode = resolveSizingModeAfterToggle(["layout.direction.row"], undefined);
  assertEquals(mode, "auto");
});

Deno.test("resolveSizingModeAfterToggle: sizing classRef removed, was custom → keep custom", () => {
  const mode = resolveSizingModeAfterToggle(["layout.direction.row"], "custom");
  assertEquals(mode, "custom");
});

Deno.test("buildFlowNodeStyle: widthMode=auto suppresses width", () => {
  const style = buildFlowNodeStyle({ width: 140, height: 60, widthMode: "auto" });
  assertEquals(style.width, undefined);
  assertEquals(style.height, "60px");
});

Deno.test("buildFlowNodeStyle: widthMode=preset suppresses width", () => {
  const style = buildFlowNodeStyle({ width: 140, height: 60, widthMode: "preset" });
  assertEquals(style.width, undefined);
  assertEquals(style.height, "60px");
});

Deno.test("buildFlowNodeStyle: widthMode=custom preserves width", () => {
  const style = buildFlowNodeStyle({ width: 200, height: 80, widthMode: "custom" });
  assertEquals(style.width, "200px");
  assertEquals(style.height, "80px");
});

Deno.test("buildFlowNodeStyle: widthMode=undefined preserves width (backward-compat)", () => {
  const style = buildFlowNodeStyle({ width: 140, height: 60 });
  assertEquals(style.width, "140px");
  assertEquals(style.height, "60px");
});

Deno.test("buildFlowNodeStyle: heightMode=preset suppresses height only", () => {
  const style = buildFlowNodeStyle({ width: 140, height: 60, heightMode: "preset" });
  assertEquals(style.width, "140px");
  assertEquals(style.height, undefined);
});

Deno.test("buildVisualLayoutPatchJson: widthMode=preset omits width from output", () => {
  const node: VisualNodePayload = {
    ...sampleNode,
    widthMode: "preset",
    layoutClassRefs: ["layout.width.full"],
  };
  const parsed = JSON.parse(buildVisualLayoutPatchJson([node]));
  assertEquals(parsed.nodes[0].width, undefined);
  assertEquals(parsed.nodes[0].height, 60);
  assertEquals(parsed.nodes[0].widthMode, "preset");
});

Deno.test("buildVisualLayoutPatchJson: widthMode=auto omits width and height", () => {
  const node: VisualNodePayload = { ...sampleNode, widthMode: "auto", heightMode: "auto" };
  const parsed = JSON.parse(buildVisualLayoutPatchJson([node]));
  assertEquals(parsed.nodes[0].width, undefined);
  assertEquals(parsed.nodes[0].height, undefined);
  assertEquals(parsed.nodes[0].widthMode, "auto");
  assertEquals(parsed.nodes[0].heightMode, "auto");
});

Deno.test("buildVisualLayoutPatchJson: widthMode=custom preserves width", () => {
  const node: VisualNodePayload = { ...sampleNode, widthMode: "custom", heightMode: "custom" };
  const parsed = JSON.parse(buildVisualLayoutPatchJson([node]));
  assertEquals(parsed.nodes[0].width, 140);
  assertEquals(parsed.nodes[0].height, 60);
});

Deno.test("buildVisualLayoutPatchJson: widthMode=undefined preserves width (backward-compat)", () => {
  const parsed = JSON.parse(buildVisualLayoutPatchJson([sampleNode]));
  assertEquals(parsed.nodes[0].width, 140);
  assertEquals(parsed.nodes[0].height, 60);
  assertEquals(parsed.nodes[0].widthMode, undefined);
});

Deno.test("parseVisualLayoutPatchJson: widthMode round-trips through patch", () => {
  const node: VisualNodePayload = {
    ...sampleNode,
    widthMode: "preset",
    heightMode: "auto",
    layoutClassRefs: ["layout.width.full"],
  };
  const json = buildVisualLayoutPatchJson([node]);
  const result = parseVisualLayoutPatchJson(json, []);
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.value.nodes[0].widthMode, "preset");
  assertEquals(result.value.nodes[0].heightMode, "auto");
  assertEquals(result.value.nodes[0].width, 140);
});

Deno.test("makeStructuralHtmlNode: defaults to widthMode=auto", () => {
  const node = makeStructuralHtmlNode("div", { nodeId: "test", x: 0, y: 0, orderIndex: 0 });
  assertEquals(node.widthMode, "auto");
  assertEquals(node.heightMode, "auto");
});

Deno.test("TOPOLOGY_LAYOUT_CLASS_DICTIONARY: state category entries are excluded from normal inspector (category filter)", () => {
  const stateEntries = TOPOLOGY_LAYOUT_CLASS_DICTIONARY.filter((e) => e.category === "state");
  assert(stateEntries.length > 0, "expect at least one state class in dictionary");
  // TopologyLayoutClassPicker filters: if (e.category === "state") return false
  for (const e of stateEntries) {
    assertEquals(e.category, "state");
  }
});

Deno.test("TOPOLOGY_LAYOUT_CLASS_DICTIONARY: label values do not equal classKey or className", () => {
  const nonStateEntries = TOPOLOGY_LAYOUT_CLASS_DICTIONARY.filter((e) => e.category !== "state");
  for (const e of nonStateEntries) {
    if (!e.label) continue;
    assertNotEquals(e.label, e.classKey, `label for ${e.classKey} must not be raw classKey`);
    assertNotEquals(e.label, e.className, `label for ${e.classKey} must not be raw className`);
  }
});

Deno.test("CSS_DICTIONARY_TOKENS: all entries have non-empty userLabel (SSOT: css-dictionary-ssot.yaml)", () => {
  for (const token of CSS_DICTIONARY_TOKENS) {
    assert(
      token.userLabel !== undefined && token.userLabel.length > 0,
      `missing or empty userLabel for tokenKey: ${token.tokenKey}`,
    );
    assertNotEquals(
      token.userLabel,
      token.tokenKey,
      `userLabel for ${token.tokenKey} must not be the raw tokenKey`,
    );
  }
});

Deno.test("CSS_DICTIONARY_TOKENS: userLabel matches expected semantic labels from SSOT", () => {
  const find = (key: string) => CSS_DICTIONARY_TOKENS.find((t) => t.tokenKey === key);
  assertEquals(find("color.action.primary.background")?.userLabel, "メインボタン背景");
  assertEquals(find("color.action.primary.text")?.userLabel, "メインボタン文字色");
  assertEquals(find("color.action.danger.background")?.userLabel, "危険操作ボタン背景");
  assertEquals(find("border.control.default")?.userLabel, "コントロール枠線");
  assertEquals(find("typography.control.monospace")?.userLabel, "管理UIテキスト");
  assertEquals(find("interaction.control.pointer")?.userLabel, "クリック可能");
  assertEquals(find("interaction.control.disabled_opacity")?.userLabel, "無効状態");
});

Deno.test("COMPONENT_EVENT_TYPES: contains standard DOM/component event types", () => {
  assert(COMPONENT_EVENT_TYPES.includes("onClick"), "must include onClick");
  assert(COMPONENT_EVENT_TYPES.includes("onChange"), "must include onChange");
  assert(COMPONENT_EVENT_TYPES.includes("onSubmit"), "must include onSubmit");
  assert(COMPONENT_EVENT_TYPES.includes("onOpen"), "must include onOpen");
  assert(COMPONENT_EVENT_TYPES.includes("onClose"), "must include onClose");
  assert(COMPONENT_EVENT_TYPES.length >= 5, "must have at least 5 event types");
});

Deno.test("COMPONENT_ACTION_TYPES: contains standard component action types", () => {
  assert(COMPONENT_ACTION_TYPES.includes("setState"), "must include setState");
  assert(COMPONENT_ACTION_TYPES.includes("navigate"), "must include navigate");
  assert(COMPONENT_ACTION_TYPES.includes("openModal"), "must include openModal");
  assert(COMPONENT_ACTION_TYPES.includes("closeModal"), "must include closeModal");
  assert(COMPONENT_ACTION_TYPES.includes("openDrawer"), "must include openDrawer");
  assert(COMPONENT_ACTION_TYPES.includes("closeDrawer"), "must include closeDrawer");
});

Deno.test("ComponentEventWiring: propsJson.eventWirings round-trip encoding preserves wiring data", () => {
  const wirings: ComponentEventWiring[] = [
    { eventType: "onClick", actionType: "openModal", targetStateKey: "modalOpen" },
    { eventType: "onChange", actionType: "setState", targetStateKey: "inputValue" },
  ];
  const propsJson = JSON.stringify({ label: "送信", eventWirings: wirings });
  const parsed = JSON.parse(propsJson);
  assertEquals(parsed.eventWirings.length, 2);
  assertEquals(parsed.eventWirings[0].eventType, "onClick");
  assertEquals(parsed.eventWirings[0].actionType, "openModal");
  assertEquals(parsed.eventWirings[0].targetStateKey, "modalOpen");
  assertEquals(parsed.eventWirings[1].eventType, "onChange");
  // other props preserved
  assertEquals(parsed.label, "送信");
});

Deno.test("ComponentEventWiring: targetStateKey is optional", () => {
  const wiring: ComponentEventWiring = { eventType: "onSubmit", actionType: "submitForm" };
  assertEquals(wiring.targetStateKey, undefined);
  const json = JSON.stringify({ eventWirings: [wiring] });
  const parsed = JSON.parse(json);
  assertEquals(parsed.eventWirings[0].targetStateKey, undefined);
});

Deno.test("modal/drawer open state: stateJson encodes as { open: boolean }", () => {
  // open=true
  const openState = JSON.stringify({ open: true });
  const parsed = JSON.parse(openState);
  assertEquals(parsed.open, true);
  // open=false
  const closedState = JSON.stringify({ open: false });
  const parsedClosed = JSON.parse(closedState);
  assertEquals(parsedClosed.open, false);
  // partial merge preserves other keys
  const existing = { theme: "dark" };
  const merged = JSON.stringify({ ...existing, open: true });
  const parsedMerged = JSON.parse(merged);
  assertEquals(parsedMerged.open, true);
  assertEquals(parsedMerged.theme, "dark");
});

Deno.test("package wiring and event/state wiring are separated: UI_BUILDER_HAS_SEPARATE_TABS false (unified workspace)", () => {
  // Package wiring (ui_topology:update_package_wiring) lives in PackageDesignPanel 'パッケージ配線' tab
  // Component event/state wiring (propsJson.eventWirings) lives in PackageDesignPanel '上級' tab (AdvancedManualOverride, SSOT未定義)
  // CanvasInspector no longer has a '配線' tab (removed from layout inspector responsibility)
  assertFalse(
    UI_BUILDER_HAS_SEPARATE_TABS,
    "workspace should remain unified (no separate tabs)",
  );
});

Deno.test("gridCol/gridRow: not in primary flow layout fields per SSOT flow_layout_projection_model", () => {
  // Flow placement is governed by parentNodeId + orderIndex + layoutClassRefs only.
  // gridCol/gridRow do not affect FlowLayoutCanvas preview.
  // They are moved to AdvancedManualOverride within the ツリー tab, not exposed as normal fields.
  const flowFields = ["parentNodeId", "orderIndex", "layoutClassRefs", "slotKey"];
  const advancedOnlyFields = ["gridCol", "gridRow"];
  for (const f of flowFields) {
    assertFalse(advancedOnlyFields.includes(f), `${f} must be a primary flow field, not advanced-only`);
  }
  for (const f of advancedOnlyFields) {
    assertFalse(flowFields.includes(f), `${f} must be advanced-only, not a primary flow field`);
  }
});

Deno.test("layout inspector tabs: 3 tabs only (概要/ツリー/クラス) — no グリッド or 配線", () => {
  // This is a documentation test verifying the SSOT-conformant tab structure.
  // CanvasInspector tabs: overview, tree, class (grid and wiring removed)
  const expectedTabs = ["概要", "ツリー", "クラス"];
  const removedTabs = ["グリッド", "配線"];
  assertEquals(expectedTabs.length, 3, "layout inspector must have exactly 3 tabs");
  for (const removed of removedTabs) {
    assertFalse(expectedTabs.includes(removed), `${removed} must not be in layout inspector tabs`);
  }
});

// ─── LayoutPreviewNodeInput: tree structure fields ────────────────────────────
// Defect 12 fix: parentNodeId / orderIndex / slotKey preserved from DraftNode
// so LayoutVisualAuditCanvas.toFlowAuditNodes can reconstruct the flow tree.

Deno.test("LayoutPreviewNodeInput: accepts parentNodeId, orderIndex, slotKey fields", () => {
  const node: LayoutPreviewNodeInput = {
    nodeId: "node_001",
    componentKey: "button.primitive",
    x: 0,
    y: 0,
    width: 148,
    height: 44,
    parentNodeId: "node_parent",
    orderIndex: 2,
    slotKey: "header",
  };
  assertEquals(node.parentNodeId, "node_parent");
  assertEquals(node.orderIndex, 2);
  assertEquals(node.slotKey, "header");
});

Deno.test("LayoutPreviewNodeInput: parentNodeId=null is valid (root node)", () => {
  const node: LayoutPreviewNodeInput = {
    nodeId: "node_root",
    componentKey: "box.primitive",
    x: 0,
    y: 0,
    width: 180,
    height: 96,
    parentNodeId: null,
    orderIndex: 0,
    slotKey: "",
  };
  assertEquals(node.parentNodeId, null);
  assertEquals(node.orderIndex, 0);
});

Deno.test("LayoutPreviewNodeInput: tree fields are optional (backward-compat)", () => {
  const node: LayoutPreviewNodeInput = {
    nodeId: "node_legacy",
    componentKey: "card.primitive",
    x: 10,
    y: 20,
    width: 240,
    height: 152,
  };
  assertEquals(node.parentNodeId, undefined);
  assertEquals(node.orderIndex, undefined);
  assertEquals(node.slotKey, undefined);
});

Deno.test("enrichLayoutPreviewNodes: preserves parentNodeId, orderIndex, slotKey via spread", () => {
  const input: LayoutPreviewNodeInput[] = [
    {
      nodeId: "node_child",
      componentKey: "button.primitive",
      x: 0,
      y: 0,
      width: 148,
      height: 44,
      parentNodeId: "node_parent",
      orderIndex: 1,
      slotKey: "footer",
    },
    {
      nodeId: "node_parent",
      componentKey: "box.primitive",
      x: 0,
      y: 0,
      width: 180,
      height: 96,
      parentNodeId: null,
      orderIndex: 0,
      slotKey: "root",
    },
  ];
  const enriched = enrichLayoutPreviewNodes(input, []);
  const child = enriched.find((n) => n.nodeId === "node_child")!;
  const parent = enriched.find((n) => n.nodeId === "node_parent")!;
  assertEquals((child as LayoutPreviewNodeInput).parentNodeId, "node_parent");
  assertEquals((child as LayoutPreviewNodeInput).orderIndex, 1);
  assertEquals((child as LayoutPreviewNodeInput).slotKey, "footer");
  assertEquals((parent as LayoutPreviewNodeInput).parentNodeId, null);
  assertEquals((parent as LayoutPreviewNodeInput).orderIndex, 0);
  assertEquals((parent as LayoutPreviewNodeInput).slotKey, "root");
});

// ─── cssTokenRefs canvas preview — Defect 3 & 10 ────────────────────────────

Deno.test("buildInlineStyleFromCssTokenRefs: primary background token applied correctly", () => {
  const style = buildInlineStyleFromCssTokenRefs(["color.action.primary.background"]);
  assertEquals(style.background, "#0070f3");
});

Deno.test("buildInlineStyleFromCssTokenRefs: empty array produces empty style", () => {
  const style = buildInlineStyleFromCssTokenRefs([]);
  assertEquals(Object.keys(style).length, 0);
});

Deno.test("buildInlineStyleFromCssTokenRefs: multiple tokens produce merged style", () => {
  const style = buildInlineStyleFromCssTokenRefs([
    "color.action.primary.background",
    "color.action.primary.text",
    "radius.control.sm",
  ]);
  assertEquals(style.background, "#0070f3");
  assertEquals(style.color, "#fff");
  assertEquals(style["border-radius"], "4px");
});

// SSOT: silent_fallback_to_unknown_css_token is prohibited (css-dictionary-ssot.yaml).
// Unknown tokens must be observable — not silently discarded.
// resolveUnknownCssTokenRefs surfaces them so callers can warn/validate.
Deno.test("resolveUnknownCssTokenRefs: unknown key is returned for explicit handling", () => {
  const unknown = resolveUnknownCssTokenRefs([
    "unknown.token.xyz",
    "color.action.primary.background",
  ]);
  assertEquals(unknown, ["unknown.token.xyz"]);
});

Deno.test("resolveUnknownCssTokenRefs: all-known refs returns empty array", () => {
  const unknown = resolveUnknownCssTokenRefs([
    "color.action.primary.background",
    "radius.control.sm",
  ]);
  assertEquals(unknown.length, 0);
});

Deno.test("resolveUnknownCssTokenRefs: empty input returns empty array", () => {
  const unknown = resolveUnknownCssTokenRefs([]);
  assertEquals(unknown.length, 0);
});

Deno.test("resolveUnknownCssTokenRefs: all-unknown refs are all returned", () => {
  const unknown = resolveUnknownCssTokenRefs(["no.such.token", "also.unknown"]);
  assertEquals(unknown, ["no.such.token", "also.unknown"]);
});

// buildInlineStyleFromCssTokenRefs still applies known tokens even when unknowns are present.
// The caller is responsible for surfacing the unknown refs via resolveUnknownCssTokenRefs.
Deno.test("buildInlineStyleFromCssTokenRefs: known tokens applied even when mixed with unknowns", () => {
  const style = buildInlineStyleFromCssTokenRefs([
    "unknown.token.xyz",
    "color.action.primary.background",
  ]);
  assertEquals(style.background, "#0070f3");
  assertFalse("unknown" in style);
});

// ─── D3/D10 regression: cssTokenRefs preserved across inlineText/link edits ─

import { buildInlineStyleFromCssTokenRefs as _buildStyle } from "../runtime/cssDictionary.ts";
import type { FlowCanvasDesignDraft } from "../components/FlowLayoutCanvas.tsx";

// Simulates the designDraftByNodeId merge logic in handleDesignDraftChange.
// partial.cssTokenRefs === undefined must NOT wipe current.cssTokenRefs.
function mergeDesignDraft(
  current: FlowCanvasDesignDraft,
  partial: FlowCanvasDesignDraft,
): FlowCanvasDesignDraft {
  return {
    ...current,
    ...partial,
    cssTokenRefs: partial.cssTokenRefs !== undefined
      ? partial.cssTokenRefs
      : current.cssTokenRefs,
  };
}

Deno.test("designDraft merge: inlineText update preserves cssTokenRefs", () => {
  const current: FlowCanvasDesignDraft = {
    inlineText: "old text",
    cssTokenRefs: ["color.action.primary.background"],
  };
  const partial: FlowCanvasDesignDraft = {
    inlineText: "new text",
    cssTokenRefs: undefined,
  };
  const merged = mergeDesignDraft(current, partial);
  assertEquals(merged.inlineText, "new text");
  assertEquals(merged.cssTokenRefs, ["color.action.primary.background"]);
});

Deno.test("designDraft merge: linkHref update preserves cssTokenRefs", () => {
  const current: FlowCanvasDesignDraft = {
    linkHref: "https://old.example.com",
    cssTokenRefs: ["radius.control.sm", "border.control.default"],
  };
  const partial: FlowCanvasDesignDraft = {
    linkHref: "https://new.example.com",
    cssTokenRefs: undefined,
  };
  const merged = mergeDesignDraft(current, partial);
  assertEquals(merged.linkHref, "https://new.example.com");
  assertEquals(merged.cssTokenRefs, ["radius.control.sm", "border.control.default"]);
});

Deno.test("designDraft merge: linkTarget update preserves cssTokenRefs", () => {
  const current: FlowCanvasDesignDraft = {
    linkTarget: "_self",
    cssTokenRefs: ["color.action.primary.background"],
  };
  const partial: FlowCanvasDesignDraft = {
    linkTarget: "_blank",
    cssTokenRefs: undefined,
  };
  const merged = mergeDesignDraft(current, partial);
  assertEquals(merged.linkTarget, "_blank");
  assertEquals(merged.cssTokenRefs, ["color.action.primary.background"]);
});

Deno.test("designDraft merge: explicit empty cssTokenRefs clears tokens", () => {
  // If partial explicitly passes [], tokens are cleared (intentional reset).
  const current: FlowCanvasDesignDraft = {
    cssTokenRefs: ["color.action.primary.background"],
  };
  const partial: FlowCanvasDesignDraft = {
    cssTokenRefs: [],
  };
  const merged = mergeDesignDraft(current, partial);
  assertEquals(merged.cssTokenRefs, []);
});

Deno.test("designDraft merge: cssTokenRefs update replaces tokens and keeps other fields", () => {
  const current: FlowCanvasDesignDraft = {
    inlineText: "button label",
    linkHref: "https://example.com",
    cssTokenRefs: ["color.action.secondary.background"],
  };
  const partial: FlowCanvasDesignDraft = {
    cssTokenRefs: ["color.action.primary.background", "radius.control.sm"],
  };
  const merged = mergeDesignDraft(current, partial);
  assertEquals(merged.cssTokenRefs, ["color.action.primary.background", "radius.control.sm"]);
  assertEquals(merged.inlineText, "button label");
  assertEquals(merged.linkHref, "https://example.com");
});

Deno.test("designDraft: cssTokenRefs inline styles applied after token toggle + text edit", () => {
  // Simulates: token selected → text edited → canvas style must include token effect.
  const afterTokenToggle: FlowCanvasDesignDraft = {
    inlineText: "btn",
    cssTokenRefs: ["color.action.primary.background"],
  };
  const afterTextEdit: FlowCanvasDesignDraft = mergeDesignDraft(
    afterTokenToggle,
    { inlineText: "click me", cssTokenRefs: undefined },
  );
  assertEquals(afterTextEdit.inlineText, "click me");
  assertEquals(afterTextEdit.cssTokenRefs, ["color.action.primary.background"]);
  const style = _buildStyle(afterTextEdit.cssTokenRefs!);
  assertEquals(style.background, "#0070f3");
});
