import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  ACCEPTANCE_FLOW_STEPS,
  ADMIN_MAIN_FLOW_STEPS,
} from "../content/adminGuides.ts";

Deno.test("ADMIN_MAIN_FLOW_STEPS: manifest before import", () => {
  const labels = ADMIN_MAIN_FLOW_STEPS.map((s) => s.label);
  assertEquals(labels, ["ログイン", "マニフェスト", "インポート", "UI Builder", "Runtime確認"]);
  const manifestIdx = labels.indexOf("マニフェスト");
  const importIdx = labels.indexOf("インポート");
  assertEquals(manifestIdx < importIdx, true);
});

Deno.test("ACCEPTANCE_FLOW_STEPS matches main flow order", () => {
  assertEquals(
    ACCEPTANCE_FLOW_STEPS.map((s) => s.label),
    ADMIN_MAIN_FLOW_STEPS.map((s) => s.label),
  );
});

Deno.test("ACCEPTANCE_FLOW_STEPS: import href after manifests", () => {
  const manifest = ACCEPTANCE_FLOW_STEPS.find((s) => s.label === "マニフェスト")!;
  const imp = ACCEPTANCE_FLOW_STEPS.find((s) => s.label === "インポート")!;
  assertEquals(manifest.href, "/admin/manifests");
  assertEquals(imp.href, "/admin/import");
  assertEquals(manifest.step < imp.step, true);
});
