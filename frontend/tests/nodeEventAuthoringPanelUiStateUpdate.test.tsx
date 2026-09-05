// frontend/tests/nodeEventAuthoringPanelUiStateUpdate.test.tsx
//
// Production-composition DOM proof (SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
// ui_event_settings.setting_category_taxonomy / admin-console-workflow-ssot.yaml
// layout_node_props_contract.descriptor actionType vocabulary).
//
// Before this round, NodeEventAuthoringPanel classified per-node interactions via the
// IMPLEMENTATION-only runtimeInteractionAuthoring.ts runtimeInteractionCategory (overlay /
// external_port / instance_operation / legacy). Any ui_state_update actionType outside the
// disclosure family (openModal/closeModal/... ) — setState, setActiveKey, localStateMutation —
// fell into "legacy" and rendered as `null`: invisible and unremovable in this authoring panel,
// even though wiringSettingCategoryOf() / WiringGraphPanel already classified and displayed them
// correctly as ui_state_update. This mounts the PRODUCTION component (not a synthetic helper call)
// and proves those interactions now get a real, editable row, and that a new one can be authored.

import { assert, assertEquals, assertExists, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import NodeEventAuthoringPanel, {
  type NodeEventWiring,
} from "../components/NodeEventAuthoringPanel.tsx";
import {
  UX_RUNTIME_INTERACTION_ADD_STATE_UPDATE,
  UX_RUNTIME_INTERACTION_LOCAL_STATE_TARGET_LABEL,
} from "../content/adminUxTerms.ts";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

// deno-lint-ignore no-explicit-any
function baseProps(overrides: Record<string, unknown> = {}): any {
  return {
    interactions: [],
    targetNodes: [
      { nodeId: "n-target", componentKey: "form_input/select", componentKind: "form_input/select" },
    ],
    triggerOptions: ["click", "change", "submit"],
    onCommit: () => {},
    ...overrides,
  };
}

Deno.test("NodeEventAuthoringPanel ui_state_update: a non-overlay setState interaction renders a real, editable row (not null)", async () => {
  const { container, cleanup } = setupDom();
  try {
    const interactions: NodeEventWiring[] = [
      { trigger: "click", actionType: "setState", targetNodeId: "n-target", statePath: "selectedKey" },
    ];
    render(
      h(NodeEventAuthoringPanel, baseProps({ interactions })),
      container,
    );
    await flushUpdates();
    // Controlled <input> values are a DOM property, not an HTML attribute, so they never appear
    // in innerHTML text — assert against the actual rendered element's .value instead.
    const inputs = Array.from(container.querySelectorAll("input")) as HTMLInputElement[];
    assert(
      inputs.some((el) => el.value === "selectedKey"),
      "the declared statePath must be rendered as the state-path input's value",
    );
    // A delete button must exist for this interaction — it must be manageable, not invisible.
    const deleteButtons = Array.from(container.querySelectorAll("button")).filter((b) =>
      b.textContent?.includes("削除")
    );
    assert(deleteButtons.length > 0, "a real row must expose a delete control");
  } finally {
    cleanup();
  }
});

Deno.test("NodeEventAuthoringPanel ui_state_update: a localStateMutation interaction renders its ui-local targetRef field", async () => {
  const { container, cleanup } = setupDom();
  try {
    const interactions: NodeEventWiring[] = [
      { trigger: "change", actionType: "localStateMutation", targetRef: "ui-local:filter_panel.selectedCategory" },
    ];
    render(
      h(NodeEventAuthoringPanel, baseProps({ interactions })),
      container,
    );
    await flushUpdates();
    const html = container.innerHTML;
    assert(html.includes(UX_RUNTIME_INTERACTION_LOCAL_STATE_TARGET_LABEL));
    const inputs = Array.from(container.querySelectorAll("input")) as HTMLInputElement[];
    assert(
      inputs.some((el) => el.value === "ui-local:filter_panel.selectedCategory"),
      "the ui-local targetRef must be rendered as the local-state-target input's value",
    );
  } finally {
    cleanup();
  }
});

Deno.test("NodeEventAuthoringPanel ui_state_update: '+ 状態の更新' commits a new setState interaction distinct from overlay disclosure", async () => {
  const { container, cleanup } = setupDom();
  let committed: NodeEventWiring[] | null = null;
  try {
    render(
      h(
        NodeEventAuthoringPanel,
        // deno-lint-ignore no-explicit-any
        baseProps({
          onCommit: (next: NodeEventWiring[]) => {
            committed = next;
          },
        } as any),
      ),
      container,
    );
    await flushUpdates();
    const addBtn = Array.from(container.querySelectorAll("button")).find((b) =>
      b.textContent === UX_RUNTIME_INTERACTION_ADD_STATE_UPDATE
    );
    assertExists(addBtn, "the generic state-update add button must render");
    addBtn!.dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    if (!committed) throw new Error("onCommit must have been called with the new interaction");
    const result = committed as NodeEventWiring[];
    assertEquals(result.length, 1);
    assertEquals(result[0].actionType, "setState");
    assertFalse(
      result[0].actionType.includes("Modal") || result[0].actionType.includes("Drawer") ||
        result[0].actionType.includes("Dialog"),
      "the generic state-update add path must not produce an overlay disclosure actionType",
    );
  } finally {
    cleanup();
  }
});
