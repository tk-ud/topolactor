import { JSX } from "preact";
import { useState } from "preact/hooks";
import {
  UX_FIELD_AGGREGATION_KEY,
  UX_FIELD_AGGREGATION_MEASURES,
  UX_FIELD_ADVANCED_SEARCH_CONDITIONS,
  UX_FIELD_SEARCH_KEY,
} from "../content/adminUxTerms.ts";
import type { AggregationBlock } from "../lib/aggregationMeasures.ts";
import {
  formatAggregationSpecFromBlocks,
  formatSearchTargetsRaw,
  parseAggregationSpecToBlocks,
  parseSearchTargetsToColumns,
} from "../lib/screenDataShapeRawBinding.ts";

export type ProRawScreenDataShapeHintsProps = {
  exampleQualifiedColumn: string;
  searchKeyColumns: string[];
  aggregationBlocks: AggregationBlock[];
  defaultSourceRef: string;
  onSearchKeyColumnsChange: (columns: string[]) => void;
  onAggregationBlocksChange: (blocks: AggregationBlock[]) => void;
};

function SqlClauseMap({
  clauses,
}: {
  clauses: { clause: string; source: string; note?: string }[];
}): JSX.Element {
  return (
    <dl class="mt-2 space-y-1.5 rounded border border-slate-200 bg-white p-2 font-mono text-[0.65rem] leading-relaxed text-slate-800">
      {clauses.map(({ clause, source, note }) => (
        <div key={clause} class="grid gap-0.5 sm:grid-cols-[5.5rem_1fr]">
          <dt class="font-semibold text-slate-600">{clause}</dt>
          <dd class="m-0">
            <span>{source}</span>
            {note && (
              <span class="mt-0.5 block font-sans text-[0.6rem] text-slate-500">
                {note}
              </span>
            )}
          </dd>
        </div>
      ))}
    </dl>
  );
}

/**
 * Pro-facing raw fields bound bidirectionally to structured Step 3 UI.
 * SSOT: docs/design/admin-console-workflow-ssot.yaml §step_3 progressive_disclosure_ui.pro_disclosed
 */
export default function ProRawScreenDataShapeHints({
  exampleQualifiedColumn,
  searchKeyColumns,
  aggregationBlocks,
  defaultSourceRef,
  onSearchKeyColumnsChange,
  onAggregationBlocksChange,
}: ProRawScreenDataShapeHintsProps): JSX.Element {
  const tableRef = exampleQualifiedColumn.includes(".")
    ? exampleQualifiedColumn.split(".")[0]
    : "employees";
  const secondCol = exampleQualifiedColumn.includes(".")
    ? `${tableRef}.status`
    : `${tableRef}_status`;

  const derivedSearch = formatSearchTargetsRaw(searchKeyColumns);
  const derivedAggregation = formatAggregationSpecFromBlocks(aggregationBlocks);

  const [searchDraft, setSearchDraft] = useState<string | null>(null);
  const [aggDraft, setAggDraft] = useState<string | null>(null);
  const [aggParseError, setAggParseError] = useState<string | null>(null);
  const commitSearch = (raw: string) => {
    onSearchKeyColumnsChange(parseSearchTargetsToColumns(raw));
    setSearchDraft(null);
  };

  const commitAggregation = (raw: string) => {
    const trimmed = raw.trim();
    if (!trimmed) {
      onAggregationBlocksChange([]);
      setAggParseError(null);
      setAggDraft(null);
      return;
    }
    const parsed = parseAggregationSpecToBlocks(trimmed, defaultSourceRef);
    if (!parsed.ok) {
      setAggParseError(parsed.error);
      return;
    }
    setAggParseError(null);
    onAggregationBlocksChange(parsed.blocks);
    setAggDraft(null);
  };

  return (
    <details class="mt-2 rounded border border-slate-200 bg-slate-50 p-3">
      <summary class="cursor-pointer text-xs font-semibold text-slate-700">
        プロ向け / raw 入力（検索・集計 — UI と双方向同期）
      </summary>

      <div class="mt-3 space-y-4 text-xs text-slate-700">
        <p class="m-0 rounded border border-blue-200 bg-blue-50 px-2 py-1.5 text-blue-900">
          この欄は上の構造化 UI と<strong>同じ値</strong>を表示します。編集すると検索キー列・集計ブロックに反映され、サンプル表示も連動します。
        </p>

        <section>
          <h4 class="m-0 text-xs font-semibold text-slate-800">
            検索対象（searchTargets ↔ {UX_FIELD_SEARCH_KEY}）
          </h4>
          <p class="mt-1 text-muted-xs">
            保存先: <code>screen_data_shape.searchTargets[]</code>。
            上の「{UX_FIELD_SEARCH_KEY}」列選択とこの raw は同じ <code>searchKeyColumns</code> にバインドされます。
          </p>
          <SqlClauseMap
            clauses={[
              {
                clause: "WHERE",
                source: `検索時に ${exampleQualifiedColumn} 等が左辺になる（演算子・値は別 UI）`,
                note: "詳細条件は集計ブロック内 searchConditions",
              },
            ]}
          />
          <label class="mt-2 block text-xs text-slate-600">
            searchTargets（列名のみ — UI 同期）
            <input
              class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
              value={searchDraft ?? derivedSearch}
              placeholder={`${exampleQualifiedColumn}, ${secondCol}`}
              onFocus={() => setSearchDraft(derivedSearch)}
              onInput={(e) =>
                setSearchDraft((e.target as HTMLInputElement).value)}
              onBlur={(e) => commitSearch((e.target as HTMLInputElement).value)}
            />
          </label>
        </section>

        <section class="border-t border-slate-200 pt-3">
          <h4 class="m-0 text-xs font-semibold text-slate-800">
            集計仕様（aggregationSpec ↔ 集計ブロック）
          </h4>
          <p class="mt-1 text-muted-xs">
            保存先: <code>screen_data_shape.aggregationSpec</code>。
            上の集計ブロックエディタと双方向同期。サンプル表示は構造化ブロックを駆動し、この raw はその SQL 形の写像です。
          </p>
          <pre class="mt-1 overflow-x-auto rounded border border-slate-300 bg-white p-2 font-mono text-[0.62rem] leading-relaxed text-slate-800">{`FROM    {ソース}
SELECT  {${UX_FIELD_AGGREGATION_KEY}}, {${UX_FIELD_AGGREGATION_MEASURES}}
WHERE   {${UX_FIELD_ADVANCED_SEARCH_CONDITIONS}}
GROUP BY {${UX_FIELD_AGGREGATION_KEY}}
HAVING  {havingConditions}

複数ブロックは --- で区切る`}</pre>
          {aggParseError && (
            <p class="mt-2 rounded border border-red-200 bg-red-50 px-2 py-1 text-red-800">
              解釈エラー: {aggParseError}
            </p>
          )}
          <label class="mt-2 block text-xs text-slate-600">
            aggregationSpec（SQL 形 — UI 同期）
            <textarea
              class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
              rows={6}
              value={aggDraft ?? derivedAggregation}
              placeholder={`FROM ${tableRef}\nSELECT ${tableRef}.dept, sum(${tableRef}.salary)\nWHERE ${tableRef}.status = 'active'\nGROUP BY ${tableRef}.dept`}
              onFocus={() => {
                setAggDraft(derivedAggregation);
                setAggParseError(null);
              }}
              onInput={(e) =>
                setAggDraft((e.target as HTMLTextAreaElement).value)}
              onBlur={(e) => commitAggregation((e.target as HTMLTextAreaElement).value)}
            />
          </label>
        </section>
      </div>
    </details>
  );
}
