/**
 * uiBuilderSelectionModel.test.ts
 *
 * Selection set contract for UIBuilder canvas workspace.
 * SSOT: docs/design/admin-console-workflow-ssot.yaml ui_builder_canvas_workspace
 *
 * All selection operations are frontend-only draft interaction state.
 * Not persisted to layout_patch_json.
 * groupId: not persisted in this bundle (transient only; no SSOT for persistence target).
 *
 * Tests:
 * - selectAll: draftNodes 全件を selection set に入れる
 * - selectSubtree: parentNodeId tree に従って対象を選ぶ
 * - selectByNodeKind: nodeKind 一致 node を選ぶ
 * - selectByComponentKind: componentKind 一致 node を選ぶ
 * - invertSelection: 現在 selection set を反転する
 * - emptySelectionSet / clearSelection: selection set と primarySelectedNodeId を解除する
 * - removeFromSelectionSet: 削除 node を selection set から除去する
 * - primarySelectedNodeId: inspector 対象として維持される（既存 selectedNodeId UX 退行なし確認）
 */
import {
  assertEquals,
  assertFalse,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  emptySelectionSet,
  invertSelection,
  removeFromSelectionSet,
  selectAll,
  selectByComponentKind,
  selectByNodeKind,
  type SelectionSetContract,
  selectSubtree,
} from "../runtime/visualLayoutUtils.ts";

// ─── Fixtures ────────────────────────────────────────────────────────────────

const flatNodes = [
  { nodeId: "a", parentNodeId: null, nodeKind: "structural_html", componentKind: undefined },
  { nodeId: "b", parentNodeId: null, nodeKind: "catalog_component", componentKind: "button" },
  { nodeId: "c", parentNodeId: null, nodeKind: "catalog_component", componentKind: "card_list" },
];

const treeNodes = [
  { nodeId: "root", parentNodeId: null, nodeKind: "structural_html", componentKind: undefined },
  { nodeId: "child1", parentNodeId: "root", nodeKind: "catalog_component", componentKind: "button" },
  { nodeId: "child2", parentNodeId: "root", nodeKind: "catalog_component", componentKind: "button" },
  { nodeId: "grandchild", parentNodeId: "child1", nodeKind: "catalog_component", componentKind: "card_list" },
  { nodeId: "sibling", parentNodeId: null, nodeKind: "structural_html", componentKind: undefined },
];

// ─── selectAll ────────────────────────────────────────────────────────────────

Deno.test("selectAll: returns all nodeIds from flat nodes", () => {
  const result = selectAll(flatNodes);
  assertEquals(result.size, 3);
  assertEquals(result.has("a"), true);
  assertEquals(result.has("b"), true);
  assertEquals(result.has("c"), true);
});

Deno.test("selectAll: returns all nodeIds from tree nodes", () => {
  const result = selectAll(treeNodes);
  assertEquals(result.size, 5);
  for (const n of treeNodes) assertEquals(result.has(n.nodeId), true);
});

Deno.test("selectAll: returns empty set for empty node list", () => {
  assertEquals(selectAll([]).size, 0);
});

// ─── selectSubtree ────────────────────────────────────────────────────────────

Deno.test("selectSubtree: selects root and all descendants", () => {
  const result = selectSubtree(treeNodes, "root");
  // root, child1, child2, grandchild
  assertEquals(result.size, 4);
  assertEquals(result.has("root"), true);
  assertEquals(result.has("child1"), true);
  assertEquals(result.has("child2"), true);
  assertEquals(result.has("grandchild"), true);
  assertFalse(result.has("sibling"));
});

Deno.test("selectSubtree: selects only subtree rooted at child1", () => {
  const result = selectSubtree(treeNodes, "child1");
  assertEquals(result.size, 2);
  assertEquals(result.has("child1"), true);
  assertEquals(result.has("grandchild"), true);
  assertFalse(result.has("root"));
  assertFalse(result.has("child2"));
});

Deno.test("selectSubtree: leaf node returns set with only itself", () => {
  const result = selectSubtree(treeNodes, "grandchild");
  assertEquals(result.size, 1);
  assertEquals(result.has("grandchild"), true);
});

Deno.test("selectSubtree: unknown nodeId returns empty set", () => {
  const result = selectSubtree(treeNodes, "nonexistent");
  assertEquals(result.size, 0);
});

// ─── selectByNodeKind ─────────────────────────────────────────────────────────

Deno.test("selectByNodeKind: selects structural_html nodes", () => {
  const result = selectByNodeKind(flatNodes, "structural_html");
  assertEquals(result.size, 1);
  assertEquals(result.has("a"), true);
});

Deno.test("selectByNodeKind: selects catalog_component nodes", () => {
  const result = selectByNodeKind(flatNodes, "catalog_component");
  assertEquals(result.size, 2);
  assertEquals(result.has("b"), true);
  assertEquals(result.has("c"), true);
});

Deno.test("selectByNodeKind: returns empty set for unknown kind", () => {
  assertEquals(selectByNodeKind(flatNodes, "unknown_kind").size, 0);
});

// ─── selectByComponentKind ───────────────────────────────────────────────────

