// frontend/tests/wiringGraphPanelTargetLabelBoundary.test.tsx
//
// Production-composition DOM proof (real WiringGraphPanel mount with real
// runtimeInteractions/internalApiWirings data, not a synthetic helper call) that the
// canonical wiring inspector's normal edge display never shows raw dispatcher-shaped
// carrier strings (external-port:.../instance-port:.../ui-local:.../a package-wiring
// manifest UUID) as primary meaning. WiringGraphPanel is a directly-reachable
// first-class canvas mode (the "配線" tab, sibling to "レイアウト" — not gated behind
// an advanced-only disclosure), so it is squarely a normal operator-facing surface for
// this Bundle's label-boundary contract, not an implicitly-exempt "advanced path".
//
// Round-7 finding: edgeTargetLabel's non-node branch returned the raw edge.targetRef
// string verbatim as primary text for external_port/instance_port/internal_api edges,
// and its node branch never parsed a localStateMutation edge's "ui-local:<nodeId>.
// <stateKey>" targetRef before the node lookup — so that lookup always missed and fell
// through to the SAME raw carrier string. Both are fixed; this file proves the fix
// with real projection data, and proves the raw reference stays reachable in a
// per-edge 技術情報 disclosure (never deleted — diagnosability preserved).
//
// Round-8 finding: the SAME panel's edge sourceLabel, node palette, and UI監視割当
// (watchBindings) display still fell back to the raw structural nodeId whenever no
// componentKey happened to resolve something "friendlier" -- but componentKey||nodeId
// was never real display authority, just implementation consistency. A real authority
// DOES exist: wiringNodeDisplayLabel/friendlyComponentLabel (lib/uiBuilderWiringProjection
// .ts), the SAME function islands/UiBuilderAdmin.tsx's Layer Tree, panel titles, and
// undo-history labels already use as the canonical per-node display name for this exact
// canvas workspace. This file adds proof that WiringGraphPanel now uses that SAME
// authority (never a new frontend-invented name, never raw nodeId as primary), and adds
// the external_instance_integration branch proof this Bundle's audit found missing.

import { assert, assertFalse } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { h, options, render } from "preact";
import { flushUpdates, setupDom } from "./test-dom-setup.ts";
import WiringGraphPanel from "../components/WiringGraphPanel.tsx";
import type { WiringNode } from "../lib/uiBuilderWiringProjection.ts";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

function visibleText(container: Element): string {
  const clone = container.cloneNode(true) as Element;
  for (const details of Array.from(clone.querySelectorAll("details"))) {
    details.remove();
  }
  return clone.textContent ?? "";
}

function technicalDisclosureText(container: Element): string {
  return Array.from(container.querySelectorAll("details")).map((d) => d.textContent ?? "").join("\n");
}

