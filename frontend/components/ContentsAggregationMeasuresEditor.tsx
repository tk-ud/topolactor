import { JSX } from "preact";
import {
  AGGREGATION_FUNCTION_OPTIONS,
  UX_FIELD_AGGREGATION_KEY,
  UX_FIELD_AGGREGATION_MEASURES,
} from "../content/adminUxTerms.ts";
import AggregationBlockConditionsEditor from "./AggregationBlockConditionsEditor.tsx";
import {
  aggregationSourceRefs,
  columnOptionsForAggregationSource,
  emptyAggregationBlock,
  emptyAggregationMeasure,
  type AggregationBlock,
  type AggregationMeasure,
} from "../lib/aggregationMeasures.ts";
import type { QualifiedColumnRef } from "../lib/manifestLogicalTables.ts";

export type ContentsAggregationMeasuresEditorProps = {
  qualifiedColumns: QualifiedColumnRef[];
  blocks: AggregationBlock[];
  onBlocksChange: (blocks: AggregationBlock[]) => void;
};

function patchBlock(
  blocks: AggregationBlock[],
  index: number,
  patch: Partial<AggregationBlock>,
): AggregationBlock[] {
  return blocks.map((b, i) => (i === index ? { ...b, ...patch } : b));
}

function patchMeasure(
  blocks: AggregationBlock[],
  blockIndex: number,
  measureIndex: number,
  patch: Partial<AggregationMeasure>,
): AggregationBlock[] {
  return blocks.map((b, bi) => {
    if (bi !== blockIndex) return b;
    return {
      ...b,
      measures: b.measures.map((m, mi) =>
        mi === measureIndex ? { ...m, ...patch } : m
      ),
    };
  });
}

