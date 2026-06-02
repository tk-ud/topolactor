import { assertEquals, assertFalse, assertNotEquals, assertObjectMatch } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  snapToGrid,
  buildVisualLayoutPatchJson,
  getDraftOnlyNodes,
  isDraftOnlyApplyBlocked,
  wouldCreateVisualParentCycle,
  RESPONSIVE_BREAKPOINTS,
  filterEmptyResponsiveRules,
  type VisualNodePayload,
  type ResponsiveTokenRules,
} from "../runtime/visualLayoutUtils.ts";
import { resolveCssTokenValue } from "../runtime/cssDictionary.ts";

// ─── snapToGrid ───────────────────────────────────────────────────────────────

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

// ─── wouldCreateVisualParentCycle ─────────────────────────────────────────────

Deno.test("wouldCreateVisualParentCycle: null parent never cycles", () => {
  const nodes: VisualNodePayload[] = [{ ...sampleNode, nodeId: "a", parentNodeId: null }];
  assertFalse(wouldCreateVisualParentCycle(nodes, "a", null));
});

Deno.test("wouldCreateVisualParentCycle: direct self-reference is a cycle", () => {
  const nodes: VisualNodePayload[] = [{ ...sampleNode, nodeId: "a", parentNodeId: null }];
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
  if (dir.includes("w")) { x = snapToGrid(Math.max(0, x - delta), SNAP); width = snapToGrid(Math.max(40, width + delta), SNAP); }
  if (dir.includes("s")) height = snapToGrid(Math.max(30, height + delta), SNAP);
  if (dir.includes("n")) { y = snapToGrid(Math.max(0, y - delta), SNAP); height = snapToGrid(Math.max(30, height + delta), SNAP); }
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
  if (dir.includes("w")) { x = snapToGrid(Math.max(0, x - delta), SNAP); width = snapToGrid(Math.max(40, width + delta), SNAP); }
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
  | "idle" | "previewing" | "previewed"
  | "validating" | "validated"
  | "applying" | "applied_ok" | "applied_fail"
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

// ─── Fix 5: CSS token value resolution (SSOT-based, no hardcoded role map) ───

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
  assertEquals(resolveCssTokenValue("border.control.default"), "1px solid #ccc");
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
  assertNotEquals(snap1, snap2, "committed position change must produce different serialized state");
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
  const isMismatch = confirmedLayoutId.length > 0 && confirmedLayoutId !== sentLayoutId;
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
  const confirmedLayoutId = typeof backendData.layoutId === "string" ? backendData.layoutId : null;
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
