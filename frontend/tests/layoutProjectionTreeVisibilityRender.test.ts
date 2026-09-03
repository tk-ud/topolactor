/**
 * DOM-connected category-collapse proof for the structural subtree conditional visibility
 * contract (docs/design/runtime-orchestration-ssot.yaml
 * ui_projection_render_reachability_contract.structural_subtree_conditional_visibility_contract).
 *
 * frontend/tests/structuralVisibility.test.ts and frontend/tests/uiEventEffectRunner.test.ts
 * already prove the shared resolveNodeVisibility()/buildVisibilityGraph() logic in isolation, and
 * that the SAME evaluator gates lifecycle-interaction reachability. This file closes the remaining
 * gap: proves the exact same mechanism gates REAL DOM mount through the REAL production pipeline
 * (renderEmission() -> LayoutProjectionTree), against REAL LayoutSchemaTensorComposer.Compose()
 * output (never a hand-authored LayoutNode literal invented to satisfy renderEmission()'s own
 * expectations) — see the "Input boundary" note in layoutSchemaStructuralRender.test.ts for the
 * same discipline this file follows.
 *
 * fixtures/layout_schema_composed_scenarios/scenario_structural_subtree_conditional_visibility.json
 * is the checked-in, byte-exact output of LayoutSchemaTensorComposer.Compose() +
 * StructureMapResolver.ToLayoutNode() for a small, domain-neutral (no credential-management/
 * manifest-specific literal) two-category scenario, proven by the companion backend test
 * backend/tests/Topolactor.Runtime.Tests/LayoutSchemaStructuralCompositionTests.cs
 * ComposeAndMapToLayoutNode_TwoVisibilityBoundCategoriesWithFilterLeafAndActions_MatchesCheckedInFrontendFixture.
 * It composes a "categoryFilter" catalog leaf (stateJson declares selectedCategory="catA") plus
 * two mutually-exclusive categories (catA/catB), each with one child Action — the exact static
 * shape credential-management's manifest 092 category-collapse now uses, expressed generically.
 *
 * Proves:
 * - initial state renders only the active category's action in the DOM; the other category's
 *   subtree (its section AND its action) never reaches the DOM at all — not CSS-hidden
 * - switching the declared state slot and re-rendering mounts the new category and unmounts the
 *   old one — the previously-visible action's DOM node is gone, not merely disabled
 * - a hidden category's action is never DOM-clickable: it is simply absent from the markup
 * - switching back to the original category re-mounts byte-identical markup for it (round trip)
 * - renderEmission() itself never drops a node for visibility reasons — spec count is identical
 *   across every switch; only LayoutProjectionTree's DOM layer varies
 * - zero componentType==="error" specs and zero visible error boxes at every step
 */
import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h } from "preact";
import { renderToString } from "preact-render-to-string";
import type { Emission, LayoutNode } from "../api/dispatch.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { LayoutProjectionTree } from "../components/LayoutProjectionTree.tsx";
import {
  createProjectionStateDispatcher,
  createRuntimeLocalStateStore,
} from "../runtime/uiEventEffectRunner.ts";
import type { WiringNode } from "../lib/uiBuilderWiringProjection.ts";

async function loadFixture(): Promise<LayoutNode[]> {
  const text = await Deno.readTextFile(
    new URL(
      "./fixtures/layout_schema_composed_scenarios/scenario_structural_subtree_conditional_visibility.json",
      import.meta.url,
    ),
  );
  return JSON.parse(text) as LayoutNode[];
}

/** Mirrors ProjectionShell.tsx's toRunnerWiringNodes() mapping — kept local since that function
 * lives in an island file this suite does not import. */
function toRunnerWiringNodes(layoutNodes: readonly LayoutNode[]): WiringNode[] {
  return layoutNodes
    .filter((n): n is LayoutNode & { nodeId: string } => typeof n.nodeId === "string" && n.nodeId.length > 0)
    .map((n) => ({
      nodeId: n.nodeId,
      componentKey: n.componentKey,
      componentKind: n.componentKind,
      stateJson: n.stateJson ?? undefined,
      runtimeInteractions: n.runtimeInteractions ?? undefined,
      parentNodeId: n.parentNodeId,
      visibilityBinding: n.visibilityBinding ?? undefined,
    }));
}

