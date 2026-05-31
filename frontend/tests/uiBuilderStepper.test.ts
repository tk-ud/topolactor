import { assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  getActiveStepIds,
  UI_BUILDER_FLOW_STEPS,
  type UiBuilderTabId,
} from "../components/UiBuilderFlowStepper.tsx";

// ─── step count ───────────────────────────────────────────────────────────────

Deno.test("UI_BUILDER_FLOW_STEPS: has exactly 5 steps", () => {
  assertEquals(UI_BUILDER_FLOW_STEPS.length, 5);
});

Deno.test("UI_BUILDER_FLOW_STEPS: step IDs are 1 through 5", () => {
  const ids = UI_BUILDER_FLOW_STEPS.map((s) => s.id);
  assertEquals(ids, [1, 2, 3, 4, 5]);
});

// ─── tab → active step mapping ────────────────────────────────────────────────

Deno.test("getActiveStepIds: bucket tab activates steps 1 and 2", () => {
  assertEquals(getActiveStepIds("bucket"), [1, 2]);
});

Deno.test("getActiveStepIds: layout tab activates steps 3 and 4", () => {
  assertEquals(getActiveStepIds("layout"), [3, 4]);
});

Deno.test("getActiveStepIds: other tabs return empty array", () => {
  const others: UiBuilderTabId[] = ["ci", "catalog", "css"];
  for (const tab of others) {
    assertEquals(
      getActiveStepIds(tab),
      [],
      `expected empty active steps for tab: ${tab}`,
    );
  }
});

// ─── step spec integrity ──────────────────────────────────────────────────────

Deno.test("flow steps 1 and 2 target the bucket tab", () => {
  const s1 = UI_BUILDER_FLOW_STEPS.find((s) => s.id === 1)!;
  const s2 = UI_BUILDER_FLOW_STEPS.find((s) => s.id === 2)!;
  assertEquals(s1.tabTarget, "bucket");
  assertEquals(s2.tabTarget, "bucket");
});

Deno.test("flow steps 3 and 4 target the layout tab", () => {
  const s3 = UI_BUILDER_FLOW_STEPS.find((s) => s.id === 3)!;
  const s4 = UI_BUILDER_FLOW_STEPS.find((s) => s.id === 4)!;
  assertEquals(s3.tabTarget, "layout");
  assertEquals(s4.tabTarget, "layout");
});

Deno.test("flow step 5 has external href pointing to /admin/runtime", () => {
  const s5 = UI_BUILDER_FLOW_STEPS.find((s) => s.id === 5)!;
  assertEquals(s5.externalHref, "/admin/runtime");
  assertEquals(s5.tabTarget, undefined);
});

Deno.test("flow step 3 has draft-only warning note", () => {
  const s3 = UI_BUILDER_FLOW_STEPS.find((s) => s.id === 3)!;
  // Must mention draft-only apply block
  assertEquals(
    typeof s3.note === "string" && s3.note.length > 0,
    true,
    "step 3 must include a note about draft-only apply block",
  );
});

// ─── step detail completeness ─────────────────────────────────────────────────

Deno.test("all steps have non-empty label and detail", () => {
  for (const step of UI_BUILDER_FLOW_STEPS) {
    assertEquals(step.label.length > 0, true, `step ${step.id} label must not be empty`);
    assertEquals(step.detail.length > 0, true, `step ${step.id} detail must not be empty`);
  }
});

// ─── step 4 includes preview/validate/apply terms ────────────────────────────

Deno.test("step 4 detail mentions preview, validate, and save-apply flow", () => {
  const s4 = UI_BUILDER_FLOW_STEPS.find((s) => s.id === 4)!;
  assertEquals(s4.detail.includes("プレビュー"), true, "step 4 must mention プレビュー");
  assertEquals(s4.detail.includes("検証"), true, "step 4 must mention 検証");
  assertEquals(s4.detail.includes("保存反映"), true, "step 4 must mention 保存反映");
  assertEquals(s4.label, "確認して保存反映");
});

// ─── step 2 mentions placement-ready flow ────────────────────────────────────

Deno.test("step 2 label and detail describe placement-ready state", () => {
  const s2 = UI_BUILDER_FLOW_STEPS.find((s) => s.id === 2)!;
  assertEquals(s2.label, "配置できる状態にする");
  assertEquals(s2.detail.includes("generate"), true, "step 2 technical note mentions generate");
  assertEquals(s2.detail.includes("promote"), true, "step 2 technical note mentions promote");
});

// ─── no step has both tabTarget and externalHref ─────────────────────────────

Deno.test("no step has both tabTarget and externalHref", () => {
  for (const step of UI_BUILDER_FLOW_STEPS) {
    assertFalse(
      Boolean(step.tabTarget) && Boolean(step.externalHref),
      `step ${step.id} must not have both tabTarget and externalHref`,
    );
  }
});
