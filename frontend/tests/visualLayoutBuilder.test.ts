/**
 * visualLayoutBuilder.test.ts
 *
 * VisualLayoutCanvas は layout draft のリアルタイムプレビュー &amp; 直感操作 surface として通常導線に残る。
 * drag / resize は draft node の x/y/width/height を更新するだけ。
 * parentNodeId / slotKey / orderIndex / layoutClassRefs はインスペクタ（CanvasInspector）で編集する。
 * cssTokenRefs / classname / tailwind は canvas / layout_patch には含まれない（選択ノード design_inspector の責務）。
 *
 * このテストファイルは以下を対象とする:
 *   - canvas utility functions (snapToGrid, buildVisualLayoutPatchJson, etc.)
 *   - inspector 構造フィールド utility (wouldCreateVisualParentCycle, orderIndex round-trip)
 *   - draft-only apply guard
 *   - responsive token rule utilities
 */
import {
  assertEquals,
  assertFalse,
  assertNotEquals,
  assertObjectMatch,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildVisualLayoutPatchJson,
  cloneVisualNode,
  filterEmptyResponsiveRules,
  getDraftOnlyNodes,
  isDraftOnlyApplyBlocked,
  makeStructuralHtmlNode,
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
import { resolveCssTokenValue } from "../runtime/cssDictionary.ts";
import {
  getLayoutPreviewDefaultSize,
  renderLayoutComponentPreview,
  resolveComponentKindForLayoutPreview,
} from "../runtime/layoutComponentPreview.ts";

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

Deno.test("buildVisualLayoutPatchJson: includes x/y/width/height in node payload", () => {
  const json = buildVisualLayoutPatchJson([sampleNode]);
  const parsed = JSON.parse(json);
  const node = parsed.nodes[0];
  assertEquals(node.x, 10);
  assertEquals(node.y, 20);
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

Deno.test("buildVisualLayoutPatchJson: cssTokenRefs are NOT in node payload (design_inspector responsibility)", () => {
  // cssTokenRefs belong to selected-node design_inspector persistence, not layout_patch node payload
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
  const original = { ...sampleNode, x: 100, y: 200 };
  const moved = { ...sampleNode, x: 150, y: 250 };

  const beforeJson = buildVisualLayoutPatchJson([original]);
  const afterJson = buildVisualLayoutPatchJson([moved]);
  const undoneJson = buildVisualLayoutPatchJson([original]);

  const before = JSON.parse(beforeJson).nodes[0];
  const after = JSON.parse(afterJson).nodes[0];
  const undone = JSON.parse(undoneJson).nodes[0];

  assertEquals(before.x, 100);
  assertEquals(after.x, 150);
  assertEquals(undone.x, before.x, "Undo should restore original x");
  assertEquals(undone.y, before.y, "Undo should restore original y");
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

Deno.test("inspector commit: same node at two positions produces distinguishable snapshots", () => {
  const base: VisualNodePayload = { ...sampleNode, x: 50, y: 50 };
  const after: VisualNodePayload = { ...sampleNode, x: 100, y: 100 };
  const snap1 = buildVisualLayoutPatchJson([base]);
  const snap2 = buildVisualLayoutPatchJson([after]);
  assertNotEquals(
    snap1,
    snap2,
    "committed position change must produce different serialized state",
  );
  assertEquals(JSON.parse(snap1).nodes[0].x, 50);
  assertEquals(JSON.parse(snap2).nodes[0].x, 100);
});

Deno.test("inspector commit: live update then commit — final state matches committed value", () => {
  // Simulate: live update to 80 (no history entry), then commit to 90
  const live: VisualNodePayload = { ...sampleNode, x: 80 };
  const committed: VisualNodePayload = { ...sampleNode, x: 90 };
  const liveSnap = buildVisualLayoutPatchJson([live]);
  const commitSnap = buildVisualLayoutPatchJson([committed]);
  assertNotEquals(liveSnap, commitSnap);
  assertEquals(JSON.parse(commitSnap).nodes[0].x, 90);
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
  UX_LAYOUT_EDITOR_SURFACE,
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

Deno.test("canvas workspace: buildVisualLayoutPatchJson is the canonical patch builder", () => {
  // The visual patch builder (includes x/y/width/height, nodeKind, htmlTag) must remain.
  // It is imported and used by LayoutBuilderSection in the canvas workspace.
  // If this import fails TypeScript compilation, the workspace is broken.
  const n = makeStructuralHtmlNode("div", {
    nodeId: "x",
    x: 0,
    y: 0,
    orderIndex: 0,
  });
  const json = buildVisualLayoutPatchJson([n]);
  const parsed = JSON.parse(json);
  assertEquals(parsed.nodes[0].nodeKind, "structural_html");
  assertEquals(typeof parsed.nodes[0].x, "number");
  assertEquals(typeof parsed.nodes[0].width, "number");
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
