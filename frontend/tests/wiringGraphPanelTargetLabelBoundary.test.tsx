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
  "WiringGraphPanel (real mount): a localStateMutation edge resolves its ui-local: targetRef to the real target node's friendly componentKey, never the raw carrier string",
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
        primary.includes("disclosure/panel"),
        "the real target node's componentKey must resolve as the friendly primary target text (the parse bug previously made this lookup always miss)",
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
