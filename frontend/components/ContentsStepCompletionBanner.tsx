import { JSX } from "preact";
import { UX_UI_BUILDER } from "../content/adminUxTerms.ts";

export type ContentsStepCompletionBannerProps = {
  title: string;
  body: string;
  primaryHref: string;
  primaryLabel: string;
  /** When set, shows a secondary action (e.g. stay on this page). */
  onDismiss?: () => void;
  dismissLabel?: string;
};

/** Prominent post-save guidance after pipeline step submit. */
export default function ContentsStepCompletionBanner({
  title,
  body,
  primaryHref,
  primaryLabel,
  onDismiss,
  dismissLabel = "この画面に留まる",
}: ContentsStepCompletionBannerProps): JSX.Element {
  return (
    <section
      class="mb-4 rounded-lg border-2 border-green-400 bg-green-50 p-4 shadow-sm"
      role="status"
      aria-live="polite"
    >
      <p class="text-base font-semibold text-green-900">{title}</p>
      <p class="mt-1 text-sm text-green-800">{body}</p>
      <div class="mt-4 flex flex-wrap gap-3">
        <a href={primaryHref} class="btn-primary inline-block text-sm no-underline">
          {primaryLabel}
        </a>
        {onDismiss && (
          <button
            type="button"
            class="btn-secondary text-sm"
            onClick={onDismiss}
          >
            {dismissLabel}
          </button>
        )}
      </div>
      <p class="mt-3 text-xs text-green-700">
        次の正規ステップ: <strong>{UX_UI_BUILDER}</strong>（部品選択 → パッケージ → レイアウト）
      </p>
    </section>
  );
}
