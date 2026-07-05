import { assertEquals, assertThrows } from "jsr:@std/assert";
import {
  applyOverlayIntentToInteraction,
  defaultExternalPortInteraction,
  defaultOverlayOpenInteraction,
  expectedDisclosureTargetKind,
  friendlyOverlayTargetLabel,
  isActionLikeComponentKind,
  listOverlayTargetNodes,
  resolveDisclosureActionType,
} from "../lib/runtimeInteractionAuthoring.ts";
import { findRuntimeInteractionPatchErrors } from "../runtime/visualLayoutUtils.ts";

Deno.test("defaultExternalPortInteraction: click trigger", () => {
  assertEquals(defaultExternalPortInteraction(), {
    trigger: "click",
    actionType: "dispatchExternalPort",
  });
});

Deno.test("isActionLikeComponentKind: action and form inputs", () => {
  assertEquals(isActionLikeComponentKind("action/button"), true);
  assertEquals(isActionLikeComponentKind("disclosure/modal"), false);
});

Deno.test("resolveDisclosureActionType: disclosure kinds only", () => {
  assertEquals(resolveDisclosureActionType("open", "disclosure/modal"), "openModal");
  assertEquals(resolveDisclosureActionType("open", "disclosure/drawer"), "openDrawer");
  assertThrows(
    () => resolveDisclosureActionType("open", "table_op/row_detail_drawer"),
    Error,
    "unsupported componentKind",
  );
});

Deno.test("expectedDisclosureTargetKind: matches backend validation", () => {
  assertEquals(expectedDisclosureTargetKind("openDrawer"), "disclosure/drawer");
  assertEquals(expectedDisclosureTargetKind("toggleModal"), "disclosure/modal");
});

Deno.test("listOverlayTargetNodes: excludes non-disclosure drawer shells", () => {
  const targets = listOverlayTargetNodes([
    { nodeId: "legacy", componentKind: "table_op/row_detail_drawer" },
    { nodeId: "audit", componentKind: "inline_edit/audit_diff_drawer" },
    { nodeId: "drawer", componentKind: "disclosure/drawer" },
    { nodeId: "modal", componentKind: "disclosure/modal" },
  ]);
  assertEquals(targets.map((t) => t.nodeId), ["drawer", "modal"]);
});

Deno.test("defaultOverlayOpenInteraction: prefers modal on canvas", () => {
  const wiring = defaultOverlayOpenInteraction([
    { nodeId: "legacy", componentKind: "table_op/row_detail_drawer" },
    { nodeId: "modal", componentKind: "disclosure/modal", propsJson: '{"title":"詳細"}' },
  ]);
  assertEquals(wiring.actionType, "openModal");
  assertEquals(wiring.targetNodeId, "modal");
});

Deno.test("applyOverlayIntentToInteraction: ignores non-disclosure drawer targets", () => {
  const interaction = {
    trigger: "click",
    actionType: "openModal",
    targetNodeId: "legacy",
    statePath: "open",
  };
  const next = applyOverlayIntentToInteraction(
    interaction,
    "open",
    { nodeId: "legacy", componentKind: "table_op/row_detail_drawer" },
  );
  assertEquals(next, interaction);
});

Deno.test("applyOverlayIntentToInteraction: retargeting disclosure drawer switches action", () => {
  const next = applyOverlayIntentToInteraction(
    { trigger: "click", actionType: "openModal", targetNodeId: "drawer", statePath: "open" },
    "open",
    { nodeId: "drawer", componentKind: "disclosure/drawer" },
  );
  assertEquals(next.actionType, "openDrawer");
});

Deno.test("friendlyOverlayTargetLabel: uses Japanese surface + title", () => {
  const label = friendlyOverlayTargetLabel({
    nodeId: "n1",
    componentKind: "disclosure/modal",
    propsJson: '{"title":"ユーザー詳細"}',
  });
  assertEquals(label, "モーダル：ユーザー詳細");
});

Deno.test("findRuntimeInteractionPatchErrors: rejects openDrawer targeting row_detail_drawer", () => {
  const errors = findRuntimeInteractionPatchErrors([
    {
      nodeId: "button-1",
      componentKey: "button.primitive",
      componentKind: "action/button",
      runtimeInteractions: [{
        trigger: "click",
        actionType: "openDrawer",
        targetNodeId: "drawer-1",
        statePath: "open",
      }],
    },
    {
      nodeId: "drawer-1",
      componentKey: "row_detail_drawer.primitive",
      componentKind: "table_op/row_detail_drawer",
    },
  ]);
  assertEquals(errors.length, 1);
  assertEquals(
    errors[0],
    'button.primitive #1: overlay 対象 componentKind が disclosure/drawer と一致しません (table_op/row_detail_drawer)',
  );
});
