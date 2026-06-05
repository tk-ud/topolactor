import { assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  getActiveStepIds,
  UI_BUILDER_FLOW_STEPS,
  type UiBuilderTabId,
} from "../components/UiBuilderFlowStepper.tsx";

Deno.test("UI_BUILDER_FLOW_STEPS: SSOT v0.8.0 four surfaces + demo handoff", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS.length, 5);
});

Deno.test("UI_BUILDER_FLOW_STEPS: step IDs include visual view surface", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS.map((s) => s.id), [1, 2, 3, 4, 5]);
});

Deno.test("getActiveStepIds: bucket tab activates step 1 only", () => {
  assertEquals(getActiveStepIds("bucket"), [1]);
});

Deno.test("getActiveStepIds: layout tab activates step 2", () => {
  assertEquals(getActiveStepIds("layout"), [2]);
});

Deno.test("getActiveStepIds: design tab activates step 3", () => {
  assertEquals(getActiveStepIds("design"), [3]);
});

Deno.test("getActiveStepIds: visual tab activates step 4", () => {
  assertEquals(getActiveStepIds("visual"), [4]);
});

Deno.test("step 1 label: package決定", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS[0].label, "package決定");
});

Deno.test("step 4 label: visual view", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS[3].label, "visual view");
  assertEquals(UI_BUILDER_FLOW_STEPS[3].tabTarget, "visual");
});

Deno.test("step 5 external href is /demo", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS[4].externalHref, "/demo");
});

Deno.test("no step has both tabTarget and externalHref", () => {
  for (const step of UI_BUILDER_FLOW_STEPS) {
    assertFalse(Boolean(step.tabTarget) && Boolean(step.externalHref));
  }
});
