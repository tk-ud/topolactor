import { JSX } from "preact";

export type LayoutComponentPreviewFallbackProps = {
  componentKey: string;
  componentKind: string;
  code: string;
  reason: string;
};

/** Explicit fallback — never silent; distinct from successful live preview. */
export function LayoutComponentPreviewFallback({
  componentKey,
  componentKind,
  code,
  reason,
}: LayoutComponentPreviewFallbackProps): JSX.Element {
  const isDraftOnly = code === "DRAFT_ONLY";
  return (
    <div
      class={`flex h-full min-h-0 flex-col items-center justify-center border-2 border-dashed p-2 text-center ${
        isDraftOnly
          ? "border-yellow-400 bg-yellow-50"
          : "border-amber-400 bg-amber-50"
      }`}
      role="status"
      aria-label={isDraftOnly ? "プレビュー未接続" : "プレビュー未対応"}
      data-layout-preview-fallback={code}
    >
      <span
        class={`text-[0.62rem] font-semibold ${
          isDraftOnly ? "text-yellow-800" : "text-amber-800"
        }`}
      >
        {isDraftOnly ? "未接続" : "プレビュー未対応"}
      </span>
      <span class="mt-0.5 font-mono text-[0.55rem] text-slate-700 break-all">
        {componentKey}
      </span>
      <span class="font-mono text-[0.5rem] text-slate-500">{componentKind}</span>
      <span class="mt-1 text-[0.5rem] text-slate-600">{reason}</span>
      <span class="mt-0.5 font-mono text-[0.48rem] text-slate-400">{code}</span>
    </div>
  );
}