Deno.test(
  "WiringGraphPanel (real mount): an external_api_integration edge's raw portTargetRef never appears in normal primary text, only in a 技術情報 disclosure",
  async () => {
    const { container, cleanup } = setupDom();
    const nodes: WiringNode[] = [{
      nodeId: "btn-1",
      componentKey: "action/button",
      runtimeInteractions: [{
        trigger: "click",
        actionType: "dispatchExternalPort",
        portTargetRef: "external-port:access_port:77777777-7777-7777-7777-777777777777",
      }],
    }];
    try {
      render(h(WiringGraphPanel, { nodes, selectedNodeId: null, onSelectNode: () => {}, onApplyNodes: () => {} }), container);
      await flushUpdates();

      const primary = visibleText(container);
      assert(primary.includes("外部APIに接続済み"), "a friendly primary text must describe the connection kind");
      assertFalse(
        primary.includes("external-port:"),
        "the raw dispatcher-shaped portTargetRef must never appear in always-visible primary text",
      );
      assertFalse(
        primary.includes("77777777-7777-7777-7777-777777777777"),
        "the raw port UUID must never appear in always-visible primary text",
      );

      const technical = technicalDisclosureText(container);
      assert(
        technical.includes("external-port:access_port:77777777-7777-7777-7777-777777777777"),
        "the raw portTargetRef must still be reachable inside a 技術情報 disclosure",
      );
    } finally {
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "WiringGraphPanel (real mount): a localStateMutation edge resolves its ui-local: targetRef to the real target node's friendly display label (same authority as the canvas Layer Tree), never the raw carrier string",
  async () => {
    const { container, cleanup } = setupDom();
    const nodes: WiringNode[] = [
      {
        nodeId: "toggle-src",
        componentKey: "form_input/checkbox",
        runtimeInteractions: [{
          trigger: "change",
          actionType: "localStateMutation",
          targetRef: "ui-local:panel-1.expanded",
        }],
      },
      { nodeId: "panel-1", componentKey: "disclosure/panel" },
    ];
    try {
      render(h(WiringGraphPanel, { nodes, selectedNodeId: null, onSelectNode: () => {}, onApplyNodes: () => {} }), container);
      await flushUpdates();

      const primary = visibleText(container);
      assert(
        primary.includes("panel"),
        "the real target node's friendly display label (last catalog path segment, via wiringNodeDisplayLabel/friendlyComponentLabel — the same authority the canvas Layer Tree uses) must resolve as the primary target text (the parse bug previously made this lookup always miss)",
      );
      assertFalse(
        primary.includes("disclosure/panel"),
        "the full schema/catalog componentKey path must not appear verbatim in always-visible primary text — only its friendly last-segment label",
      );
      assertFalse(
        primary.includes("ui-local:panel-1.expanded"),
        "the raw ui-local: carrier string must never appear in always-visible primary text",
      );

      const technical = technicalDisclosureText(container);
      assert(
        technical.includes("ui-local:panel-1.expanded"),
        "the raw ui-local: carrier must still be reachable inside a 技術情報 disclosure",
      );
    } finally {
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "WiringGraphPanel (real mount): the 内部API package-wiring lane's raw manifest-referencing targetRef never appears in normal primary text, only in disclosure",
  async () => {
    const { container, cleanup } = setupDom();
    const nodes: WiringNode[] = [{ nodeId: "n-1", componentKey: "form_input/text" }];
    try {
      render(
        h(WiringGraphPanel, {
          nodes,
          selectedNodeId: null,
          onSelectNode: () => {},
          onApplyNodes: () => {},
          internalApiWirings: [{
            wiringKey: "orders_list_navigate",
            targetRef: "manifest:88888888-8888-8888-8888-888888888888:orders_list_navigate",
          }],
        }),
        container,
      );
      await flushUpdates();

      const primary = visibleText(container);
      assert(primary.includes("内部APIパッケージに接続済み"), "a friendly primary text must describe the connection kind");
      assertFalse(
        primary.includes("88888888-8888-8888-8888-888888888888"),
        "the raw manifest UUID must never appear in always-visible primary text",
      );

      const technical = technicalDisclosureText(container);
      assert(
        technical.includes("manifest:88888888-8888-8888-8888-888888888888:orders_list_navigate"),
        "the raw package targetRef must still be reachable inside a 技術情報 disclosure",
      );
    } finally {
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "WiringGraphPanel (real mount): an external_instance_integration edge's raw instanceTargetRef never appears in normal primary text, only in a 技術情報 disclosure",
  async () => {
    const { container, cleanup } = setupDom();
    const nodes: WiringNode[] = [{
      nodeId: "btn-instance-1",
      componentKey: "action/button",
      runtimeInteractions: [{
        trigger: "click",
        actionType: "dispatchInstanceOperation",
        instanceTargetRef: "instance-port:runtime_instance_port:99999999-9999-9999-9999-999999999999:refresh",
      }],
    }];
    try {
      render(h(WiringGraphPanel, { nodes, selectedNodeId: null, onSelectNode: () => {}, onApplyNodes: () => {} }), container);
      await flushUpdates();

      const primary = visibleText(container);
      assert(primary.includes("外部インスタンスに接続済み"), "a friendly primary text must describe the connection kind");
      assertFalse(
        primary.includes("instance-port:"),
        "the raw dispatcher-shaped instanceTargetRef must never appear in always-visible primary text",
      );
      assertFalse(
        primary.includes("99999999-9999-9999-9999-999999999999"),
        "the raw instance port UUID must never appear in always-visible primary text",
      );

      const technical = technicalDisclosureText(container);
      assert(
        technical.includes("instance-port:runtime_instance_port:99999999-9999-9999-9999-999999999999:refresh"),
        "the raw instanceTargetRef must still be reachable inside a 技術情報 disclosure",
      );
    } finally {
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "WiringGraphPanel (real mount): edge sourceLabel, node palette, and UI監視割当 all use the SAME friendly display authority the canvas Layer Tree uses — never the raw structural nodeId as primary",
  async () => {
    const { container, cleanup } = setupDom();
    // Two distinct nodes (not a self-loop) so this test's own claim -- friendly
    // sourceLabel/palette/watch-binding resolution -- isn't conflated with the
    // separately-decided SIDE_EFFECT_DIRECT_SELF_LOOP diagnostic message's own
    // raw-nodeId-inline convention (that policy-error surface is authoring-time
    // linter output, not a normal browsing label, and is intentionally out of
    // this test's scope).
    const nodes: WiringNode[] = [
      {
        nodeId: "11111111-aaaa-bbbb-cccc-222222222222",
        componentKey: "form_input/checkbox",
        stateJson: JSON.stringify({ checked: false }),
        runtimeInteractions: [{
          trigger: "click",
          actionType: "setState",
          targetNodeId: "55555555-eeee-ffff-aaaa-666666666666",
        }],
      },
      { nodeId: "55555555-eeee-ffff-aaaa-666666666666", componentKey: "display/badge" },
    ];
    try {
      render(h(WiringGraphPanel, { nodes, selectedNodeId: null, onSelectNode: () => {}, onApplyNodes: () => {} }), container);
      await flushUpdates();

      const primary = visibleText(container);
      // friendlyComponentLabel("form_input/checkbox") -> "checkbox" / ("display/badge") ->
      // "badge", the same last-segment resolution the Layer Tree/palette use for these
      // exact nodes elsewhere in the canvas.
      assert(primary.includes("checkbox"), "sourceLabel/palette/watch-binding must show the friendly display label");
      assert(primary.includes("badge"), "the resolved edge target must also show the friendly display label");
      assertFalse(
        primary.includes("11111111-aaaa-bbbb-cccc-222222222222"),
        "the raw source nodeId must never appear in always-visible primary text",
      );
      assertFalse(
        primary.includes("55555555-eeee-ffff-aaaa-666666666666"),
        "the raw target nodeId must never appear in always-visible primary text",
      );

      const technical = technicalDisclosureText(container);
      assert(
        technical.includes("11111111-aaaa-bbbb-cccc-222222222222"),
        "the raw source nodeId must still be reachable inside a 技術情報 disclosure (edge source, palette, and/or watch-binding)",
      );
      assert(
        technical.includes("55555555-eeee-ffff-aaaa-666666666666"),
        "the raw target nodeId must still be reachable inside a 技術情報 disclosure (edge target)",
      );
    } finally {
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "WiringGraphPanel (real mount): a node with no componentKey at all shows a fail-close placeholder, never the raw nodeId, as its friendly label",
  async () => {
    const { container, cleanup } = setupDom();
    const nodes: WiringNode[] = [{ nodeId: "33333333-dddd-eeee-ffff-444444444444" }];
    try {
      render(h(WiringGraphPanel, { nodes, selectedNodeId: null, onSelectNode: () => {}, onApplyNodes: () => {} }), container);
      await flushUpdates();

      const primary = visibleText(container);
      assert(primary.includes("名称未設定"), "a fail-close placeholder must show when no componentKey exists to resolve a label from");
      assertFalse(
        primary.includes("33333333-dddd-eeee-ffff-444444444444"),
        "the raw nodeId must never be promoted to primary text just because componentKey is absent",
      );

      const technical = technicalDisclosureText(container);
      assert(
        technical.includes("33333333-dddd-eeee-ffff-444444444444"),
        "the raw nodeId must still be reachable for diagnostics inside a 技術情報 disclosure",
      );
    } finally {
      render(null, container);
      cleanup();
    }
  },
);