function renderHtml(layoutNodes: LayoutNode[], dispatcher: ReturnType<typeof createProjectionStateDispatcher>) {
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-000000000201",
    layoutNodes,
  };
  const specs = renderEmission(emission, defaultComponentRegistry, { localStateStore: dispatcher });
  const html = renderToString(
    h(LayoutProjectionTree, { specs, layoutId: emission.layoutId, localStateStore: dispatcher }),
  );
  return { specs, html };
}

Deno.test("DOM-connected proof: initial state (selectedCategory=catA from stateJson) mounts ONLY Category A's action; Category B's section and action never reach the DOM", async () => {
  const layoutNodes = await loadFixture();
  const dispatcher = createProjectionStateDispatcher(toRunnerWiringNodes(layoutNodes), createRuntimeLocalStateStore());

  const { specs, html } = renderHtml(layoutNodes, dispatcher);

  assertEquals(specs.filter((s) => s.componentType === "error"), []);
  assertEquals(specs.length, layoutNodes.length, "renderEmission() never drops a node for visibility reasons");

  assert(html.includes(">Action A<"), `expected Category A's action in the DOM; html: ${html}`);
  assert(!html.includes(">Action B<"), `expected Category B's action absent from the DOM; html: ${html}`);
  assert(!html.includes("Section B"), "expected Category B's whole subtree (including its section) unmounted, not merely its action");
  assert(!/rounded border border-red-200/.test(html), "expected zero visible error boxes");
});

Deno.test("DOM-connected proof: switching the declared state slot mounts the new category and unmounts the old one — not CSS-hidden, not merely disabled", async () => {
  const layoutNodes = await loadFixture();
  const dispatcher = createProjectionStateDispatcher(toRunnerWiringNodes(layoutNodes), createRuntimeLocalStateStore());

  const before = renderHtml(layoutNodes, dispatcher);
  assert(before.html.includes(">Action A<"));
  assert(!before.html.includes(">Action B<"));

  const setResult = dispatcher.set("categoryFilter", "selectedCategory", "catB");
  assert(setResult.ok, "declared slot switch must succeed");

  const after = renderHtml(layoutNodes, dispatcher);
  assertEquals(after.specs.filter((s) => s.componentType === "error"), []);
  assertEquals(after.specs.length, layoutNodes.length, "the static composed tree is unaffected by the state switch");

  assert(after.html.includes(">Action B<"), `expected Category B's action to mount after switching; html: ${after.html}`);
  assert(!after.html.includes(">Action A<"), `expected Category A's action to unmount after switching — never just CSS-hidden; html: ${after.html}`);
  assert(!after.html.includes("Section A"), "expected Category A's whole subtree unmounted");
  assert(!/rounded border border-red-200/.test(after.html), "expected zero visible error boxes after the switch");

  // Never DOM-clickable: the hidden category's action isn't present at all, so there is no
  // <button>Action A</button> node in the markup a click handler could ever attach to.
  assert(!/<button[^>]*>\s*Action A\s*<\/button>/.test(after.html));
});

Deno.test("DOM-connected proof: switching back to the original category re-mounts byte-identical markup (dispatchTargetRef/payloadFrom-independent structural round trip)", async () => {
  const layoutNodes = await loadFixture();
  const dispatcher = createProjectionStateDispatcher(toRunnerWiringNodes(layoutNodes), createRuntimeLocalStateStore());

  const initial = renderHtml(layoutNodes, dispatcher);

  assert(dispatcher.set("categoryFilter", "selectedCategory", "catB").ok);
  renderHtml(layoutNodes, dispatcher);

  assert(dispatcher.set("categoryFilter", "selectedCategory", "catA").ok);
  const roundTripped = renderHtml(layoutNodes, dispatcher);

  assertEquals(roundTripped.html, initial.html, "Category A's markup must be identical after a hide-then-show round trip");
});
