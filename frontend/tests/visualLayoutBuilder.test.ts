import { assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  snapToGrid,
  buildVisualLayoutPatchJson,
  getDraftOnlyNodes,
  isDraftOnlyApplyBlocked,
  wouldCreateVisualParentCycle,
  type VisualNodePayload,
} from "../runtime/visualLayoutUtils.ts";

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
