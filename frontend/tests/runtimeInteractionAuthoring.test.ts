import { assertEquals } from "jsr:@std/assert";
import {
  applyOverlayIntentToInteraction,
  defaultExternalPortInteraction,
  defaultOverlayOpenInteraction,
  friendlyOverlayTargetLabel,
  isActionLikeComponentKind,
  resolveDisclosureActionType,
} from "../lib/runtimeInteractionAuthoring.ts";

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

Deno.test("resolveDisclosureActionType: modal vs drawer from componentKind", () => {
  assertEquals(resolveDisclosureActionType("open", "disclosure/modal"), "openModal");
  assertEquals(resolveDisclosureActionType("open", "table_op/row_detail_drawer"), "openDrawer");
});

Deno.test("defaultOverlayOpenInteraction: prefers modal on canvas", () => {
  const wiring = defaultOverlayOpenInteraction([
    { nodeId: "drawer", componentKind: "table_op/row_detail_drawer" },
    { nodeId: "modal", componentKind: "disclosure/modal", propsJson: '{"title":"詳細"}' },
  ]);
  assertEquals(wiring.actionType, "openModal");
  assertEquals(wiring.targetNodeId, "modal");
});

Deno.test("applyOverlayIntentToInteraction: retargeting drawer switches action", () => {
  const next = applyOverlayIntentToInteraction(
    { trigger: "click", actionType: "openModal", targetNodeId: "drawer", statePath: "open" },
    "open",
    { nodeId: "drawer", componentKind: "table_op/row_detail_drawer" },
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
