import { JSX } from "preact";
import { ADMIN_MAIN_FLOW_STEPS } from "../content/adminGuides.ts";

/** 管理トップ用 — Manifest → Import → UI Builder → Runtime の推奨順ステッパー */
export default function AdminMainFlowStepper(): JSX.Element {
  return (
    <nav
      class="mb-6 rounded-lg border border-blue-200 bg-blue-50 p-4"
      aria-label="管理画面の推奨作業順"
    >
      <p class="mb-3 text-sm text-blue-900">
        <strong>作業の流れ（この順で進めてください）</strong>
        {" "}— インポートの前に取り込み設定が必要です。
      </p>
      <ol class="flex flex-wrap items-start gap-2" role="list">
        {ADMIN_MAIN_FLOW_STEPS.map((step, i) => (
          <li key={step.step} class="flex items-center" role="listitem">
            {i > 0 && (
              <span class="mx-1 text-blue-400" aria-hidden="true">
                →
              </span>
            )}
            <a
              href={step.href}
              class="inline-flex min-w-[7rem] flex-col rounded-md border border-blue-200 bg-white px-2 py-1.5 text-center text-xs hover:border-blue-400 hover:bg-blue-50"
            >
              <span class="font-mono text-[0.65rem] text-blue-600">Step {step.step}</span>
              <span class="font-semibold text-blue-900">{step.label}</span>
            </a>
          </li>
        ))}
      </ol>
    </nav>
  );
}
