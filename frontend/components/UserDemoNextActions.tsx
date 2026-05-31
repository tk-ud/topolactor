import { JSX } from "preact";
import type { UserFacingResult } from "../runtime/emissionSummary.ts";

type Props = {
  result: UserFacingResult;
  currentScenarioId: string | null;
  onRelated?: (scenarioId: string) => void;
  onRetry?: () => void;
};

function relatedScenario(current: string | null): { id: string; label: string } {
  if (current === "demo_hub_recommendation") {
    return { id: "demo_entity_list", label: "候補の一覧を見る" };
  }
  if (current === "demo_hub_overview") {
    return { id: "demo_hub_recommendation", label: "おすすめを見る" };
  }
  return { id: "demo_hub_overview", label: "全体を見る" };
}

export function UserDemoNextActions(
  { result: _result, currentScenarioId, onRelated, onRetry }: Props,
): JSX.Element {
  const related = relatedScenario(currentScenarioId);

  return (
    <div class="mt-6">
      <h3 class="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">
        次にできること
      </h3>
      <div class="flex flex-wrap gap-2">
        {onRelated && (
          <button
            type="button"
            onClick={() => onRelated(related.id)}
            class="rounded border border-blue-200 bg-blue-50 px-4 py-2 text-sm text-blue-800 hover:bg-blue-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            {related.label}
          </button>
        )}
        {onRetry && (
          <button
            type="button"
            onClick={onRetry}
            class="rounded border border-gray-200 bg-white px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-gray-400"
          >
            別のシナリオを試す
          </button>
        )}
        <a
          href="/demo/debug"
          class="rounded border border-gray-200 bg-white px-4 py-2 text-sm text-gray-500 hover:bg-gray-50"
        >
          詳細・デバッグ
        </a>
        <a
          href="/admin/runtime"
          class="rounded border border-gray-200 bg-white px-4 py-2 text-sm text-gray-500 hover:bg-gray-50"
        >
          管理者向け検証
        </a>
      </div>
    </div>
  );
}
