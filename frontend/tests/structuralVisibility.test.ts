/**
 * Proof surface for the generic schema-composed structural subtree conditional
 * visibility evaluator.
 * SSOT: docs/design/runtime-orchestration-ssot.yaml
 * ui_projection_render_reachability_contract.structural_subtree_conditional_visibility_contract
 *
 * Proves the resolver frontend/components/LayoutProjectionTree.tsx (DOM mount) and
 * frontend/runtime/uiEventEffectRunner.ts (lifecycle reachability) both call:
 * - a node with no visibilityBinding anywhere in its ancestor chain is always visible
 * - a node whose own or an ancestor's visibilityBinding source resolves to a
 *   non-matching live value is invisible
 * - multiple bindings on one root-to-leaf path compose as a logical AND
 * - an undeclared or malformed binding source fails close (explicit error), never
 *   silently visible or silently hidden
 * - the evaluator is generic: it is driven entirely by nodeId/parentNodeId/
 *   visibilityBinding data, never a credential-management or manifest-specific literal
 */

import { assert, assertEquals } from "jsr:@std/assert";
import {
  buildVisibilityGraph,
  resolveNodeVisibility,
  type VisibilityGraphNode,
} from "../runtime/structuralVisibility.ts";
import { createRuntimeStateDispatcher, createRuntimeLocalStateStore } from "../runtime/uiEventEffectRunner.ts";

function store(declared: Record<string, Record<string, unknown>>) {
  const dispatcher = createRuntimeStateDispatcher(createRuntimeLocalStateStore());
  for (const [nodeId, slots] of Object.entries(declared)) {
    for (const [stateKey, value] of Object.entries(slots)) {
      dispatcher.declare(nodeId, stateKey, value);
    }
  }
  return dispatcher;
}

Deno.test("resolveNodeVisibility: a node with no visibilityBinding anywhere in its ancestor chain is always visible", () => {
  const graph = buildVisibilityGraph([
    { nodeId: "root" },
    { nodeId: "child", parentNodeId: "root" },
    { nodeId: "leaf", parentNodeId: "child" },
  ]);
  const result = resolveNodeVisibility("leaf", graph, undefined);
  assertEquals(result, { ok: true, visible: true });
});

Deno.test("resolveNodeVisibility: a node's OWN visibilityBinding gates it — matching value is visible, non-matching is not", () => {
  const graph = buildVisibilityGraph([
    {
      nodeId: "catA",
      visibilityBinding: { source: "ui-local:selector.selectedCategory", matchValue: "catA" },
    },
  ]);
  const matching = store({ selector: { selectedCategory: "catA" } });
  assertEquals(resolveNodeVisibility("catA", graph, matching), { ok: true, visible: true });

  const nonMatching = store({ selector: { selectedCategory: "catB" } });
  assertEquals(resolveNodeVisibility("catA", graph, nonMatching), { ok: true, visible: false });
});

Deno.test("resolveNodeVisibility: a descendant inherits an ancestor's visibilityBinding without authoring its own", () => {
  const graph = buildVisibilityGraph([
    {
      nodeId: "catA",
      visibilityBinding: { source: "ui-local:selector.selectedCategory", matchValue: "catA" },
    },
    { nodeId: "section", parentNodeId: "catA" },
    { nodeId: "field", parentNodeId: "section" },
  ]);
  const hidden = store({ selector: { selectedCategory: "catB" } });
  assertEquals(resolveNodeVisibility("field", graph, hidden), { ok: true, visible: false });

  const visible = store({ selector: { selectedCategory: "catA" } });
  assertEquals(resolveNodeVisibility("field", graph, visible), { ok: true, visible: true });
});

