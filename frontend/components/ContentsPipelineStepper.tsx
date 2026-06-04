import { JSX } from "preact";

export type ContentsPipelineStep = 1 | 2 | 2.5 | 3;

const STEP_ORDER: ContentsPipelineStep[] = [1, 2, 2.5, 3];

function stepOrdinal(step: ContentsPipelineStep): number {
  return STEP_ORDER.indexOf(step);
}

/** True when this step was saved in the current pipeline run. */
export function isPipelineStepComplete(
  completedThrough: ContentsPipelineStep | null,
  step: ContentsPipelineStep,
): boolean {
  if (completedThrough === null) return false;
  return stepOrdinal(step) <= stepOrdinal(completedThrough);
}

const STEPS: { id: ContentsPipelineStep; label: string; detail: string }[] = [
  {
    id: 1,
    label: "空登録",
    detail: "マニフェストを空登録し、わかりやすいトポロジー名を登録します。",
  },
  {
    id: 2,
    label: "テーブル定義",
    detail: "jsonb 内の項目名と型を登録します（複数テーブル可）。",
  },
  {
    id: 2.5,
    label: "関連付け",
    detail: "テーブル間の relationship を設定します（独立して保存）。",
  },
  {
    id: 3,
    label: "物理割当・ページ",
    detail: "ページ名・操作種別（複数可）・検索・集計・データ入力を登録します。",
  },
];

export default function ContentsPipelineStepper({
  activeStep,
  completedThroughStep = null,
  onStepChange,
}: {
  activeStep: ContentsPipelineStep;
  /** Highest step successfully saved this session (visual progress). */
  completedThroughStep?: ContentsPipelineStep | null;
  onStepChange: (step: ContentsPipelineStep) => void;
}): JSX.Element {
  return (
    <div
      class="mb-4 rounded-lg border border-blue-200 bg-blue-50 p-3"
      role="navigation"
      aria-label="ページ作成パイプライン"
    >
      <p class="mb-2 text-xs font-semibold text-blue-900">
        正規作成フロー（step 1 → 4 は画面づくりへ）
      </p>
      <div class="flex flex-wrap items-start gap-2" role="list">
        {STEPS.map((step) => {
          const isActive = activeStep === step.id;
          const isComplete = isPipelineStepComplete(completedThroughStep, step.id);
          return (
            <button
              key={String(step.id)}
              type="button"
              role="listitem"
              class={`rounded border px-2 py-1 text-left text-xs transition-colors ${
                isActive
                  ? "border-blue-600 bg-blue-600 text-white"
                  : isComplete
                  ? "border-green-500 bg-green-100 text-green-900 hover:border-green-600"
                  : "border-blue-200 bg-white text-blue-800 hover:border-blue-400"
              }`}
              aria-current={isActive ? "step" : undefined}
              onClick={() => onStepChange(step.id)}
            >
              <span class="font-bold">
                Step {step.id}
                {isComplete && !isActive ? " ✓" : ""}
              </span>{" "}
              {step.label}
            </button>
          );
        })}
      </div>
      <p class="mt-2 text-xs text-blue-900">
        {completedThroughStep === 3
          ? "Step 1〜3 は保存済みです。次は画面づくり（Step 4）へ進んでください。"
          : STEPS.find((s) => s.id === activeStep)?.detail}
      </p>
    </div>
  );
}
