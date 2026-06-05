import { assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  getActivePhaseIds,
  UI_BUILDER_CANVAS_PHASES,
  UI_BUILDER_PHASE_LABELS,
  type UiBuilderPhaseId,
} from "../components/UiBuilderFlowStepper.tsx";

Deno.test("UI_BUILDER_CANVAS_PHASES: canvas workspace has exactly 2 phases", () => {
  assertEquals(UI_BUILDER_CANVAS_PHASES.length, 2);
});

Deno.test("UI_BUILDER_CANVAS_PHASES: phase IDs are 1 and 2", () => {
  assertEquals(UI_BUILDER_CANVAS_PHASES.map((p) => p.id), [1, 2]);
});

Deno.test("UI_BUILDER_CANVAS_PHASES: phase 1 is package selection", () => {
  assertEquals(UI_BUILDER_CANVAS_PHASES[0].phaseId, "package");
  assertEquals(UI_BUILDER_CANVAS_PHASES[0].id, 1);
});

Deno.test("UI_BUILDER_CANVAS_PHASES: phase 2 is canvas workspace", () => {
  assertEquals(UI_BUILDER_CANVAS_PHASES[1].phaseId, "canvas");
  assertEquals(UI_BUILDER_CANVAS_PHASES[1].id, 2);
});

Deno.test("getActivePhaseIds: package phase activates phase 1 only", () => {
  assertEquals(getActivePhaseIds("package"), [1]);
});

Deno.test("getActivePhaseIds: canvas phase activates phase 2 only", () => {
  assertEquals(getActivePhaseIds("canvas"), [2]);
});

Deno.test("UI_BUILDER_PHASE_LABELS: package phase has user-facing label", () => {
  const label = UI_BUILDER_PHASE_LABELS["package" as UiBuilderPhaseId];
  assertEquals(typeof label, "string");
  assertEquals(label.length > 0, true);
});

Deno.test("UI_BUILDER_PHASE_LABELS: canvas phase has user-facing label", () => {
  const label = UI_BUILDER_PHASE_LABELS["canvas" as UiBuilderPhaseId];
  assertEquals(typeof label, "string");
  assertEquals(label.includes("canvas"), true);
});

Deno.test("canvas workspace: no separate layout/design/visual tabs in phase model", () => {
  const phaseIds = UI_BUILDER_CANVAS_PHASES.map((p) => p.phaseId);
  assertFalse(phaseIds.includes("layout" as UiBuilderPhaseId));
  assertFalse(phaseIds.includes("design" as UiBuilderPhaseId));
  assertFalse(phaseIds.includes("visual" as UiBuilderPhaseId));
});

Deno.test("canvas workspace: phase 1 label includes パッケージ", () => {
  assertEquals(UI_BUILDER_CANVAS_PHASES[0].label.includes("パッケージ"), true);
});

Deno.test("canvas workspace: phase 2 detail describes unified canvas editing", () => {
  const detail = UI_BUILDER_CANVAS_PHASES[1].detail;
  assertEquals(typeof detail, "string");
  assertEquals(detail.includes("canvas"), true);
});
