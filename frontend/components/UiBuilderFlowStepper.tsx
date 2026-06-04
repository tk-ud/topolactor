import { JSX } from "preact";

/** Tab IDs for /admin/ui-builder — must stay in sync with UiBuilderAdmin.tsx TabId */
export type UiBuilderTabId = "ci" | "catalog" | "bucket" | "css" | "layout";

type StepSpec = {
  id: number;
  label: string;
  detail: string;
  note?: string;
  tabTarget?: UiBuilderTabId;
  externalHref?: string;
};

/** SSOT v0.7.2 step 4 — two phases: 4.1 package, 4.2 edit package children. */
export const UI_BUILDER_FLOW_STEPS: StepSpec[] = [
  {
    id: 1,
    label: "部品選択でパッケージ化",
    detail:
      "バケットから部品を複数選択し、1 回の submit でパッケージ化します（内部: generate → promote）。カタログは参照専用です。",
    note: "コンポーネントカタログ・CI は画面下部の参照専用セクションにあります。",
    tabTarget: "bucket",
  },
  {
    id: 2,
    label: "パッケージの layout / design",
    detail:
      "パッケージを選び、子階層の UI 配置（layout）と色・形・トークン・リアクション（component design）・配線を編集します。layout 保存には packageId が必須です。",
    tabTarget: "layout",
  },
  {
    id: 3,
    label: "動作確認",
    detail: "保存反映後は Runtime確認 で登録結果を試します。",
    externalHref: "/demo",
  },
];

export function getActiveStepIds(activeTab: UiBuilderTabId): number[] {
  if (activeTab === "bucket") return [1];
  if (activeTab === "layout" || activeTab === "css") return [2];
  return [];
}

export default function UiBuilderFlowStepper({
  activeTab,
  onNavigate,
}: {
  activeTab: UiBuilderTabId;
  onNavigate: (tab: UiBuilderTabId) => void;
}): JSX.Element {
  const activeStepIds = getActiveStepIds(activeTab);
  const activeDetails = UI_BUILDER_FLOW_STEPS.filter((s) => activeStepIds.includes(s.id));

  return (
    <div
      class="mb-4 rounded-lg border border-blue-200 bg-blue-50 p-3"
      role="navigation"
      aria-label="UI ビルダー作業フロー"
    >
      <div class="mb-2.5 flex items-center gap-2">
        <span class="text-xs font-semibold text-blue-900">Step 4 — パッケージ編集ルート</span>
        <span class="text-[0.65rem] text-blue-600">
          — 現在: <strong>{activeTab}</strong>
        </span>
      </div>

      <div class="flex items-start overflow-x-auto pb-1" role="list">
        {UI_BUILDER_FLOW_STEPS.map((step, i) => {
          const isActive = activeStepIds.includes(step.id);
          return (
            <div key={step.id} class="flex items-center" role="listitem">
              {i > 0 && <div class="mx-1 mt-3 h-px w-4 shrink-0 bg-blue-200" aria-hidden="true" />}
              <div class="flex min-w-[100px] max-w-[160px] flex-col items-center">
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
                {step.tabTarget && (
                  <button
                    type="button"
                    onClick={() => onNavigate(step.tabTarget!)}
                    class={`mt-1 rounded px-1.5 py-0.5 text-[0.58rem] font-medium ${
                      isActive ? "bg-blue-600 text-white" : "border border-gray-300 bg-white text-gray-500"
                    }`}
                  >
                    {isActive ? "作業中" : "移動"}
                  </button>
                )}
                {step.externalHref && (
                  <a href={step.externalHref} class="mt-1 text-[0.58rem] text-blue-600 underline">
                    確認 →
                  </a>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {activeDetails.map((step) => (
        <div key={step.id} class="mt-3 rounded border border-blue-200 bg-white p-2 text-xs">
          <p class="font-semibold text-blue-900">Phase {step.id}: {step.label}</p>
          <p class="text-gray-700">{step.detail}</p>
          {step.note && <p class="text-amber-700">{step.note}</p>}
        </div>
      ))}
    </div>
  );
}
