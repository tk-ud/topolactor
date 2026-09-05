// frontend/tests/uiBuilderInternalApiWiringProjection.test.tsx
//
// SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
// lane_storage_boundary.lanes.package_internal_api_wiring_lane / setting_category_taxonomy
// (内部API is part of the wiring inspector classification and "must not be hidden outside the
// inspector taxonomy while claiming backend-side completion").
//
// Before this round, WiringGraphPanel's `internalApiWirings` projection input existed and was
// fully implemented (buildWiringGraphProjection projects it into a category:"internal_api" edge),
// but the ONLY production mount of WiringGraphPanel (frontend/islands/UiBuilderAdmin.tsx, "配線
// ビュー" canvas mode) never passed it — the capability was real but structurally disconnected from
// production composition, so 内部API never actually appeared in the wiring inspector taxonomy view.
//
// This proves two things with real production code, not a synthetic helper call:
// 1. The REAL useEffectivePackageWiringKind hook (mounted in a live DOM, hitting a mocked
//    ui_topology:get_package_wiring fetch — the SAME action PackageWiringEditor itself reads)
//    resolves targetSurface/targetRef alongside wiringKind.
// 2. UiBuilderAdmin.tsx's actual <WiringGraphPanel /> call site now forwards internalApiWirings,
//    and that prop is DERIVED from the same hook/package-wiring row rather than a hardcoded empty
//    array — i.e. the projection capability and the production authoring composition are the same
//    taxonomy, not two disconnected surfaces.

import { assert, assertEquals, assertExists, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import { useEffectivePackageWiringKind } from "../lib/uiBuilderEventAuthoringHooks.ts";
import { LayoutRightDock } from "../islands/UiBuilderAdmin.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

function HookHarness(
  { packageId, onResult }: {
    packageId: string;
    onResult: (r: { wiringKind: string | null; targetSurface: string | null; targetRef: string | null }) => void;
  },
) {
  const result = useEffectivePackageWiringKind(packageId);
  onResult(result);
  return h("div", null, JSON.stringify(result));
}

Deno.test("useEffectivePackageWiringKind: resolves targetSurface/targetRef alongside wiringKind from the SAME ui_topology:get_package_wiring row PackageWiringEditor reads", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  try {
    globalThis.fetch = (_url: string | URL | Request, init?: RequestInit) => {
      const req = JSON.parse((init?.body as string) ?? "{}");
      if (req.layer === "ui_topology" && req.action === "get_package_wiring") {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              success: true,
              emission: {
                data: {
                  wiringKind: "manifest",
                  targetSurface: "manifest",
                  targetRef: "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups",
                },
              },
            }),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      }
      return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
    };
    let latest: { wiringKind: string | null; targetSurface: string | null; targetRef: string | null } | null = null;
    render(
      h(HookHarness, {
        packageId: "pkg-1",
        onResult: (r) => {
          latest = r;
        },
      }),
      container,
    );
    await flushUpdates();
    await flushUpdates();
    if (!latest) throw new Error("hook must have produced a result");
    const result = latest as { wiringKind: string | null; targetSurface: string | null; targetRef: string | null };
    assertEquals(result.wiringKind, "manifest");
    assertEquals(result.targetSurface, "manifest");
    assertEquals(
      result.targetRef,
      "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups",
    );
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("UiBuilderAdmin production composition: the real WiringGraphPanel mount forwards internalApiWirings derived from useEffectivePackageWiringKind, not a hardcoded empty array", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // The wiring-canvas call site must forward the prop.
  assert(
    /<WiringGraphPanel[\s\S]{0,800}internalApiWirings=\{internalApiWirings\}/.test(source),
    "the production <WiringGraphPanel /> mount must pass internalApiWirings",
  );
  // internalApiWirings must be derived from the SAME package-wiring authority
  // (useEffectivePackageWiringKind's targetSurface/targetRef), not a literal [] — otherwise the
  // prop would exist syntactically while staying permanently empty.
  assert(
    source.includes(
      "const { targetSurface: fetchedPackageWiringTargetSurface, targetRef: fetchedPackageWiringTargetRef } =",
    ),
    "internalApiWirings must be sourced from useEffectivePackageWiringKind's targetSurface/targetRef",
  );
  assert(
    /const internalApiWirings: InternalApiWiringInput\[\] = packageWiringTargetSurface === "manifest"/
      .test(source),
    "internalApiWirings must be built from the package wiring row's targetSurface==='manifest' case",
  );
  // Staleness fix: internalApiWirings must be overridable by a same-package sibling save, not
  // only by the initial fetch (round 19's own wiringSaveOverride pattern, reused here).
  assert(
    source.includes("onPackageWiringSaved={onPackageWiringSaved}"),
    "LayoutRightDock must receive the save-invalidation callback so a same-package save is reflected",
  );
  assert(
    source.includes("activePackageWiringOverride") &&
      source.includes("packageWiringSaveOverride.packageId === scopedPackageId"),
    "internalApiWirings must prefer a fresh same-package save (packageWiringSaveOverride) over the initial fetch",
  );
});