Deno.test("resolveNodeVisibility: multiple bindings on one root-to-leaf path compose as logical AND, never OR", () => {
  const graph = buildVisibilityGraph([
    {
      nodeId: "catA",
      visibilityBinding: { source: "ui-local:selector.selectedCategory", matchValue: "catA" },
    },
    {
      nodeId: "advancedSection",
      parentNodeId: "catA",
      visibilityBinding: { source: "ui-local:toggle.advancedMode", matchValue: true },
    },
    { nodeId: "advancedField", parentNodeId: "advancedSection" },
  ]);

  // Category matches but advanced mode is off — still invisible (AND, not OR).
  const catOnly = store({
    selector: { selectedCategory: "catA" },
    toggle: { advancedMode: false },
  });
  assertEquals(resolveNodeVisibility("advancedField", graph, catOnly), { ok: true, visible: false });

  // Advanced mode on but wrong category — still invisible.
  const advancedOnly = store({
    selector: { selectedCategory: "catB" },
    toggle: { advancedMode: true },
  });
  assertEquals(resolveNodeVisibility("advancedField", graph, advancedOnly), { ok: true, visible: false });

  // Both conditions satisfied — visible.
  const both = store({
    selector: { selectedCategory: "catA" },
    toggle: { advancedMode: true },
  });
  assertEquals(resolveNodeVisibility("advancedField", graph, both), { ok: true, visible: true });
});

Deno.test("resolveNodeVisibility: an undeclared source slot fails close as an explicit error — never silently visible or hidden", () => {
  const graph = buildVisibilityGraph([
    {
      nodeId: "catA",
      visibilityBinding: { source: "ui-local:never_declared.selectedCategory", matchValue: "catA" },
    },
  ]);
  const emptyStore = createRuntimeStateDispatcher(createRuntimeLocalStateStore());
  const result = resolveNodeVisibility("catA", graph, emptyStore);
  assert(!result.ok);
  assert(result.error.startsWith("STRUCTURAL_VISIBILITY_BINDING_SOURCE_UNDECLARED"));
});

Deno.test("resolveNodeVisibility: a malformed source shape (not ui-local:<nodeId>.<stateKey>) fails close as an explicit error", () => {
  const graph = buildVisibilityGraph([
    { nodeId: "catA", visibilityBinding: { source: "not-a-real-ref", matchValue: "catA" } },
  ]);
  const anyStore = store({});
  const result = resolveNodeVisibility("catA", graph, anyStore);
  assert(!result.ok);
  assert(result.error.startsWith("STRUCTURAL_VISIBILITY_BINDING_INVALID"));
});

Deno.test("resolveNodeVisibility: no store at all (e.g. a caller that never wires one in) fails close identically to undeclared — never defaults to visible", () => {
  const graph = buildVisibilityGraph([
    { nodeId: "catA", visibilityBinding: { source: "ui-local:selector.selectedCategory", matchValue: "catA" } },
  ]);
  const result = resolveNodeVisibility("catA", graph, undefined);
  assert(!result.ok);
  assert(result.error.startsWith("STRUCTURAL_VISIBILITY_BINDING_SOURCE_UNDECLARED"));
});

Deno.test("resolveNodeVisibility: a node absent from the graph resolves visible (defensive — no ancestor chain to walk, nothing to gate it)", () => {
  const graph: ReadonlyMap<string, VisibilityGraphNode> = buildVisibilityGraph([]);
  assertEquals(resolveNodeVisibility("ghost", graph, undefined), { ok: true, visible: true });
});

Deno.test("resolveNodeVisibility: three mutually-exclusive sibling categories on the SAME source resolve independently — the credential-management category-collapse shape, expressed generically", () => {
  const graph = buildVisibilityGraph([
    { nodeId: "users", visibilityBinding: { source: "ui-local:credential_category_filter.selectedCategory", matchValue: "users" } },
    { nodeId: "external_api_credential", visibilityBinding: { source: "ui-local:credential_category_filter.selectedCategory", matchValue: "external_api_credential" } },
    { nodeId: "instance_settings", visibilityBinding: { source: "ui-local:credential_category_filter.selectedCategory", matchValue: "instance_settings" } },
  ]);
  const selected = store({ credential_category_filter: { selectedCategory: "external_api_credential" } });
  assertEquals(resolveNodeVisibility("users", graph, selected), { ok: true, visible: false });
  assertEquals(resolveNodeVisibility("external_api_credential", graph, selected), { ok: true, visible: true });
  assertEquals(resolveNodeVisibility("instance_settings", graph, selected), { ok: true, visible: false });
});
