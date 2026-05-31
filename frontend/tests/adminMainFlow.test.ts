import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  ACCEPTANCE_FLOW_STEPS,
  ADMIN_MAIN_FLOW_STEPS,
} from "../content/adminGuides.ts";
import { UX_MAIN_FLOW_STEP_LABELS } from "../content/adminUxTerms.ts";

Deno.test("ADMIN_MAIN_FLOW_STEPS: import settings before import", () => {
  assertEquals(
    ADMIN_MAIN_FLOW_STEPS.map((s) => s.label),
    [...UX_MAIN_FLOW_STEP_LABELS],
  );
  const settingsIdx = UX_MAIN_FLOW_STEP_LABELS.indexOf("取り込み設定");
  const importIdx = UX_MAIN_FLOW_STEP_LABELS.indexOf("インポート");
  assertEquals(settingsIdx < importIdx, true);
});

Deno.test("ACCEPTANCE_FLOW_STEPS matches main flow order", () => {
  assertEquals(
    ACCEPTANCE_FLOW_STEPS.map((s) => s.label),
    ADMIN_MAIN_FLOW_STEPS.map((s) => s.label),
  );
});

Deno.test("ACCEPTANCE_FLOW_STEPS: import settings href before import", () => {
  const settings = ACCEPTANCE_FLOW_STEPS.find((s) => s.label === "取り込み設定")!;
  const imp = ACCEPTANCE_FLOW_STEPS.find((s) => s.label === "インポート")!;
  assertEquals(settings.href, "/admin/manifests");
  assertEquals(imp.href, "/admin/import");
  assertEquals(settings.step < imp.step, true);
});
