// frontend/tests/layoutRightDockWiringKindSync.test.tsx
//
// Round 19 DOM interaction proof: the PRODUCTION LayoutRightDock component (mounted whole, not a
// hand-rolled stand-in) renders BOTH PackageWiringEditor (via PackageConnectionPanel) and
// NodeEventAuthoringPanel as sibling panels for the SAME package. Before this round,
// useEffectivePackageWiringKind only re-fetched on packageId CHANGE, so a real save inside
// PackageWiringEditor for the SAME package left NodeEventAuthoringPanel's admin_runtime override
// gate showing the stale pre-save wiringKind until an unrelated remount happened to re-fetch it.
//
// This test drives the REAL save flow end to end: open both accordions, change wiringKind from a
// non-admin_runtime value to "admin_runtime" via PackageWiringEditor's own <select>/save button/
// confirm dialog, and asserts the override gate on the SAME mounted component instance flips from
// disabled-with-reason to fully editable -- without unmounting or changing packageId.

import { assert, assertFalse, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import { LayoutRightDock } from "../islands/UiBuilderAdmin.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

const PACKAGE_ID = "11111111-1111-1111-1111-111111111111";

function makeDispatchFetch(state: { wiringKind: string }): typeof globalThis.fetch {
  return (url: string | URL | Request, init?: RequestInit) => {
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
                targetSurface: "route",
                targetRef: null,
              },
            },
          }),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      );
    }

    if (layer === "ui_topology" && action === "update_package_wiring") {
      const nextWiringKind = (req.payload?.wiringKind as string) ?? state.wiringKind;
      state.wiringKind = nextWiringKind;
      return Promise.resolve(
        new Response(
          JSON.stringify({
            success: true,
            emission: {
              data: {
                wiring: {
                  wiringId: "wiring-1",
                  wiringKey: "test-wiring",
                  wiringKind: nextWiringKind,
                  targetSurface: req.payload?.targetSurface ?? "route",
                  targetRef: req.payload?.targetRef ?? null,
                },
              },
            },
          }),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      );
    }

    if (layer === "ui_topology" && action === "list_package_components") {
      // A component of kind "admin_runtime" is what makes "admin_runtime" a selectable
      // wiringKind option in PackageWiringEditor's own <select> (buildWiringKindSelectOptions
      // merges packageComponents' componentKind values in verbatim) -- mirrors how a real
      // admin_runtime-destined package actually offers this option in production.
      return Promise.resolve(
        new Response(
          JSON.stringify({
            success: true,
            emission: {
              data: [{ componentId: "comp-1", componentKey: "admin_op_button", componentKind: "admin_runtime" }],
            },
          }),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      );
    }

    // list_admin_runtime_target_ref_authoring_candidates and anything else this render pulls in:
    // generic empty-but-successful response.
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, emission: { data: [] } }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
  };
}

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

// deno-lint-ignore no-explicit-any
function baseProps(overrides: Record<string, unknown> = {}): any {
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
  return {
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
    ...overrides,
  };
}

Deno.test("LayoutRightDock: PackageWiringEditor save syncs NodeEventAuthoringPanel's override gate live, without remount", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  const state = { wiringKind: "external_port" };
  try {
    globalThis.fetch = makeDispatchFetch(state);
    render(h(LayoutRightDock, baseProps()), container);
    await flushUpdates();
    await flushUpdates();

    // Open both accordions: 内部API（パッケージ接続・ルート遷移） (holds PackageWiringEditor) and
    // 操作イベント (holds NodeEventAuthoringPanel).
    accordionTriggerByTitle(container, "内部API").dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();
    accordionTriggerByTitle(container, "操作イベント").dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();

    // Before any save: wiringKind is "external_port" (not admin_runtime) and no overrides are
    // authored yet -- the admin_runtime override section must not render at all.
    let html = container.innerHTML;
    assertFalse(html.includes("管理操作の上書き"), "override section must not render before wiringKind is admin_runtime");

    // Drive PackageWiringEditor's real save flow: switch wiringKind to "admin_runtime" via its own
    // <select>, click its own save button, then click its own real ConfirmDialog's confirm button.
    const wiringKindSelect = Array.from(container.querySelectorAll("select")).find((s) =>
      Array.from(s.querySelectorAll("option")).some((o) => o.textContent?.includes("admin_runtime") || (o as HTMLOptionElement).value === "admin_runtime")
    ) as HTMLSelectElement | undefined;
    assertExists(wiringKindSelect, "wiringKind select not found");
    wiringKindSelect.value = "admin_runtime";
    wiringKindSelect.dispatchEvent(new Event("input", { bubbles: true }));
    wiringKindSelect.dispatchEvent(new Event("change", { bubbles: true }));
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

    // After the save resolves (still the SAME mounted component instance -- no remount, no
    // packageId change): NodeEventAuthoringPanel's gate must reflect the fresh wiringKind
    // immediately, proving the sibling-save invalidation path works.
    html = container.innerHTML;
    assert(html.includes("管理操作の上書き"), "override section must render once wiringKind becomes admin_runtime via a live sibling save");
    assertFalse(html.includes("admin_runtimeではないため"), "override editing must not still show the non-admin_runtime disabled reason after the save");
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});