Deno.test("LayoutRightDock production composition: a real PackageWiringEditor save (targetSurface=manifest) bubbles the FULL fresh row — including targetSurface/targetRef, not only wiringKind — to onPackageWiringSaved", async () => {
  // Behavioral proof (not source regex) of the staleness fix: drives the SAME real save flow
  // layoutRightDockWiringKindSync.test.tsx already proves for the admin_runtime wiringKind gate,
  // but asserts the NEW onPackageWiringSaved callback (which the parent canvas component uses to
  // refresh WiringGraphPanel's internalApiWirings) receives the complete fresh row.
  const PACKAGE_ID = "22222222-2222-2222-2222-222222222222";
  const state: { wiringKind: string; targetSurface: string; targetRef: string | null } = {
    wiringKind: "external_port",
    targetSurface: "route",
    targetRef: null,
  };
  const fetchImpl: typeof globalThis.fetch = (url: string | URL | Request, init?: RequestInit) => {
    const urlStr = typeof url === "string" ? url : url.toString();
    if (!urlStr.includes("/api/dispatch")) {
      return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
    }
    const req = JSON.parse((init?.body as string) ?? "{}");
    const layer = req.layer as string | undefined;
    const action = req.action as string | undefined;
    if (layer === "ui_topology" && action === "get_package_wiring") {
      return Promise.resolve(
        new Response(
          JSON.stringify({
            success: true,
            emission: {
              data: {
                wiringId: "wiring-1",
                wiringKey: "test-wiring",
                wiringKind: state.wiringKind,
                targetSurface: state.targetSurface,
                targetRef: state.targetRef,
              },
            },
          }),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      );
    }
    if (layer === "ui_topology" && action === "update_package_wiring") {
      // The mock fully controls the save RESPONSE shape — this is what proves the plumbing
      // (onSiblingWiringSaved -> onPackageWiringSaved) forwards the row's targetSurface/targetRef
      // with full fidelity, regardless of exactly which UI fields a human would have driven to
      // reach a manifest-surface save in production.
      state.wiringKind = (req.payload?.wiringKind as string) ?? state.wiringKind;
      state.targetSurface = "manifest";
      state.targetRef = "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups";
      return Promise.resolve(
        new Response(
          JSON.stringify({
            success: true,
            emission: {
              data: {
                wiring: {
                  wiringId: "wiring-1",
                  wiringKey: "test-wiring",
                  wiringKind: state.wiringKind,
                  targetSurface: state.targetSurface,
                  targetRef: state.targetRef,
                },
              },
            },
          }),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      );
    }
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, emission: { data: [] } }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
  };

  function accordionTriggerByTitle(container: Element, title: string): HTMLButtonElement {
    const buttons = Array.from(container.querySelectorAll("button.accordion-trigger-closed, button.accordion-trigger-open"));
    const match = buttons.find((b) => b.textContent?.includes(title)) as HTMLButtonElement | undefined;
    assertExists(match, `accordion trigger not found: ${title}`);
    return match;
  }
  function buttonByText(container: Element, text: string): HTMLButtonElement {
    const buttons = Array.from(container.querySelectorAll("button"));
    const match = (buttons.find((b) => b.textContent?.trim() === text) ??
      buttons.find((b) => b.textContent?.includes(text))) as HTMLButtonElement | undefined;
    assertExists(match, `button not found: ${text}`);
    return match;
  }

  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  let saved: { targetSurface: string; targetRef?: string | null } | null = null;
  try {
    globalThis.fetch = fetchImpl;
    const node = {
      nodeId: "node-1",
      componentKey: "text_display",
      isDraftOnly: false,
      slotKey: "root",
      orderIndex: 0,
      parentNodeId: null,
      gridCol: 0,
      gridRow: 0,
      x: 0,
      y: 0,
      width: 100,
      height: 40,
      packageId: PACKAGE_ID,
    };
    render(
      h(LayoutRightDock, {
        draftNodes: [node],
        selectedNodeId: node.nodeId,
        selectedNodeIds: undefined,
        selectedNode: node,
        packageId: PACKAGE_ID,
        onSelectNode: () => {},
        onReparent: () => {},
        onCopy: () => {},
        onDelete: () => {},
        slotKeyCandidates: ["root"],
        onUpdateNode: () => {},
        onCommitNode: () => {},
        onToggleLayoutClassRef: () => {},
        onDesignChange: () => {},
        routeCandidates: ["demo"],
        topologyRouteKey: undefined,
        canvasDesignDraft: undefined,
        calculationBindings: [],
        draftNodeIds: [node.nodeId],
        calcResults: new Map(),
        onCalcBindingsChange: () => {},
        emissionDataJson: "{}",
        onEmissionDataJsonChange: () => {},
        onBatchApplyNodes: () => {},
        suggestShape: null,
        // deno-lint-ignore no-explicit-any
        onPackageWiringSaved: (wiring: any) => {
          saved = wiring;
        },
        // deno-lint-ignore no-explicit-any
      } as any),
      container,
    );
    await flushUpdates();
    await flushUpdates();

    accordionTriggerByTitle(container, "内部API").dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();

    const saveBtn = buttonByText(container, "配線を保存");
    saveBtn.dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();

    const confirmBtn = buttonByText(container, "実行する");
    confirmBtn.dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();
    await flushUpdates();

    if (!saved) throw new Error("onPackageWiringSaved must have been called after a real save");
    const result = saved as { targetSurface: string; targetRef?: string | null };
    assertEquals(result.targetSurface, "manifest");
    assertEquals(result.targetRef, "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups");
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});
