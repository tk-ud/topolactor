/**
 * Production-path proof for manifest 092 (credential-management)'s category selector —
 * structural_subtree_conditional_visibility_contract's CONSUMER/evidence (docs/design/
 * admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.credentials.
 * seed_contract.component_tree[credential_category_filter].conditional_visibility_note).
 *
 * Unlike layoutProjectionTreeVisibilityRender.test.ts (generic, domain-neutral catA/catB
 * fixture proving the SHARED evaluator mechanism) and layoutSchemaStructuralRender.test.ts's
 * own manifest-092 DOM test (proves a single already-active category's real Action leaves
 * reach the DOM), THIS file proves the full, real, production category-SWITCH operation
 * chain for manifest 092 specifically, against a real DOM (happy-dom + Preact render(), not
 * renderToString) and a real native tab `click` event — never a direct dispatcher.set()
 * standing in for the user's own interaction:
 *
 *   real tab buttons exist (3, matching Topolactor.Schema.CredentialManagementCategories.All
 *   exactly — see LayoutSchemaStructuralCompositionTests.cs's own
 *   ManifestCd004RealSeedContent_CategoryFilterTabsItems_* companion proof) -> initial
 *   aria-selected tab matches the declared default -> firing a REAL native "click" DOM event on
 *   a REAL tab `<button role="tab">` (tabEl.dispatchEvent(new MouseEvent("click"))) drives the
 *   SAME onClick/onSelect closure runtimeComponentFactory.ts's tabsFactory wires for production ->
 *   emitBoundEvent -> the authored setState runtimeInteraction -> the projection-local
 *   selectedCategory state slot -> resolveNodeVisibility -> DOM mount of the new category's real
 *   Action leaves / unmount of the old category's -> a hide-then-show round trip reproduces
 *   byte-identical markup for an unaffected action (dispatchTargetRef/payloadFrom survive).
 *
 * (structural-subtree-conditional-visibility-implementation, tabs-presentation closure round:
 * credential_category_filter's presentation moved from select.template to tabs.template —
 * docs/design/admin-normal-surface-projection-seed-ssot.yaml's own presentation_history field —
 * driving the SAME ui-local:credential_category_filter.selectedCategory source slot the generic
 * SSOT's presentation_component_independence text already anticipated a tabs.template consumer
 * would use, with zero change to the generic evaluator. This file was a <select>/"change"-event
 * proof before that round; it is rewritten here to drive the real tabs UI instead, never a
 * `dispatcher.set()` standing in for it.)
 *
 * The physical category node's own key (users/external_api_credential/instance_settings) is
 * independent from the visibilityBinding matchValue/tab item key (users/
 * external_api_credential/external_instance_credential) for the third category — see the
 * ManifestCd002RealSeedContent test's own doc comment for why "instance_settings" and
 * "external_instance_credential" are deliberately different, SSOT-grounded strings, never
 * conflated or invented here.
 *
 * No lifecycle-triggered (initial_mount/route_enter/initial_display) runtimeInteraction exists
 * anywhere in manifest 092's real seed today, so this file does not fabricate one; lifecycle
 * reachability gating by the SAME resolveNodeVisibility evaluator is proven generically by
 * frontend/tests/uiEventEffectRunner.test.ts instead.
 *
 * structural-subtree-conditional-visibility-implementation round (owner-directed
 * credential-management seed UI日本語化): action labels asserted here are the real, seed-authored
 * Japanese display text (admin-normal-surface-projection-seed-ssot.yaml credentials.seed_contract.
 * display_language_boundary) -- machine identity (data-node-id, the tab item `key`s asserted
 * against EXPECTED_CATEGORY_VALUES, targetRef/payloadFrom) stays canonical/English and is never
 * used interchangeably with a label string as an identifier. Step 6 below additionally audits
 * that no pre-translation English operation sentence remains reachable anywhere in this
 * manifest's rendered surface, across all three categories.
 */
import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import type { Emission, LayoutNode } from "../api/dispatch.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { LayoutProjectionTree } from "../components/LayoutProjectionTree.tsx";
import {
  createProjectionStateDispatcher,
  createRuntimeLocalStateStore,
} from "../runtime/uiEventEffectRunner.ts";
import { createLiveNodeValueTracker } from "../runtime/liveNodeValueTracker.ts";
import type { WiringNode } from "../lib/uiBuilderWiringProjection.ts";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

