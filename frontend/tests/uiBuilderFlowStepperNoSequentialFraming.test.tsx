// frontend/tests/uiBuilderFlowStepperNoSequentialFraming.test.tsx
//
// SSOT: docs/design/admin-console-workflow-ssot.yaml
// - line ~208: "/admin/ui-builder as a canvas workspace route (NOT a pipeline step): full-width
//   canvas with docked panels; no separate surfaces, no sequential step framing"
// - canvas_workspace_contract.prohibited: tab_based_or_step_based_surface_separation_for_layout_
//   vs_design_vs_preview / framing_ui_builder_as_a_pipeline_step_or_workflow_step
//
// Before this round, the PRODUCTION UiBuilderFlowStepper (mounted unconditionally at the top of
// /admin/ui-builder) rendered a 3-node numbered stepper track — circular step badges (1/2/3),
// connecting lines between them, and per-step "ステップ N: ..." framing driven by an
// aria-current="step" active/inactive comparison across all three phases. That IS the "sequential
// step framing ... within this workspace" the SSOT prohibits (route selection -> canvas edit ->
// persist rendered as a wizard), independent of the (out-of-scope-for-this-bundle) "Step 4"
// whole-admin ordinal wording, which this fix deliberately leaves untouched.
//
// This mounts the REAL production component (not a data-shape-only check, which
// uiBuilderStepper.test.ts already covers) and proves the rendered DOM no longer contains a
// multi-node numbered track, while still surfacing real contextual guidance for the current phase.

import { assert, assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import UiBuilderFlowStepper from "../components/UiBuilderFlowStepper.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

Deno.test("UiBuilderFlowStepper: renders no multi-node numbered step track (no aria-current=step, no per-step ordinal badges 1/2/3)", async () => {
  const { container, cleanup } = setupDom();
  try {
    render(h(UiBuilderFlowStepper, { activeStep: "canvas_edit" }), container);
    await flushUpdates();
    assertEquals(
      container.querySelectorAll('[aria-current="step"]').length,
      0,
      "no node should be marked as the active step of a sequential track",
    );
    assertEquals(
      container.querySelectorAll('[role="listitem"]').length,
      0,
      "no step-track list items should render inside the canvas workspace guide",
    );
    // Exactly one contextual guidance card for the CURRENT phase, not three step nodes.
    const html = container.innerHTML;
    assert(html.includes("canvas workspace で配置・デザインを編集"), "current-phase guidance must still render");
  } finally {
    cleanup();
  }
});

Deno.test("UiBuilderFlowStepper: switching activeStep changes the shown guidance without a step-track comparison across all phases", async () => {
  const { container, cleanup } = setupDom();
  try {
    render(h(UiBuilderFlowStepper, { activeStep: "route" }), container);
    await flushUpdates();
    assert(container.innerHTML.includes("ルートを選ぶ"));
    assertFalse(
      container.innerHTML.includes("プレビュー → 検証 → 保存反映"),
      "only the active phase's own guidance should render, not every phase's detail side by side",
    );

    render(h(UiBuilderFlowStepper, { activeStep: "persist" }), container);
    await flushUpdates();
    assert(container.innerHTML.includes("プレビュー → 検証 → 保存反映"));
    assertFalse(
      container.innerHTML.includes("左パネルの部品カードを canvas にドロップ"),
      "the previous phase's own detail text must not remain alongside the new active phase",
    );
  } finally {
    cleanup();
  }
});
