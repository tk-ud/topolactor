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

export default function UiBuilderFlowStepper({
  activeStep,
}: {
  activeStep: UiBuilderFlowStepId;
}): JSX.Element {
  const activeStepIds = getActiveFlowStepIds(activeStep);
  const activeLabel = UI_BUILDER_FLOW_LABELS[activeStep] ?? activeStep;

  return (
    <div
      class="mb-4 rounded-lg border border-blue-200 bg-blue-50 p-3"
      role="navigation"
      aria-label="画面づくりの作業フロー"
    >
      <div class="mb-2.5 flex items-center gap-2">
        <span class="text-xs font-semibold text-blue-900">Step 4 — 画面づくり（canvas workspace）</span>
        <span class="text-[0.65rem] text-blue-600">
          — 現在: <strong>{activeLabel}</strong>
        </span>
      </div>

      <div class="flex items-start overflow-x-auto pb-1" role="list">
        {UI_BUILDER_CANVAS_FLOW.map((step, i) => {
          const isActive = activeStepIds.includes(step.id);
          return (
            <div key={step.id} class="flex items-center" role="listitem">
              {i > 0 && <div class="mx-1 mt-3 h-px w-4 shrink-0 bg-blue-200" aria-hidden="true" />}
              <div class="flex min-w-[120px] max-w-[180px] flex-col items-center">
                <div
                  class={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold ${
                    isActive
                      ? "bg-blue-600 text-white ring-2 ring-blue-300 ring-offset-1"
                      : "border-2 border-gray-300 bg-white text-gray-500"
                  }`}
                  aria-current={isActive ? "step" : undefined}
                >
                  {step.id}
                </div>
                <div class={`mt-1 text-center text-[0.63rem] font-medium ${isActive ? "text-blue-800" : "text-gray-500"}`}>
                  {step.label}
                </div>
                {step.id === 2 && (
                  <a
                    href="#projection-inspection"
                    class="mt-1 text-[0.58rem] text-blue-600 underline"
                  >
                    投影を確認 →
                  </a>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {UI_BUILDER_CANVAS_FLOW.filter((s) => activeStepIds.includes(s.id)).map((step) => (
        <div key={step.id} class="mt-3 rounded border border-blue-200 bg-white p-2 text-xs">
          <p class="font-semibold text-blue-900">ステップ {step.id}: {step.label}</p>
          <p class="text-gray-700">{step.detail}</p>
          {step.note && <p class="text-amber-700">{step.note}</p>}
        </div>
      ))}
    </div>
  );
}
