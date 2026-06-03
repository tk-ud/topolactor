import { JSX } from "preact";
import {
  AGGREGATION_FUNCTION_OPTIONS,
  UX_FIELD_AGGREGATION_KEY,
  UX_FIELD_AGGREGATION_MEASURES,
} from "../content/adminUxTerms.ts";
import {
  emptyAggregationMeasure,
  type AggregationMeasure,
} from "../lib/aggregationMeasures.ts";

export type ContentsAggregationMeasuresEditorProps = {
  columnNames: string[];
  aggregationKey: string;
  measures: AggregationMeasure[];
  onAggregationKeyChange: (key: string) => void;
  onMeasuresChange: (measures: AggregationMeasure[]) => void;
};

export default function ContentsAggregationMeasuresEditor({
  columnNames,
  aggregationKey,
  measures,
  onAggregationKeyChange,
  onMeasuresChange,
}: ContentsAggregationMeasuresEditorProps): JSX.Element {
  const patchMeasure = (index: number, patch: Partial<AggregationMeasure>) => {
    onMeasuresChange(
      measures.map((m, i) => (i === index ? { ...m, ...patch } : m)),
    );
  };

  const removeMeasure = (index: number) => {
    onMeasuresChange(measures.filter((_, i) => i !== index));
  };

  const addMeasure = () => {
    onMeasuresChange([...measures, emptyAggregationMeasure()]);
  };

  return (
    <div class="space-y-3">
      <label class="block text-xs">
        {UX_FIELD_AGGREGATION_KEY}（グループ化・任意）
        <select
          class="mt-1 w-full max-w-xs rounded border px-2 py-1 text-xs"
          value={aggregationKey}
          onChange={(e) =>
            onAggregationKeyChange((e.target as HTMLSelectElement).value)}
        >
          <option value="">— なし（全体） —</option>
          {columnNames.map((c) => (
            <option key={c} value={c}>{c}</option>
          ))}
        </select>
      </label>

      <div>
        <p class="mb-1 text-xs font-semibold text-slate-700">
          {UX_FIELD_AGGREGATION_MEASURES}
        </p>
        <p class="mb-2 text-xs text-muted-xs">
          同じ列に複数関数を登録できます（例: salary の sum / max / min / avg）。
        </p>
        {measures.length === 0
          ? (
            <p class="text-xs italic text-slate-400">
              「集計式を追加」から登録してください。
            </p>
          )
          : (
            <div class="space-y-2">
              {measures.map((m, mi) => (
                <div
                  key={mi}
                  class="flex flex-wrap items-center gap-2 rounded border border-slate-100 p-2"
                >
                  <select
                    class="rounded border px-2 py-1 text-xs font-mono"
                    value={m.column}
                    onChange={(e) =>
                      patchMeasure(mi, {
                        column: (e.target as HTMLSelectElement).value,
                      })}
                  >
                    <option value="">— 列 —</option>
                    {columnNames.map((c) => (
                      <option key={c} value={c}>{c}</option>
                    ))}
                  </select>
                  <select
                    class="rounded border px-2 py-1 text-xs"
                    value={m.function}
                    onChange={(e) =>
                      patchMeasure(mi, {
                        function: (e.target as HTMLSelectElement).value,
                      })}
                  >
                    {AGGREGATION_FUNCTION_OPTIONS.filter((o) => o.value).map((
                      o,
                    ) => (
                      <option key={o.value} value={o.value}>{o.label}</option>
                    ))}
                  </select>
                  <button
                    type="button"
                    class="text-xs text-red-500 hover:text-red-700"
                    onClick={() => removeMeasure(mi)}
                  >
                    削除
                  </button>
                </div>
              ))}
            </div>
          )}
        <button
          type="button"
          class="btn-secondary mt-2 text-xs"
          onClick={addMeasure}
        >
          集計式を追加
        </button>
      </div>
    </div>
  );
}
