/**
 * Production-path proof for manifest 092 (credential-management)'s category selector —
 * structural_subtree_conditional_visibility_contract's FIRST CONSUMER (docs/design/
 * admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.credentials.
 * seed_contract.component_tree[credential_category_filter].conditional_visibility_note).
 *
 * Unlike layoutProjectionTreeVisibilityRender.test.ts (generic, domain-neutral catA/catB
 * fixture proving the SHARED evaluator mechanism) and layoutSchemaStructuralRender.test.ts's
 * own manifest-092 DOM test (proves a single already-active category's real Action leaves
 * reach the DOM), THIS file proves the full, real, production category-SWITCH operation
 * chain for manifest 092 specifically, against a real DOM (happy-dom + Preact render(), not
 * renderToString) and a real native <select> change event — never a direct dispatcher.set()
 * standing in for the user's own interaction:
 *
 *   real <select> options exist (3, matching Topolactor.Schema.CredentialManagementCategories.All
 *   exactly — see LayoutSchemaStructuralCompositionTests.cs's own
 *   ManifestCd004RealSeedContent_CategoryFilterOptions_* companion proof) -> initial displayed
 *   value matches the declared default -> firing a REAL native "change" DOM event on the REAL
 *   <select> element (selectEl.value = ...; dispatchEvent(new Event("change"))) drives the SAME
 *   onChange closure runtimeComponentFactory.ts's selectFactory wires for production ->
 *   emitBoundEvent -> the authored setState runtimeInteraction -> the projection-local
 *   selectedCategory state slot -> resolveNodeVisibility -> DOM mount of the new category's real
 *   Action leaves / unmount of the old category's -> a hide-then-show round trip reproduces
 *   byte-identical markup for an unaffected action (dispatchTargetRef/payloadFrom survive).
 *
 * The physical category node's own key (users/external_api_credential/instance_settings) is
 * independent from the visibilityBinding matchValue/select option value (users/
 * external_api_credential/external_instance_credential) for the third category — see the
 * ManifestCd002RealSeedContent test's own doc comment for why "instance_settings" and
 * "external_instance_credential" are deliberately different, SSOT-grounded strings, never
 * conflated or invented here.
 *
 * No lifecycle-triggered (initial_mount/route_enter/initial_display) runtimeInteraction exists
 * anywhere in manifest 092's real seed today, so this file does not fabricate one; lifecycle
 * reachability gating by the SAME resolveNodeVisibility evaluator is proven generically by
 * frontend/tests/uiEventEffectRunner.test.ts instead.
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

Deno.test("production path: manifest 092's real credential_category_filter <select> drives a real category switch end to end (real DOM, real change event, real evaluator, real mount/unmount)", async () => {
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
    // setState runtimeInteraction the real "change" event below fires) notifies this listener,
    // which re-runs renderEmission() and re-renders — never a test-only manual re-render call
    // standing in for the production reactive loop.
    localStore.subscribe(renderTree);

    renderTree();
    await flushUpdates();

    const selectEl = container.querySelector(
      '[data-node-id="credential_category_filter"] select',
    ) as unknown as { value: string; dispatchEvent: (e: Event) => boolean } | null;
    assert(selectEl, "expected the real category filter <select> element to exist in the DOM");

    // 1. Real, data-defined options — never an empty select masquerading as a completed
    // category selector, and never a fabricated/invented category label.
    const optionEls = container.querySelectorAll(
      '[data-node-id="credential_category_filter"] select option',
    );
    const optionValues = Array.from(optionEls)
      .map((o) => (o as unknown as { value: string }).value)
      .filter((v) => v !== ""); // exclude the non-required placeholder option
    assertEquals(optionValues, EXPECTED_CATEGORY_VALUES);

    // 2. Initial displayed value matches the declared default (stateJson's selectedCategory).
    assertEquals(selectEl!.value, "external_api_credential");

    // 3. Initial DOM: only the default-active category's real Action leaves are reachable.
    let html = container.innerHTML;
    assert(html.includes("Create external API credential record"), "expected external_api_credential's own action in the initial DOM");
    assert(!html.includes("Create user account (with initial credential)"), "expected users' action absent from the initial DOM");
    assert(!html.includes(">Validate<"), "expected instance_settings/external_instance_credential's action absent from the initial DOM");
    assert(!/rounded border border-red-200/.test(html), "expected zero visible error boxes on initial render");

    const firstApiCredentialActionHtml = container.querySelector(
      '[data-node-id="external_api_credential_create_button"]',
    )?.outerHTML;
    assert(firstApiCredentialActionHtml, "expected to capture the external_api_credential action's initial markup");

    // 4. Fire a REAL native "change" DOM event on the REAL <select> — the same interaction a
    // browser user performs — never a direct dispatcher.set()/store mutation standing in for it.
    selectEl!.value = "external_instance_credential";
    selectEl!.dispatchEvent(new Event("change", { bubbles: true }));
    await flushUpdates();

    html = container.innerHTML;
    assertEquals(
      (container.querySelector('[data-node-id="credential_category_filter"] select') as unknown as { value: string })
        .value,
      "external_instance_credential",
      "expected the real <select>'s displayed value to follow the user's own real selection",
    );
    assert(html.includes(">Validate<"), "expected instance_settings/external_instance_credential's own action to mount after the real switch");
    assert(!html.includes("Create external API credential record"), "expected external_api_credential's action to unmount — never merely hidden — after switching away from it");
    assert(!html.includes("Create user account (with initial credential)"), "expected users' action to remain absent");
    assert(!/rounded border border-red-200/.test(html), "expected zero visible error boxes after the real switch");
    // The options themselves never change — this is a structural mount/unmount of content, not a
    // re-population of the filter control itself.
    const optionValuesAfterSwitch = Array.from(
      container.querySelectorAll('[data-node-id="credential_category_filter"] select option'),
    )
      .map((o) => (o as unknown as { value: string }).value)
      .filter((v) => v !== "");
    assertEquals(optionValuesAfterSwitch, EXPECTED_CATEGORY_VALUES);

    // 5. Fire a SECOND real change event back to the original category — a hide-then-show round
    // trip through the real control — and prove the unaffected action's own markup (dispatch
    // target / payloadFrom wiring) is byte-identical to its first-render form.
    (container.querySelector('[data-node-id="credential_category_filter"] select') as unknown as { value: string })
      .value = "external_api_credential";
    (
      container.querySelector('[data-node-id="credential_category_filter"] select') as unknown as {
        dispatchEvent: (e: Event) => boolean;
      }
    ).dispatchEvent(new Event("change", { bubbles: true }));
    await flushUpdates();

    html = container.innerHTML;
    assert(html.includes("Create external API credential record"), "expected external_api_credential's action to re-mount on switching back");
    assert(!html.includes(">Validate<"), "expected instance_settings/external_instance_credential's action to unmount again after switching away");
    const roundTrippedApiCredentialActionHtml = container.querySelector(
      '[data-node-id="external_api_credential_create_button"]',
    )?.outerHTML;
    assertEquals(
      roundTrippedApiCredentialActionHtml,
      firstApiCredentialActionHtml,
      "expected the external_api_credential action's markup (dispatchTargetRef/payloadFrom wiring) to survive a real hide-then-show round trip byte-identically",
    );
  } finally {
    render(null, container as unknown as Element);
    cleanup();
  }
});
