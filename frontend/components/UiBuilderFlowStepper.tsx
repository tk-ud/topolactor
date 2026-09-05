import { JSX } from "preact";

/**
 * /admin/ui-builder canvas workspace flow guide.
 * SSOT: admin-console-workflow-ssot.yaml §authoring_flow
 *
 * Single unified flow: route selection → canvas drop/edit (implicit package).
 */

export type UiBuilderFlowStepId = "route" | "canvas_edit" | "persist";

/** 通常表示用ステップラベル */
export const UI_BUILDER_FLOW_LABELS: Record<UiBuilderFlowStepId, string> = {
  route: "ルート選択",
  canvas_edit: "canvas workspace で配置・デザインを編集",
  persist: "保存反映",
};

type FlowStepSpec = {
  id: number;
  stepId: UiBuilderFlowStepId;
  label: string;
  detail: string;
  note?: string;
};

/** SSOT: canvas_workspace_contract — implicit package, single authoring flow. */
export const UI_BUILDER_CANVAS_FLOW: FlowStepSpec[] = [
  {
    id: 1,
    stepId: "route",
    label: "ルートを選ぶ",
    detail:
      "ページルートを選択または入力すると、該当ルートのパッケージがなければ自動生成されます。",
    note: "パッケージ化ボタンはありません。",
  },
  {
    id: 2,
    stepId: "canvas_edit",
    label: "canvas workspace で配置・デザインを編集",
    detail:
      "左パネルの部品カードを canvas にドロップすると自動でパッケージに追加されます。" +
      " 配置（parentNodeId / slotKey / orderIndex / layoutClassRefs）は layout_patch:apply で保存。" +
      " デザイントークンは component_style_design:upsert で保存。",
  },
  {
    id: 3,
    stepId: "persist",
    label: "プレビュー → 検証 → 保存反映",
    detail: "layout_patch の preview / validate / apply で canvas 配置を永続化します。",
  },
];

export function getActiveFlowStepIds(stepId: UiBuilderFlowStepId): number[] {
  if (stepId === "route") return [1];
  if (stepId === "canvas_edit") return [2];
  if (stepId === "persist") return [3];
  return [];
}

/**
 * SSOT: admin-console-workflow-ssot.yaml ui_builder_canvas_workspace /
 * canvas_workspace_contract prohibits "pipeline-step framing ... within this
 * workspace" and "no sequential step framing" for the canvas workspace route
 * itself. This guide surfaces contextual help for the author's current phase
 * (route selection / canvas editing / persist) WITHOUT rendering it as a
 * numbered multi-node stepper track (no step-N-of-3 circles, no connecting
 * lines, no active/inactive step comparison) — that visual model is exactly
 * the sequential pipeline framing the workspace contract prohibits. The
 * underlying UiBuilderFlowStepId / UI_BUILDER_CANVAS_FLOW phase data stays a
 * legitimate internal progress signal; only the stepper-shaped rendering is
 * removed here. The "Step 4" whole-admin ordinal label wording itself is out
 * of this surface's scope (docs/design/admin-console-workflow-ssot.yaml
 * canonical_authoring_order boundary) and is left unchanged.
 */
export default function UiBuilderFlowStepper({
  activeStep,
}: {
  activeStep: UiBuilderFlowStepId;
}): JSX.Element {
  const activeStepIds = getActiveFlowStepIds(activeStep);
  const activeLabel = UI_BUILDER_FLOW_LABELS[activeStep] ?? activeStep;
  const activeSpec = UI_BUILDER_CANVAS_FLOW.find((s) => activeStepIds.includes(s.id));

  return (
    <div
      class="mb-4 rounded-lg border border-blue-200 bg-blue-50 p-3"
      role="note"
      aria-label="canvas workspace ガイド"
    >
      <div class="mb-2 flex items-center gap-2">
        <span class="text-xs font-semibold text-blue-900">Step 4 — 画面づくり（canvas workspace）</span>
        <span class="text-[0.65rem] text-blue-600">
          — 現在: <strong>{activeLabel}</strong>
        </span>
      </div>

      {activeSpec && (
        <div class="rounded border border-blue-200 bg-white p-2 text-xs">
          <p class="font-semibold text-blue-900">{activeSpec.label}</p>
          <p class="text-gray-700">{activeSpec.detail}</p>
          {activeSpec.note && <p class="text-amber-700">{activeSpec.note}</p>}
          {activeSpec.stepId === "canvas_edit" && (
            <a
              href="#projection-inspection"
              class="mt-1 inline-block text-[0.65rem] text-blue-600 underline"
            >
              投影を確認 →
            </a>
          )}
        </div>
      )}
    </div>
  );
}