export default function ContentsAggregationMeasuresEditor({
  qualifiedColumns,
  blocks: blocksProp,
  onBlocksChange,
}: ContentsAggregationMeasuresEditorProps): JSX.Element {
  const blocks = blocksProp ?? [];
  const sourceOptions = aggregationSourceRefs(qualifiedColumns);

  const addBlock = () => {
    const nextRef = sourceOptions[blocks.length]?.ref ?? sourceOptions[0]?.ref ?? "";
    onBlocksChange([...blocks, emptyAggregationBlock(nextRef)]);
  };

  const removeBlock = (index: number) => {
    onBlocksChange(blocks.filter((_, i) => i !== index));
  };

  const addMeasure = (blockIndex: number) => {
    const block = blocks[blockIndex];
    if (!block) return;
    onBlocksChange(patchBlock(blocks, blockIndex, {
      measures: [...block.measures, emptyAggregationMeasure()],
    }));
  };

  const removeMeasure = (blockIndex: number, measureIndex: number) => {
    const block = blocks[blockIndex];
    if (!block) return;
    onBlocksChange(patchBlock(blocks, blockIndex, {
      measures: block.measures.filter((_, i) => i !== measureIndex),
    }));
  };

  return (
    <div class="space-y-3">
      <p class="text-xs text-muted-xs">
        SQL ソース（論理テーブル / 有効マニフェスト列）ごとに集計ブロックを追加できます。
        ブロック内で集計キー・集計式・検索条件・HAVING を設定します。
      </p>

      {blocks.length === 0
        ? (
          <p class="text-xs italic text-slate-400">
            「集計ブロックを追加」から登録してください。
          </p>
        )
        : (
          <div class="space-y-3">
            {blocks.map((block, bi) => {
              const columnOptions = columnOptionsForAggregationSource(
                qualifiedColumns,
                block.sourceRef,
              );
              const sourceLabel = (
                sourceOptions.find((s) => s.ref === block.sourceRef)?.label
                ?? block.sourceRef
              ) || "（ソース未選択）";
              return (
                <div
                  key={bi}
                  class="rounded border border-slate-200 bg-white p-3 space-y-2"
                >
                  <div class="flex flex-wrap items-center justify-between gap-2">
                    <p class="text-xs font-semibold text-slate-700">
                      集計ブロック {bi + 1}
                      <span class="ml-1 font-normal text-slate-500">— {sourceLabel}</span>
                    </p>
                    <button
                      type="button"
                      class="text-xs text-red-500 hover:text-red-700"
                      onClick={() => removeBlock(bi)}
                    >
                      ブロックを削除
                    </button>
                  </div>

                  <label class="block text-xs">
                    SQL ソース（論理テーブル）
                    <select
                      class="mt-1 w-full max-w-md rounded border px-2 py-1 text-xs"
                      value={block.sourceRef}
                      onChange={(e) => {
                        const sourceRef = (e.target as HTMLSelectElement).value;
                        const nextOptions = columnOptionsForAggregationSource(
                          qualifiedColumns,
                          sourceRef,
                        );
                        const keyStillValid = nextOptions.some((c) =>
                          c.key === block.aggregationKey
                        );
                        const measuresStillValid = block.measures.map((m) => ({
                          ...m,
                          column: nextOptions.some((c) => c.key === m.column) ? m.column : "",
                        }));
                        onBlocksChange(patchBlock(blocks, bi, {
                          sourceRef,
                          aggregationKey: keyStillValid ? block.aggregationKey : "",
                          measures: measuresStillValid,
                        }));
                      }}
                    >
                      <option value="">— ソース —</option>
                      {sourceOptions.map((s) => (
                        <option key={s.ref} value={s.ref}>{s.label}</option>
                      ))}
                    </select>
                  </label>

                  <label class="block text-xs">
                    {UX_FIELD_AGGREGATION_KEY}（グループ化・任意）
                    <select
                      class="mt-1 w-full max-w-xs rounded border px-2 py-1 text-xs"
                      value={block.aggregationKey}
                      onChange={(e) =>
                        onBlocksChange(patchBlock(blocks, bi, {
                          aggregationKey: (e.target as HTMLSelectElement).value,
                        }))}
                    >
                      <option value="">— なし（全体） —</option>
                      {columnOptions.map((c) => (
                        <option key={c.key} value={c.key}>{c.label}</option>
                      ))}
                    </select>
                  </label>

                  <div>
                    <p class="mb-1 text-xs font-semibold text-slate-700">
                      {UX_FIELD_AGGREGATION_MEASURES}
                    </p>
                    {block.measures.length === 0
                      ? (
                        <p class="text-xs italic text-slate-400">
                          「集計式を追加」から登録してください。
                        </p>
                      )
                      : (
                        <div class="space-y-2">
                          {block.measures.map((m, mi) => (
                            <div
                              key={mi}
                              class="flex flex-wrap items-center gap-2 rounded border border-slate-100 p-2"
                            >
                              <select
                                class="rounded border px-2 py-1 text-xs font-mono"
                                value={m.column}
                                onChange={(e) =>
                                  onBlocksChange(patchMeasure(blocks, bi, mi, {
                                    column: (e.target as HTMLSelectElement).value,
                                  }))}
                              >
                                <option value="">— 列 —</option>
                                {columnOptions.map((c) => (
                                  <option key={c.key} value={c.key}>{c.label}</option>
                                ))}
                              </select>
                              <select
                                class="rounded border px-2 py-1 text-xs"
                                value={m.function}
                                onChange={(e) =>
                                  onBlocksChange(patchMeasure(blocks, bi, mi, {
                                    function: (e.target as HTMLSelectElement).value,
                                  }))}
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
                                onClick={() => removeMeasure(bi, mi)}
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
                      onClick={() => addMeasure(bi)}
                    >
                      集計式を追加
                    </button>
                  </div>

                  <AggregationBlockConditionsEditor
                    columnKeys={columnOptions}
                    qualifiedColumns={qualifiedColumns}
                    measures={block.measures}
                    searchConditions={block.searchConditions ?? []}
                    havingConditions={block.havingConditions ?? []}
                    onSearchConditionsChange={(searchConditions) =>
                      onBlocksChange(patchBlock(blocks, bi, { searchConditions }))}
                    onHavingConditionsChange={(havingConditions) =>
                      onBlocksChange(patchBlock(blocks, bi, { havingConditions }))}
                  />
                </div>
              );
            })}
          </div>
        )}

      <button
        type="button"
        class="btn-secondary text-xs"
        onClick={addBlock}
        disabled={sourceOptions.length === 0}
      >
        集計ブロックを追加
      </button>
    </div>
  );
}
