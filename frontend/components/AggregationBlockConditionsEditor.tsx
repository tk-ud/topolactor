import { JSX } from "preact";
import {
  HAVING_OPERATOR_OPTIONS,
  LOGICAL_CONNECTOR_OPTIONS,
  SEARCH_OPERATOR_OPTIONS,
  UX_FIELD_ADD_SEARCH_CONDITION,
  UX_FIELD_ADVANCED_SEARCH_CONDITIONS,
  UX_FIELD_HAVING_CONDITIONS,
  UX_FIELD_LOGICAL_CONDITION,
  UX_FIELD_SEARCH_CONDITIONS,
} from "../content/adminUxTerms.ts";
import type { AggregationMeasure } from "../lib/aggregationMeasures.ts";
import type { QualifiedColumnRef } from "../lib/manifestLogicalTables.ts";
import type { HavingCondition, SearchCondition, SearchOperator } from "../lib/manifestScreenDesign.ts";
import { isDateLikeType } from "../lib/searchConditionEval.ts";

export type AggregationBlockConditionsEditorProps = {
  columnKeys: { key: string; label: string }[];
  qualifiedColumns: QualifiedColumnRef[];
  measures: AggregationMeasure[];
  searchConditions: SearchCondition[];
  havingConditions: HavingCondition[];
  onSearchConditionsChange: (next: SearchCondition[]) => void;
  onHavingConditionsChange: (next: HavingCondition[]) => void;
};

