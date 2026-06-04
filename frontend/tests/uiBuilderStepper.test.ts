import { assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  getActiveStepIds,
  UI_BUILDER_FLOW_STEPS,
  type UiBuilderTabId,
} from "../components/UiBuilderFlowStepper.tsx";

Deno.test("UI_BUILDER_FLOW_STEPS: has package + layout/design + verify steps (SSOT 4.1 / 4.2)", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS.length, 4);
});

Deno.test("UI_BUILDER_FLOW_STEPS: step IDs include the component design subphase", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS.map((s) => s.id), [1, 2, 2.5, 3]);
});

Deno.test("getActiveStepIds: bucket tab activates step 1 only", () => {
  assertEquals(getActiveStepIds("bucket"), [1]);
});

Deno.test("getActiveStepIds: layout tab activates step 2", () => {
  assertEquals(getActiveStepIds("layout"), [2]);
});

Deno.test("getActiveStepIds: css tab activates layout and design subphases", () => {
  assertEquals(getActiveStepIds("css"), [2, 2.5]);
});

Deno.test("step 1 label: 部品選択でパッケージ化", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS[0].label, "部品選択でパッケージ化");
});

Deno.test("step 1 notes catalog is reference-only", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS[0].note?.includes("参照専用"), true);
});

Deno.test("step 2 mentions layout and component design", () => {
  const s2 = UI_BUILDER_FLOW_STEPS[1];
  assertEquals(s2.label.includes("layout"), true);
  assertEquals(s2.detail.includes("design"), true);
});

Deno.test("step 3 external href is /demo", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS[3].externalHref, "/demo");
});

Deno.test("no step has both tabTarget and externalHref", () => {
  for (const step of UI_BUILDER_FLOW_STEPS) {
    assertFalse(Boolean(step.tabTarget) && Boolean(step.externalHref));
  }
});
