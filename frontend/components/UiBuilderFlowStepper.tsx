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

export const UI_BUILDER_FLOW_STEPS: StepSpec[] = [
  {
    id: 1,
    label: "部品を登録する",
    detail:
      "カタログから部品を選んで登録します。未登録・登録中・配置可能などの状態を確認します。",
    tabTarget: "bucket",
  },
  {
    id: 2,
    label: "配置できる状態にする",
    detail:
      "登録済みの部品を順に処理し、レイアウト用パレットに出せる状態にします（内部: generate → promote）。",
    tabTarget: "bucket",
  },
  {
    id: 3,
    label: "レイアウトを組む",
    detail:
      "パレットから追加・ドラッグでキャンバスに配置します。テンプレートボタンも利用できます。",
    note: "⚠ 未登録のみの配置は保存反映できません — 先に「配置できる状態にする」を完了してください",
    tabTarget: "layout",
  },
  {
    id: 4,
    label: "確認して保存反映",
    detail:
      "プレビュー（DBは変更しない）→ 検証（整合性）→ 保存反映 の順で実行します。未登録のみの配置がないことを確認してください。",
    tabTarget: "layout",
  },
  {
    id: 5,
    label: "動作確認",
    detail:
      "保存反映後は Runtime確認 で登録結果を試します。不足があれば該当画面へ戻ります。",
    externalHref: "/demo",
  },
];

/** Returns the step IDs that are active for a given tab */
export function getActiveStepIds(activeTab: UiBuilderTabId): number[] {
  if (activeTab === "bucket") return [1, 2];
  if (activeTab === "layout") return [3, 4];
  return [];
}

/**
 * Compact Stepper for /admin/ui-builder admin flow.
 *
 * Shows the 5-step flow (register → ready → layout → preview → validate → apply → runtime).
 * Highlights the steps relevant to the current active tab.
 * Navigation buttons switch to the relevant tab via the onNavigate callback.
 *
 * Boundary: presentational only. No topology judgment. No direct DB write.
 */
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
        <span class="text-xs font-semibold text-blue-900">UI Builder 内の作業フロー</span>
        <span class="text-[0.65rem] text-blue-600">
          — 現在のタブ: <strong>{activeTab}</strong>
        </span>
      </div>

      <div class="flex items-start overflow-x-auto pb-1" role="list" aria-label="フローステップ">
        {UI_BUILDER_FLOW_STEPS.map((step, i) => {
          const isActive = activeStepIds.includes(step.id);
          return (
            <div key={step.id} class="flex items-center" role="listitem">
              {i > 0 && (
                <div
                  class="mx-1 mt-3 h-px w-4 shrink-0 bg-blue-200"
                  aria-hidden="true"
                />
              )}
              <div class="flex min-w-[72px] max-w-[120px] flex-col items-center">
                <div
                  class={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold ${
                    isActive
                      ? "bg-blue-600 text-white ring-2 ring-blue-300 ring-offset-1"
                      : "border-2 border-gray-300 bg-white text-gray-500"
                  }`}
                  aria-current={isActive ? "step" : undefined}
                  aria-label={`Step ${step.id}: ${step.label}${isActive ? " (現在の作業)" : ""}`}
                >
                  {step.id}
                </div>

                <div
                  class={`mt-1 text-center text-[0.63rem] font-medium leading-snug ${
                    isActive ? "text-blue-800" : "text-gray-500"
                  }`}
                >
                  {step.label}
                </div>

                {step.tabTarget && (
                  <button
                    type="button"
                    onClick={() => onNavigate(step.tabTarget!)}
                    class={`mt-1 rounded px-1.5 py-0.5 text-[0.58rem] font-medium transition-colors focus-visible:ring-2 focus-visible:ring-blue-400 ${
                      isActive
                        ? "bg-blue-600 text-white hover:bg-blue-700"
                        : "border border-gray-300 bg-white text-gray-500 hover:border-blue-400 hover:text-blue-700"
                    }`}
                    aria-label={`Step ${step.id}のタブへ移動`}
                  >
                    {isActive ? "↑ 作業中" : "→ 移動"}
                  </button>
                )}
                {step.externalHref && (
                  <a
                    href={step.externalHref}
                    class={`mt-1 rounded px-1.5 py-0.5 text-[0.58rem] font-medium transition-colors focus-visible:ring-2 focus-visible:ring-blue-400 ${
                      isActive
                        ? "bg-green-600 text-white hover:bg-green-700"
                        : "border border-gray-300 bg-white text-gray-500 hover:border-green-400 hover:text-green-700"
                    }`}
                    aria-label="動作確認へ"
                  >
                    確認 →
                  </a>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {activeDetails.length > 0 && (
        <div class="mt-3 space-y-2">
          {activeDetails.map((step) => (
            <div
              key={step.id}
              class="rounded border border-blue-200 bg-white p-2"
              role="region"
              aria-label={`Step ${step.id} 説明`}
            >
              <p class="text-xs font-semibold text-blue-900">
                Step {step.id}: {step.label}
              </p>
              <p class="mt-0.5 text-xs text-gray-700">{step.detail}</p>
              {step.note && (
                <p class="mt-0.5 text-xs font-medium text-amber-700">{step.note}</p>
              )}
            </div>
          ))}
        </div>
      )}

      {activeDetails.length === 0 && (
        <p class="mt-2 text-[0.65rem] text-gray-500">
          「バケット管理」タブで Step 1–2、「レイアウトビルダー」タブで Step 3–4 の作業を行ってください。
          完了後は Step 5 の{" "}
          <a href="/demo" class="text-blue-600 underline hover:text-blue-800">Runtime確認</a>{" "}
          で動作を確認してください。
        </p>
      )}
    </div>
  );
}
