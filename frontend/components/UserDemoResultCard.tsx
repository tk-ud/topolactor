import { JSX } from "preact";
import type { UserFacingResult } from "../runtime/emissionSummary.ts";

type Props = {
  result: UserFacingResult;
};

export function UserDemoResultCard({ result }: Props): JSX.Element {
  const isOk = result.status === "success";
  return (
    <div
      class={`rounded-lg border p-5 ${
        isOk ? "border-green-200 bg-green-50" : "border-red-200 bg-red-50"
      }`}
      role="status"
      aria-live="polite"
    >
      <div class="flex items-start gap-3">
        <span
          class={`mt-0.5 text-xl font-bold ${
            isOk ? "text-green-600" : "text-red-600"
          }`}
          aria-hidden="true"
        >
          {isOk ? "✓" : "✗"}
        </span>
        <div>
          <p
            class={`font-semibold ${
              isOk ? "text-green-800" : "text-red-800"
            }`}
          >
            {result.headline}
          </p>
          {result.detail && (
            <p class="mt-1 text-sm text-gray-600">{result.detail}</p>
          )}
          {result.layoutId && (
            <p class="mt-3 font-mono text-xs text-gray-500">
              layout: <code>{result.layoutId}</code>
            </p>
          )}
          {result.hasRecommendation && result.recommendationSummary && (
            <p class="mt-3 inline-block rounded bg-blue-100 px-3 py-1 text-sm text-blue-800">
              {result.recommendationSummary}
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