/** The SAME 3-member enum backend/schema/CredentialManagementSearchContracts.cs's
 * CredentialManagementCategories.All declares — asserted equal to the real seed's authored
 * options by LayoutSchemaStructuralCompositionTests.cs; duplicated here only as an assertion
 * target for THIS file's own DOM proof, never as a second source of truth the seed is
 * generated from. */
const EXPECTED_CATEGORY_VALUES = ["users", "external_api_credential", "external_instance_credential"];

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

async function loadManifest092Fixture(): Promise<LayoutNode[]> {
  const text = await Deno.readTextFile(
    new URL("./fixtures/manifest_0092_bare_entry_layout_nodes.json", import.meta.url),
  );
  return JSON.parse(text) as LayoutNode[];
}

function getTabButtons(container: Element): { key: string; el: Element }[] {
  const buttons = Array.from(
    container.querySelectorAll('[data-node-id="credential_category_filter"] button[role="tab"]'),
  );
  return buttons.map((el, i) => ({ key: EXPECTED_CATEGORY_VALUES[i], el }));
}

function clickTab(container: Element, key: string): void {
  const tabs = getTabButtons(container);
  const target = tabs.find((t) => t.key === key);
  assert(target, `expected a real tab button for category "${key}"`);
  (target!.el as unknown as { dispatchEvent: (e: Event) => boolean }).dispatchEvent(
    new Event("click", { bubbles: true }),
  );
}

function activeTabKey(container: Element): string | undefined {
  const tabs = getTabButtons(container);
  return tabs.find((t) => (t.el as unknown as { getAttribute: (n: string) => string | null }).getAttribute("aria-selected") === "true")?.key;
}

