import { assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  getActiveStepIds,
  UI_BUILDER_FLOW_STEPS,
  type UiBuilderTabId,
} from "../components/UiBuilderFlowStepper.tsx";

Deno.test("UI_BUILDER_FLOW_STEPS: has package + layout/design + verify steps (SSOT 4.1 / 4.2)", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS.length, 4);
});

Deno.test("UI_BUILDER_FLOW_STEPS: step IDs include the design-settings subphase", () => {
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

Deno.test("step 2 uses placement vocabulary without raw tab ids", () => {
  const s2 = UI_BUILDER_FLOW_STEPS[1];
  assertEquals(s2.label, "配置");
  assertEquals(s2.detail.includes("デザイン設定"), true);
  assertEquals(s2.detail.toLowerCase().includes("layout_patch"), false);
});

Deno.test("step 2.5 uses design-settings label without component design phrase", () => {
  const designStep = UI_BUILDER_FLOW_STEPS.find((s) => s.id === 2.5)!;
  assertEquals(designStep.label.includes("デザイン設定"), true);
  assertEquals(designStep.label.toLowerCase().includes("component design"), false);
});

Deno.test("step 1 detail avoids submit and internal promote vocabulary", () => {
  const s1 = UI_BUILDER_FLOW_STEPS[0];
  assertEquals(s1.detail.toLowerCase().includes("submit"), false);
  assertEquals(s1.detail.includes("promote"), false);
  assertEquals(s1.detail.includes("バケット"), false);
});

Deno.test("step 3 external href is /demo", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS[3].externalHref, "/demo");
});

Deno.test("no step has both tabTarget and externalHref", () => {
  for (const step of UI_BUILDER_FLOW_STEPS) {
    assertFalse(Boolean(step.tabTarget) && Boolean(step.externalHref));
  }
});
