import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  ACCEPTANCE_FLOW_STEPS,
  ADMIN_MAIN_FLOW_STEPS,
} from "../content/adminGuides.ts";
import { UX_MAIN_FLOW_STEP_LABELS } from "../content/adminUxTerms.ts";

Deno.test("ADMIN_MAIN_FLOW_STEPS: contents before data ingestion", () => {
  assertEquals(
    ADMIN_MAIN_FLOW_STEPS.map((s) => s.label),
    [...UX_MAIN_FLOW_STEP_LABELS],
  );
  const contentsIdx = UX_MAIN_FLOW_STEP_LABELS.indexOf("コンテンツ設定");
  const ingestionIdx = UX_MAIN_FLOW_STEP_LABELS.indexOf("データ取り込み");
  assertEquals(contentsIdx < ingestionIdx, true);
});

Deno.test("ACCEPTANCE_FLOW_STEPS matches main flow order", () => {
  assertEquals(
    ACCEPTANCE_FLOW_STEPS.map((s) => s.label),
    ADMIN_MAIN_FLOW_STEPS.map((s) => s.label),
  );
});

Deno.test("ACCEPTANCE_FLOW_STEPS: contents href before data ingestion", () => {
  const contents = ACCEPTANCE_FLOW_STEPS.find((s) => s.label === "コンテンツ設定")!;
  const ingestion = ACCEPTANCE_FLOW_STEPS.find((s) => s.label === "データ取り込み")!;
  assertEquals(contents.href, "/admin/contents");
  assertEquals(ingestion.href, "/admin/import");
  assertEquals(contents.step < ingestion.step, true);
});

Deno.test("ADMIN_MAIN_FLOW_STEPS: hub manifests after ui builder", () => {
  const uiIdx = UX_MAIN_FLOW_STEP_LABELS.indexOf("画面づくり");
  const hubIdx = UX_MAIN_FLOW_STEP_LABELS.indexOf("ハブ・画面群");
  assertEquals(uiIdx < hubIdx, true);
});
