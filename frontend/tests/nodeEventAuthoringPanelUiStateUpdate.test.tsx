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

Deno.test("NodeEventAuthoringPanel ui_state_update: a localStateMutation interaction stays visible/deletable as a READ-ONLY technical row — no raw free-text target editor", async () => {
  // SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml ui_event_settings.setting_category_taxonomy
  // .frontend_side.ui_state_update.action_types does NOT include localStateMutation (a separate,
  // seed-authored internal_instance_wiring carrier from a different SSOT). Promoting it to a
  // normally-editable row with a free-text targetRef input would let an author hand-write a target
  // outside stateUpdateTargets, bypassing selectableWriteTargets / side_effect_cycle_policy's
  // dependency-closure filtering entirely — so it must render read-only (technical disclosure),
  // never as an editable <input>.
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
    assert(html.includes("ui-local:filter_panel.selectedCategory"), "the target must remain visible");
    const inputs = Array.from(container.querySelectorAll("input")) as HTMLInputElement[];
    assertFalse(
      inputs.some((el) => el.value === "ui-local:filter_panel.selectedCategory"),
      "the ui-local target must NOT be a raw free-text editable input (bypasses dependency-closure policy)",
    );
    const deleteButtons = Array.from(container.querySelectorAll("button")).filter((b) =>
      b.textContent?.includes("削除")
    );
    assert(deleteButtons.length > 0, "must still be deletable — never silently invisible again");
  } finally {
    cleanup();
  }
});

Deno.test("NodeEventAuthoringPanel ui_state_update: localStateMutation is never a selectable option in the add/edit actionType dropdown", async () => {
  const { container, cleanup } = setupDom();
  try {
    // An existing, freely-editable setState interaction — its actionType <select> must offer
    // only the canonical SSOT action_types (setState/setActiveKey), never localStateMutation.
    const interactions: NodeEventWiring[] = [
      { trigger: "click", actionType: "setState", targetNodeId: "n-target", statePath: "selectedKey" },
    ];
    render(h(NodeEventAuthoringPanel, baseProps({ interactions })), container);
    await flushUpdates();
    const options = Array.from(container.querySelectorAll("option")).map((o) => o.getAttribute("value"));
    assertFalse(
      options.includes("localStateMutation"),
      "localStateMutation must never appear as a choosable actionType option",
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

Deno.test("NodeEventAuthoringPanel ui_state_update: statePath candidates are derived from the SELECTED TARGET node's own declared state, never the source node's", async () => {
  // The source node ("n-source") declares a slot the mutation must NOT offer; the target node
  // ("n-target") declares a different slot that IS the correct candidate for this mutation's
  // statePath, since a mutation writes to the TARGET's declared state, not the source's.
  const { container, cleanup } = setupDom();
  try {
    const interactions: NodeEventWiring[] = [
      { trigger: "click", actionType: "setState", targetNodeId: "n-target" },
    ];
    render(
      h(NodeEventAuthoringPanel, baseProps({
        interactions,
        sourceNodeId: "n-source",
        stateJson: JSON.stringify({ sourceOnlySlot: null }),
        allNodes: [
          { nodeId: "n-source", stateJson: JSON.stringify({ sourceOnlySlot: null }) },
          { nodeId: "n-target", stateJson: JSON.stringify({ targetOnlySlot: null }) },
        ],
      })),
      container,
    );
    await flushUpdates();
    const datalistOptions = Array.from(container.querySelectorAll("datalist option")).map((o) =>
      o.getAttribute("value")
    );
    assert(datalistOptions.includes("targetOnlySlot"), "the target node's own declared slot must be offered");
    assertFalse(
      datalistOptions.includes("sourceOnlySlot"),
      "the source node's declared slot must NOT leak into the target's statePath candidates",
    );
  } finally {
    cleanup();
  }
});

Deno.test("NodeEventAuthoringPanel ui_state_update: the target dropdown excludes a node inside the dependency closure (side_effect_cycle_policy negative case)", async () => {
  // A (source) already has a value-reactive "change" interaction writing to B (A -> B). B ALSO
  // has a value-reactive "change" interaction writing back to A (B -> A) — a real authored
  // indirect cycle. dependencyClosureOfTriggerSource(nodes, "A") is therefore {A, B}, and
  // selectableWriteTargets must exclude both, leaving only C selectable as a NEW write target
  // for a trigger sourced on A.
  const { container, cleanup } = setupDom();
  try {
    const allNodes = [
      {
        nodeId: "A",
        componentKey: "form_input/select",
        runtimeInteractions: [{ trigger: "change", actionType: "setState", targetNodeId: "B" }],
      },
      {
        nodeId: "B",
        componentKey: "form_input/select",
        runtimeInteractions: [{ trigger: "change", actionType: "setState", targetNodeId: "A" }],
      },
      { nodeId: "C", componentKey: "form_input/select", runtimeInteractions: [] },
    ];
    const interactions: NodeEventWiring[] = [
      { trigger: "click", actionType: "setState", targetNodeId: "C" },
    ];
    render(
      h(NodeEventAuthoringPanel, baseProps({
        interactions,
        sourceNodeId: "A",
        allNodes,
        targetNodes: [
          { nodeId: "A", componentKey: "form_input/select", componentKind: "form_input/select" },
          { nodeId: "B", componentKey: "form_input/select", componentKind: "form_input/select" },
          { nodeId: "C", componentKey: "form_input/select", componentKind: "form_input/select" },
        ],
      })),
      container,
    );
    await flushUpdates();
    // The target <select> is the second <select> in the row (trigger, actionType, target).
    const selects = Array.from(container.querySelectorAll("select"));
    const targetSelect = selects.find((s) =>
      Array.from(s.querySelectorAll("option")).some((o) => o.getAttribute("value") === "C")
    );
    assertExists(targetSelect, "the target select for this row must exist");
    const targetOptionValues = Array.from(targetSelect!.querySelectorAll("option")).map((o) =>
      o.getAttribute("value")
    );
    assert(targetOptionValues.includes("C"), "C is outside the dependency closure and must remain selectable");
    assertFalse(
      targetOptionValues.includes("B"),
      "B is inside the dependency closure of A (B -> A reaches the trigger source) and must be excluded",
    );
  } finally {
    cleanup();
  }
});