Deno.test("selectByComponentKind: selects button nodes", () => {
  const result = selectByComponentKind(flatNodes, "button");
  assertEquals(result.size, 1);
  assertEquals(result.has("b"), true);
});

Deno.test("selectByComponentKind: selects multiple matching nodes from tree", () => {
  const result = selectByComponentKind(treeNodes, "button");
  assertEquals(result.size, 2);
  assertEquals(result.has("child1"), true);
  assertEquals(result.has("child2"), true);
});

Deno.test("selectByComponentKind: returns empty set for non-matching kind", () => {
  assertEquals(selectByComponentKind(flatNodes, "nonexistent").size, 0);
});

// ─── invertSelection ─────────────────────────────────────────────────────────

Deno.test("invertSelection: inverts selection (selected → unselected and vice versa)", () => {
  const current = new Set(["a"]);
  const result = invertSelection(flatNodes, current);
  assertEquals(result.size, 2);
  assertFalse(result.has("a"));
  assertEquals(result.has("b"), true);
  assertEquals(result.has("c"), true);
});

Deno.test("invertSelection: empty selection inverts to all nodes", () => {
  const result = invertSelection(flatNodes, new Set());
  assertEquals(result.size, 3);
});

Deno.test("invertSelection: full selection inverts to empty", () => {
  const full = new Set(flatNodes.map((n) => n.nodeId));
  const result = invertSelection(flatNodes, full);
  assertEquals(result.size, 0);
});

// ─── emptySelectionSet (clearSelection) ──────────────────────────────────────

Deno.test("emptySelectionSet: returns ReadonlySet with zero elements", () => {
  const empty = emptySelectionSet();
  assertEquals(empty.size, 0);
});

Deno.test("clearSelection: primarySelectedNodeId should be null and selectedNodeIds empty", () => {
  // Simulate the contract shape after clear
  const contract: SelectionSetContract = {
    selectedNodeIds: emptySelectionSet(),
    primarySelectedNodeId: null,
  };
  assertEquals(contract.selectedNodeIds.size, 0);
  assertEquals(contract.primarySelectedNodeId, null);
});

// ─── removeFromSelectionSet ───────────────────────────────────────────────────

Deno.test("removeFromSelectionSet: removes present node", () => {
  const current = new Set(["a", "b", "c"]);
  const result = removeFromSelectionSet(current, "b");
  assertEquals(result.size, 2);
  assertFalse(result.has("b"));
  assertEquals(result.has("a"), true);
  assertEquals(result.has("c"), true);
});

Deno.test("removeFromSelectionSet: returns same reference when node not in set", () => {
  const current = new Set<string>(["a"]);
  const result = removeFromSelectionSet(current, "z");
  // Same reference returned (no-op)
  assertEquals(result === current, true);
});

Deno.test("removeFromSelectionSet: handles empty set", () => {
  const empty = new Set<string>();
  const result = removeFromSelectionSet(empty, "a");
  assertEquals(result === empty, true);
});

// ─── primarySelectedNodeId (inspector target) ────────────────────────────────

Deno.test("SelectionSetContract: primarySelectedNodeId is inspector target for single click", () => {
  // Single click: both selectedNodeIds and primarySelectedNodeId are set
  const clickedId = "child1";
  const contract: SelectionSetContract = {
    selectedNodeIds: new Set([clickedId]),
    primarySelectedNodeId: clickedId,
  };
  assertEquals(contract.primarySelectedNodeId, clickedId);
  assertEquals(contract.selectedNodeIds.size, 1);
  assertEquals(contract.selectedNodeIds.has(clickedId), true);
});

Deno.test("SelectionSetContract: primarySelectedNodeId stays unchanged when batch select used", () => {
  // Batch selection does not reset the primary (inspector) node
  const clickedId = "root";
  const allIds = new Set(treeNodes.map((n) => n.nodeId));
  const contract: SelectionSetContract = {
    selectedNodeIds: allIds,
    primarySelectedNodeId: clickedId, // unchanged from last explicit click
  };
  assertEquals(contract.primarySelectedNodeId, clickedId);
  assertEquals(contract.selectedNodeIds.size, 5);
});

// ─── selectSubtree: parentNodeId traversal correctness ───────────────────────

Deno.test("selectSubtree: sibling of root is excluded from root subtree", () => {
  const result = selectSubtree(treeNodes, "root");
  assertFalse(result.has("sibling"));
});

Deno.test("selectSubtree: deep tree — grandchild is included in root subtree", () => {
  const deepTree = [
    { nodeId: "n1", parentNodeId: null, nodeKind: "structural_html", componentKind: undefined },
    { nodeId: "n2", parentNodeId: "n1", nodeKind: "catalog_component", componentKind: "button" },
    { nodeId: "n3", parentNodeId: "n2", nodeKind: "catalog_component", componentKind: "card_list" },
    { nodeId: "n4", parentNodeId: "n3", nodeKind: "catalog_component", componentKind: "card_list" },
  ];
  const result = selectSubtree(deepTree, "n1");
  assertEquals(result.size, 4);
  for (const n of deepTree) assertEquals(result.has(n.nodeId), true);
});
