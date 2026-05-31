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
    label: "コンポーネントを登録する",
    detail:
      "カタログからコンポーネントを選んでバケット登録。未登録 / bucketed / packaging / promoted の状態を確認。",
    tabTarget: "bucket",
  },
  {
    id: 2,
    label: "パッケージ化してプロモートする",
    detail:
      "bucketed → generate（packaging）→ promote の順で実行。promoted になって初めてレイアウトパレットに出せます。",
    tabTarget: "bucket",
  },
  {
    id: 3,
    label: "レイアウトを組む",
    detail:
      "パレットから「追加」ボタン・ドラッグ・キーボード操作でキャンバスに配置。empty state のテンプレートボタンも使えます。",
    note: "⚠ ドラフトのみノード（未登録）は apply 不可 — 先にプロモートしてください",
    tabTarget: "layout",
  },
  {
    id: 4,
    label: "確認して適用する",
    detail:
      "preview（DB不変）→ validate（ref整合）→ apply の順で実行。ApplyReadinessPanel でドラフトのみノードがゼロであることを確認してから apply。",
    tabTarget: "layout",
  },
  {
    id: 5,
    label: "次に確認する",
    detail:
      "apply 後は /admin/runtime で dispatch → emission を検証。登録不足なら対応する画面へ戻る。",
    externalHref: "/admin/runtime",
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
 * Shows the 5-step flow (bucket → generate → promote → layout → preview → validate → apply → runtime).
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
        <span class="text-xs font-semibold text-blue-900">作業フロー</span>
        <span class="text-[0.65rem] text-blue-600">
          — 現在のタブ: <strong>{activeTab}</strong>
        </span>
      </div>

      {/* Step indicators row */}
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
                {/* circle indicator */}
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

                {/* label */}
                <div
                  class={`mt-1 text-center text-[0.63rem] font-medium leading-snug ${
                    isActive ? "text-blue-800" : "text-gray-500"
                  }`}
                >
                  {step.label}
                </div>

                {/* navigation button or external link */}
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
                    aria-label="runtime 確認へ"
                  >
                    確認 →
                  </a>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* Active step detail panel */}
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
          「バケット管理」タブで Step 1-2、「レイアウトビルダー」タブで Step 3-4 の作業を行ってください。
          完了後は Step 5 の <a href="/admin/runtime" class="text-blue-600 underline hover:text-blue-800">/admin/runtime</a> で検証。
        </p>
      )}
    </div>
  );
}
