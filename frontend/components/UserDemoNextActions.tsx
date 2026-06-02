import { JSX } from "preact";
import type { UserFacingResult } from "../runtime/emissionSummary.ts";
import { demoPreviewOptions } from "../runtime/operationPresets.ts";

type Props = {
  result: UserFacingResult;
  currentScenarioId: string | null;
  onRelated?: (scenarioId: string) => void;
  onRetry?: () => void;
};

/**
 * preview 監査上の次の確認導線を提示する。
 * 構築・編集導線ではなく、関連 projection の確認 / debug 検証 / admin 構築面への遷移。
 */
export function UserDemoNextActions(
  { result: _result, currentScenarioId, onRelated, onRetry }: Props,
): JSX.Element {
  // Related projections: all demo options excluding the current one.
  const related = demoPreviewOptions().filter((opt) => opt.id !== currentScenarioId);

  return (
    <div class="mt-6">
      <h3 class="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">
        関連する投影を確認する
      </h3>
      <div class="flex flex-wrap gap-2">
        {onRelated && related.map((opt) => (
          <button
            key={opt.id}
            type="button"
            onClick={() => onRelated(opt.id)}
            class="rounded border border-blue-200 bg-blue-50 px-4 py-2 text-sm text-blue-800 hover:bg-blue-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            {opt.previewLabel}
          </button>
        ))}
        {onRetry && (
          <button
            type="button"
            onClick={onRetry}
            class="rounded border border-gray-200 bg-white px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-gray-400"
          >
            別の投影を確認する
          </button>
        )}
      </div>

      <h3 class="mb-3 mt-4 text-sm font-semibold uppercase tracking-wide text-gray-500">
        検証・構築
      </h3>
      <div class="flex flex-wrap gap-2">
        <a
          href="/demo/debug"
          class="rounded border border-gray-200 bg-white px-4 py-2 text-sm text-gray-500 hover:bg-gray-50"
        >
          raw runtime を検証する
        </a>
        <a
          href="/admin"
          class="rounded border border-gray-200 bg-white px-4 py-2 text-sm text-gray-500 hover:bg-gray-50"
        >
          admin で構築・編集する
        </a>
        <a
          href="/demo/debug"
          class="rounded border border-gray-200 bg-white px-4 py-2 text-sm text-gray-500 hover:bg-gray-50"
        >
          admin runtime 検証
        </a>
      </div>
    </div>
  );
}
