import { useState } from "preact/hooks";
import { JSX } from "preact";
import { ADMIN_MAIN_FLOW_STEPS } from "../content/adminGuides.ts";

/** 管理トップ用 — canonical workflow ステッパー（subSteps タブ分岐対応） */
export default function AdminMainFlowStepper(): JSX.Element {
  const [activeSubStepHref, setActiveSubStepHref] = useState<Record<number, string>>({});

  return (
    <nav
      class="mb-6 rounded-lg border border-blue-200 bg-blue-50 p-4"
      aria-label="管理画面の推奨作業順"
    >
      <p class="mb-3 text-sm text-blue-900">
        <strong>作業の流れ（この順で進めてください）</strong>
        {" "}— データ取り込みの前にコンテンツ設定を進めてください。
      </p>
      <ol class="flex flex-wrap items-start gap-2" role="list">
        {ADMIN_MAIN_FLOW_STEPS.map((step, i) => {
          const hasSubSteps = step.subSteps && step.subSteps.length > 0;
          const currentHref = hasSubSteps
            ? (activeSubStepHref[step.step] ?? step.subSteps![0].href)
            : step.href;

          return (
            <li key={step.step} class="flex items-center" role="listitem">
              {i > 0 && (
                <span class="mx-1 text-blue-400" aria-hidden="true">→</span>
              )}
              <div class="inline-flex min-w-[7rem] flex-col rounded-md border border-blue-200 bg-white text-center text-xs">
                <a
                  href={currentHref}
                  class="rounded-t-md px-2 py-1.5 hover:border-blue-400 hover:bg-blue-50"
                >
                  <span class="block font-mono text-[0.65rem] text-blue-600">Step {step.step}</span>
                  <span class="block font-semibold text-blue-900">{step.label}</span>
                </a>
                {hasSubSteps && (
                  <div class="flex divide-x divide-blue-100 border-t border-blue-100 rounded-b-md overflow-hidden">
                    {step.subSteps!.map((sub) => {
                      const isActive = (activeSubStepHref[step.step] ?? step.subSteps![0].href) === sub.href;
                      return (
                        <button
                          key={sub.href}
                          type="button"
                          class={`flex-1 px-1 py-0.5 text-[0.6rem] transition-colors ${
                            isActive
                              ? "bg-blue-100 font-semibold text-blue-800"
                              : "text-blue-500 hover:bg-blue-50"
                          }`}
                          onClick={() =>
                            setActiveSubStepHref((prev) => ({ ...prev, [step.step]: sub.href }))}
                        >
                          {sub.label}
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
