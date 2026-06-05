import { assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  getActiveFlowStepIds,
  UI_BUILDER_CANVAS_FLOW,
  UI_BUILDER_FLOW_LABELS,
  type UiBuilderFlowStepId,
} from "../components/UiBuilderFlowStepper.tsx";

Deno.test("UI_BUILDER_CANVAS_FLOW: unified canvas flow has 3 steps", () => {
  assertEquals(UI_BUILDER_CANVAS_FLOW.length, 3);
});

Deno.test("UI_BUILDER_CANVAS_FLOW: step 1 is route selection", () => {
  assertEquals(UI_BUILDER_CANVAS_FLOW[0].stepId, "route");
  assertEquals(UI_BUILDER_CANVAS_FLOW[0].label.includes("ルート"), true);
});

Deno.test("UI_BUILDER_CANVAS_FLOW: step 2 is canvas workspace edit", () => {
  assertEquals(UI_BUILDER_CANVAS_FLOW[1].stepId, "canvas_edit");
  assertEquals(UI_BUILDER_CANVAS_FLOW[1].label.includes("canvas"), true);
});

Deno.test("getActiveFlowStepIds: route step activates step 1 only", () => {
  assertEquals(getActiveFlowStepIds("route"), [1]);
});

Deno.test("getActiveFlowStepIds: canvas_edit activates step 2 only", () => {
  assertEquals(getActiveFlowStepIds("canvas_edit"), [2]);
});

Deno.test("UI_BUILDER_FLOW_LABELS: no legacy package-selection phase label", () => {
  const labels = Object.values(UI_BUILDER_FLOW_LABELS).join(" ");
  assertFalse(labels.includes("部品選択でパッケージ化"));
});

Deno.test("canvas workspace: no separate layout/design/visual tabs in flow model", () => {
  const stepIds = UI_BUILDER_CANVAS_FLOW.map((s) => s.stepId);
  assertFalse(stepIds.includes("layout" as UiBuilderFlowStepId));
  assertFalse(stepIds.includes("design" as UiBuilderFlowStepId));
  assertFalse(stepIds.includes("visual" as UiBuilderFlowStepId));
});

Deno.test("canvas workspace: step 2 detail describes implicit registration on drop", () => {
  const detail = UI_BUILDER_CANVAS_FLOW[1].detail;
  assertEquals(typeof detail, "string");
  assertEquals(detail.includes("ドロップ"), true);
});