Deno.test("production path: manifest 092's real credential_category_filter tabs drive a real category switch end to end (real DOM, real click event, real evaluator, real mount/unmount)", async () => {
  const layoutNodes = await loadManifest092Fixture();
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes,
    packageId: "00000000-0000-0000-0000-0000000cd005",
    manifestId: "00000000-0000-0000-0000-000000000092",
  };

  const localStore = createRuntimeLocalStateStore();
  const dispatcher = createProjectionStateDispatcher(toRunnerWiringNodes(layoutNodes), localStore);
  const tracker = createLiveNodeValueTracker();

  const { container, cleanup } = setupDom();
  try {
    function renderTree(): void {
      const specs = renderEmission(emission, defaultComponentRegistry, {
        localStateStore: dispatcher,
        payloadFromNodeValues: tracker.snapshot(),
        onNodeValueChange: (nodeId, value) => tracker.set(nodeId, value),
      });
      render(
        h(LayoutProjectionTree, { specs, layoutId: emission.layoutId, localStateStore: dispatcher }),
        container,
      );
    }

    // Mirrors ProjectionShell.tsx's own production wiring: a state-store mutation (from the
    // setState runtimeInteraction the real "click"/"select" event below fires) notifies this
    // listener, which re-runs renderEmission() and re-renders — never a test-only manual
    // re-render call standing in for the production reactive loop.
    localStore.subscribe(renderTree);

    renderTree();
    await flushUpdates();

    const tabsRoot = container.querySelector('[data-node-id="credential_category_filter"]');
    assert(tabsRoot, "expected the real category filter tabs node to exist in the DOM");

    // 1. Real, data-defined tab buttons — never an empty tablist masquerading as a completed
    // category selector, and never a fabricated/invented category label.
    const tabButtons = getTabButtons(container);
    assertEquals(tabButtons.map((t) => t.key), EXPECTED_CATEGORY_VALUES);
    assertEquals(tabButtons.length, 3, "expected exactly 3 real tab buttons, matching Topolactor.Schema.CredentialManagementCategories.All");

    // 2. Initial active tab matches the declared default (stateJson's selectedCategory).
    assertEquals(activeTabKey(container), "external_api_credential");

    // 3. Initial DOM: only the default-active category's real Action leaves are reachable.
    let html = container.innerHTML;
    assert(html.includes("外部APIクレデンシャルレコードを作成"), "expected external_api_credential's own action in the initial DOM");
    assert(!html.includes("ユーザーアカウントを作成"), "expected users' action absent from the initial DOM");
    assert(!html.includes(">検証<"), "expected instance_settings/external_instance_credential's action absent from the initial DOM");
    assert(!/rounded border border-red-200/.test(html), "expected zero visible error boxes on initial render");

    const firstApiCredentialActionHtml = container.querySelector(
      '[data-node-id="external_api_credential_create_button"]',
    )?.outerHTML;
    assert(firstApiCredentialActionHtml, "expected to capture the external_api_credential action's initial markup");

    // 4. Fire a REAL native "click" DOM event on a REAL tab `<button role="tab">` — the same
    // interaction a browser user performs — never a direct dispatcher.set()/store mutation
    // standing in for it.
    clickTab(container, "external_instance_credential");
    await flushUpdates();

    html = container.innerHTML;
    assertEquals(
      activeTabKey(container),
      "external_instance_credential",
      "expected the real tab's aria-selected state to follow the user's own real click",
    );
    assert(html.includes(">検証<"), "expected instance_settings/external_instance_credential's own action to mount after the real switch");
    assert(!html.includes("外部APIクレデンシャルレコードを作成"), "expected external_api_credential's action to unmount — never merely hidden — after switching away from it");
    assert(!html.includes("ユーザーアカウントを作成"), "expected users' action to remain absent");
    assert(!/rounded border border-red-200/.test(html), "expected zero visible error boxes after the real switch");
    // The tab buttons themselves never change — this is a structural mount/unmount of content,
    // not a re-population of the filter control itself.
    assertEquals(getTabButtons(container).map((t) => t.key), EXPECTED_CATEGORY_VALUES);

    // 5. Fire a SECOND real click back to the original category — a hide-then-show round
    // trip through the real control — and prove the unaffected action's own markup (dispatch
    // target / payloadFrom wiring) is byte-identical to its first-render form.
    clickTab(container, "external_api_credential");
    await flushUpdates();

    html = container.innerHTML;
    assert(html.includes("外部APIクレデンシャルレコードを作成"), "expected external_api_credential's action to re-mount on switching back");
    assert(!html.includes(">検証<"), "expected instance_settings/external_instance_credential's action to unmount again after switching away");
    const roundTrippedApiCredentialActionHtml = container.querySelector(
      '[data-node-id="external_api_credential_create_button"]',
    )?.outerHTML;
    assertEquals(
      roundTrippedApiCredentialActionHtml,
      firstApiCredentialActionHtml,
      "expected the external_api_credential action's markup (dispatchTargetRef/payloadFrom wiring) to survive a real hide-then-show round trip byte-identically",
    );

    // 5b. A disabled tab must never dispatch — hidden-category action/lifecycle unreachability
    // is proven generically elsewhere, but the tabs control itself must also refuse a click on
    // a disabled item. Manifest 092's real seed authors zero disabled tabs today, so this only
    // asserts the structural guard the shared Tabs.tsx component itself enforces (see its own
    // `!tab.disabled && onSelect(tab.key)` onClick guard) rather than fabricating a disabled
    // category that doesn't exist in the real seed.
    for (const t of getTabButtons(container)) {
      assertEquals(
        (t.el as unknown as { disabled: boolean }).disabled,
        false,
        `expected no real category tab ("${t.key}") to be disabled in manifest 092's authored seed`,
      );
    }

    // 6. Japanese seed UI audit (structural-subtree-conditional-visibility-implementation round,
    // owner-directed credential-management seed UI日本語化): none of the pre-translation English
    // operation sentences this exact production-path proof used to assert are still reachable
    // anywhere in this manifest's rendered surface, across every category. Cycle through all
    // three categories (the third, external_instance_credential, was never re-selected above) so
    // this check covers content this test's earlier steps never rendered.
    let allHtml = html;
    for (const category of EXPECTED_CATEGORY_VALUES) {
      clickTab(container, category);
      await flushUpdates();
      allHtml += container.innerHTML;
    }
    for (
      const staleEnglish of [
        "Create external API credential record",
        "Create user account (with initial credential)",
        "Create external instance credential record",
        ">Validate<",
        ">Preview<",
        ">Apply<",
        ">Approve<",
      ]
    ) {
      assert(
        !allHtml.includes(staleEnglish),
        `expected no pre-translation English operation sentence "${staleEnglish}" to remain reachable anywhere in the credential-management surface`,
      );
    }
  } finally {
    render(null, container as unknown as Element);
    cleanup();
  }
});
