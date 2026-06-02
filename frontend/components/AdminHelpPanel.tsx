import { type ComponentChildren, JSX } from "preact";
import type { AdminGuide } from "../content/adminGuides.ts";

export type AdminHelpPanelProps = AdminGuide & {
  defaultOpen?: boolean;
};

function GuideList({ items }: { items: string[] }): JSX.Element {
  return (
    <ul class="mt-1 list-inside list-disc space-y-0.5 text-sm">
      {items.map((item, i) => (
        <li key={i}>{item}</li>
      ))}
    </ul>
  );
}

/** Purpose → inputs → actions → outputs → next steps → boundary (+ errors / caution). SSR-safe (no hooks). */
export default function AdminHelpPanel({
  title,
  purpose,
  inputs,
  actions,
  outputs,
  nextSteps,
  boundaryNotes,
  errorGuide,
  caution,
  defaultOpen = false,
}: AdminHelpPanelProps): JSX.Element {
  return (
    <details class="admin-help-panel mb-6" open={defaultOpen || undefined}>
      <summary class="accordion-trigger-closed cursor-pointer list-none [&::-webkit-details-marker]:hidden">
        <span class="flex w-full items-center justify-between">
          <span>詳細リファレンス — {title}</span>
          <span class="text-muted-xs">▼ 開閉</span>
        </span>
      </summary>
      <div class="alert-info rounded-t-none border-t-0">
        <p class="mb-3 text-sm">
          <strong class="font-semibold">目的:</strong> {purpose}
        </p>

        <div class="mb-3 grid gap-3 sm:grid-cols-2">
          <div>
            <h3 class="text-xs font-bold uppercase tracking-wide text-blue-900">入力</h3>
            <GuideList items={inputs} />
          </div>
          <div>
            <h3 class="text-xs font-bold uppercase tracking-wide text-blue-900">操作</h3>
            <GuideList items={actions} />
          </div>
          <div>
            <h3 class="text-xs font-bold uppercase tracking-wide text-blue-900">成功時</h3>
            <GuideList items={outputs} />
          </div>
          {nextSteps && nextSteps.length > 0 && (
            <div>
              <h3 class="text-xs font-bold uppercase tracking-wide text-blue-900">次のステップ</h3>
              <GuideList items={nextSteps} />
            </div>
          )}
        </div>

        {boundaryNotes && boundaryNotes.length > 0 && (
          <div class="mb-3 border-t border-blue-200 pt-3">
            <h3 class="text-xs font-bold uppercase tracking-wide text-blue-900">責務境界（SSOT）</h3>
            <GuideList items={boundaryNotes} />
          </div>
        )}

        {errorGuide && errorGuide.length > 0 && (
          <div class="mb-3 border-t border-blue-200 pt-3">
            <h3 class="text-xs font-bold uppercase tracking-wide text-blue-900">エラー時の見方</h3>
            <GuideList items={errorGuide} />
          </div>
        )}

        {caution && (
          <p class="alert-warn mt-2 text-sm">
            <strong>注意:</strong> {caution}
          </p>
        )}
      </div>
    </details>
  );
}

/** One-line hint under a button or control */
export function AdminActionHint({ children }: { children: ComponentChildren }): JSX.Element {
  return <p class="text-muted-xs mt-1 mb-0 max-w-2xl">{children}</p>;
}