export default function AggregationBlockConditionsEditor({
  columnKeys,
  qualifiedColumns,
  measures,
  searchConditions,
  havingConditions,
  onSearchConditionsChange,
  onHavingConditionsChange,
}: AggregationBlockConditionsEditorProps): JSX.Element {
  const completeMeasures = measures.filter((m) => m.column && m.function);
  const defaultColumn = columnKeys[0]?.key ?? "";

  return (
    <details class="mt-2 rounded border border-slate-100 bg-slate-50 p-2">
      <summary class="cursor-pointer text-xs font-semibold text-slate-700">
        {UX_FIELD_ADVANCED_SEARCH_CONDITIONS}
      </summary>
      <p class="mt-2 text-xs text-muted-xs">
        このブロックのソース行だけを対象に検索条件を適用します。集計後の絞り込み（HAVING）もブロック内で設定します。
      </p>
      {searchConditions.length === 0
        ? (
          <button
            type="button"
            class="btn-secondary mt-2 text-xs"
            onClick={() => {
              onSearchConditionsChange([
                { column: defaultColumn, operator: "=" as SearchOperator, value: "" },
              ]);
            }}
          >
            {UX_FIELD_ADD_SEARCH_CONDITION}
          </button>
        )
        : (
          <div class="mt-3">
            <h4 class="text-xs font-semibold text-slate-700">{UX_FIELD_SEARCH_CONDITIONS}</h4>
            {searchConditions.map((cond, ci) => (
              <div
                key={ci}
                class="mb-2 flex flex-wrap items-center gap-2 rounded border border-slate-100 bg-white p-2 text-xs"
              >
                {ci > 0 && (
                  <label class="flex items-center gap-1 text-xs text-slate-600">
                    <span>{UX_FIELD_LOGICAL_CONDITION}</span>
                    <select
                      class="rounded border px-1 py-0.5 text-xs"
                      value={searchConditions[ci - 1].logicalConnector ?? "and"}
                      onChange={(e) => {
                        const next = searchConditions.map((c, i) =>
                          i === ci - 1
                            ? {
                              ...c,
                              logicalConnector: (e.target as HTMLSelectElement)
                                .value as SearchCondition["logicalConnector"],
                            }
                            : c
                        );
                        onSearchConditionsChange(next);
                      }}
                    >
                      {LOGICAL_CONNECTOR_OPTIONS.map((o) => (
                        <option key={o.value} value={o.value}>{o.label}</option>
                      ))}
                    </select>
                  </label>
                )}
                <select
                  class="rounded border px-1 py-0.5 font-mono text-xs"
                  value={cond.column}
                  onChange={(e) => {
                    const next = searchConditions.map((c, i) =>
                      i === ci ? { ...c, column: (e.target as HTMLSelectElement).value } : c
                    );
                    onSearchConditionsChange(next);
                  }}
                >
                  <option value="">— 項目 —</option>
                  {columnKeys.map((col) => (
                    <option key={col.key} value={col.key}>{col.label}</option>
                  ))}
                </select>
                <select
                  class="rounded border px-1 py-0.5 text-xs"
                  value={cond.operator}
                  onChange={(e) => {
                    const op = (e.target as HTMLSelectElement).value as SearchOperator;
                    const next = searchConditions.map((c, i) =>
                      i === ci
                        ? { ...c, operator: op, value: undefined, valueTo: undefined, values: undefined }
                        : c
                    );
                    onSearchConditionsChange(next);
                  }}
                >
                  {SEARCH_OPERATOR_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
                {["is null", "is not null"].includes(cond.operator)
                  ? null
                  : cond.operator === "between"
                  ? (() => {
                    const colType = qualifiedColumns.find((q) => q.key === cond.column)?.column
                      .dataType ?? "";
                    const dateInput = isDateLikeType(colType);
                    const inputType = colType.trim().toLowerCase() === "date"
                      ? "date"
                      : dateInput
                      ? "datetime-local"
                      : undefined;
                    const inputClass = dateInput
                      ? "rounded border px-1 py-0.5 text-xs"
                      : "w-20 rounded border px-1 py-0.5 font-mono text-xs";
                    return (
                      <>
                        <input
                          type={inputType}
                          class={inputClass}
                          placeholder="から"
                          value={cond.value ?? ""}
                          onInput={(e) => {
                            const next = searchConditions.map((c, i) =>
                              i === ci ? { ...c, value: (e.target as HTMLInputElement).value } : c
                            );
                            onSearchConditionsChange(next);
                          }}
                        />
                        <span class="text-xs text-slate-500">〜</span>
                        <input
                          type={inputType}
                          class={inputClass}
                          placeholder="まで"
                          value={cond.valueTo ?? ""}
                          onInput={(e) => {
                            const next = searchConditions.map((c, i) =>
                              i === ci
                                ? { ...c, valueTo: (e.target as HTMLInputElement).value }
                                : c
                            );
                            onSearchConditionsChange(next);
                          }}
                        />
                      </>
                    );
                  })()
                  : ["in", "not in"].includes(cond.operator)
                  ? (
                    <input
                      class="w-40 rounded border px-1 py-0.5 font-mono text-xs"
                      placeholder="値1, 値2, …"
                      value={(cond.values ?? []).join(", ")}
                      onInput={(e) => {
                        const vals = (e.target as HTMLInputElement).value
                          .split(",")
                          .map((v) => v.trim())
                          .filter(Boolean);
                        const next = searchConditions.map((c, i) =>
                          i === ci ? { ...c, values: vals } : c
                        );
                        onSearchConditionsChange(next);
                      }}
                    />
                  )
                  : (
                    <input
                      class="w-28 rounded border px-1 py-0.5 font-mono text-xs"
                      placeholder="値"
                      value={cond.value ?? ""}
                      onInput={(e) => {
                        const next = searchConditions.map((c, i) =>
                          i === ci ? { ...c, value: (e.target as HTMLInputElement).value } : c
                        );
                        onSearchConditionsChange(next);
                      }}
                    />
                  )}
                <button
                  type="button"
                  class="text-xs text-red-500 hover:text-red-700"
                  onClick={() => {
                    onSearchConditionsChange(searchConditions.filter((_, i) => i !== ci));
                  }}
                >
                  削除
                </button>
              </div>
            ))}
            <button
              type="button"
              class="btn-secondary mt-1 text-xs"
              onClick={() => {
                onSearchConditionsChange([
                  ...searchConditions,
                  { column: defaultColumn, operator: "=" as SearchOperator, value: "" },
                ]);
              }}
            >
              {UX_FIELD_ADD_SEARCH_CONDITION}
            </button>
          </div>
        )}

      {completeMeasures.length > 0 && (
        <div class="mt-4 border-t border-slate-200 pt-3">
          <p class="mb-1 text-xs font-semibold text-slate-700">{UX_FIELD_HAVING_CONDITIONS}</p>
          <p class="mb-2 text-xs text-muted-xs">
            このブロックの集計結果に対する絞り込み条件です。
          </p>
          {havingConditions.map((hc, hi) => (
            <div
              key={hi}
              class="mb-2 flex flex-wrap items-center gap-2 rounded border border-slate-100 bg-white p-2 text-xs"
            >
              <select
                class="rounded border px-1 py-0.5 font-mono text-xs"
                value={`${hc.column}__${hc.function}`}
                onChange={(e) => {
                  const parts = (e.target as HTMLSelectElement).value.split("__");
                  const col = parts[0] ?? "";
                  const fn = parts[1] ?? "";
                  const next = havingConditions.map((h, i) =>
                    i === hi ? { ...h, column: col, function: fn } : h
                  );
                  onHavingConditionsChange(next);
                }}
              >
                <option value="__">— 集計式 —</option>
                {completeMeasures.map((m) => (
                  <option key={`${m.column}__${m.function}`} value={`${m.column}__${m.function}`}>
                    {m.function}({m.column})
                  </option>
                ))}
              </select>
              <select
                class="rounded border px-1 py-0.5 text-xs"
                value={hc.operator}
                onChange={(e) => {
                  const next = havingConditions.map((h, i) =>
                    i === hi
                      ? {
                        ...h,
                        operator: (e.target as HTMLSelectElement).value as HavingCondition["operator"],
                      }
                      : h
                  );
                  onHavingConditionsChange(next);
                }}
              >
                {HAVING_OPERATOR_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
              <input
                class="w-20 rounded border px-1 py-0.5 font-mono text-xs"
                placeholder="値"
                value={hc.value}
                onInput={(e) => {
                  const next = havingConditions.map((h, i) =>
                    i === hi ? { ...h, value: (e.target as HTMLInputElement).value } : h
                  );
                  onHavingConditionsChange(next);
                }}
              />
              <button
                type="button"
                class="text-xs text-red-500 hover:text-red-700"
                onClick={() => {
                  onHavingConditionsChange(havingConditions.filter((_, i) => i !== hi));
                }}
              >
                削除
              </button>
            </div>
          ))}
          <button
            type="button"
            class="btn-secondary mt-1 text-xs"
            onClick={() => {
              const firstMeasure = completeMeasures[0];
              onHavingConditionsChange([
                ...havingConditions,
                {
                  column: firstMeasure?.column ?? "",
                  function: firstMeasure?.function ?? "",
                  operator: ">",
                  value: "",
                },
              ]);
            }}
          >
            {UX_FIELD_HAVING_CONDITIONS}を追加
          </button>
        </div>
      )}
    </details>
  );
}
