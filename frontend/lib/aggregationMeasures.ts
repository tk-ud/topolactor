import type {
  HavingCondition,
  SearchCondition,
} from "./manifestScreenDesign.ts";
import { parseQualifiedColumnKey } from "./manifestLogicalTables.ts";
import type { QualifiedColumnRef } from "./manifestLogicalTables.ts";
import { columnOptionLabel } from "./contentDataFieldHelpers.ts";

/** One aggregation measure: column + function (sum, avg, max, min, count). */
export type AggregationMeasure = {
  column: string;
  function: string;
};

/** One aggregation block scoped to a logical / remote table (SQL source). */
export type AggregationBlock = {
  /** Logical table ref, e.g. employees or auth.user */
  sourceRef: string;
  aggregationKey: string;
  measures: AggregationMeasure[];
  /** Row filter before grouping (scoped to this block's source). */
  searchConditions: SearchCondition[];
  /** Post-aggregation filter on this block's measure results. */
  havingConditions: HavingCondition[];
};

export function emptyAggregationMeasure(): AggregationMeasure {
  return { column: "", function: "" };
}

export function emptyAggregationBlock(sourceRef = ""): AggregationBlock {
  return {
    sourceRef,
    aggregationKey: "",
    measures: [],
    searchConditions: [],
    havingConditions: [],
  };
}

/** Move legacy global conditions into the first block when blocks lack scoped conditions. */
export function hydrateAggregationBlockConditions(
  blocks: AggregationBlock[],
  globalSearch: SearchCondition[],
  globalHaving: HavingCondition[],
): AggregationBlock[] {
  if (blocks.length === 0) return blocks;
  const hasBlockSearch = blocks.some((b) => (b.searchConditions ?? []).length > 0);
  const hasBlockHaving = blocks.some((b) => (b.havingConditions ?? []).length > 0);
  return blocks.map((b, i) => {
    const searchConditions = b.searchConditions ?? [];
    const havingConditions = b.havingConditions ?? [];
    if (i === 0 && !hasBlockSearch && globalSearch.length > 0) {
      return { ...b, searchConditions: globalSearch, havingConditions };
    }
    if (i === 0 && !hasBlockHaving && globalHaving.length > 0) {
      return { ...b, havingConditions: globalHaving };
    }
    return {
      ...b,
      searchConditions,
      havingConditions,
    };
  });
}

export function sourceRefFromQualifiedKey(key: string): string {
  return parseQualifiedColumnKey(key).tableRef;
}

export function aggregationSourceRefs(
  qualifiedColumns: QualifiedColumnRef[],
): { ref: string; label: string }[] {
  const refs = new Map<string, string>();
  for (const q of qualifiedColumns) {
    const ref = q.tableRef.trim();
    if (!ref) continue;
    if (!refs.has(ref)) {
      refs.set(
        ref,
        q.remoteManifestId ? `${ref}（有効マニフェスト）` : ref,
      );
    }
  }
  return [...refs.entries()].map(([ref, label]) => ({ ref, label }));
}

export function columnOptionsForAggregationSource(
  qualifiedColumns: QualifiedColumnRef[],
  sourceRef: string,
): { key: string; label: string }[] {
  const ref = sourceRef.trim();
  return qualifiedColumns
    .filter((q) => !ref || q.tableRef === ref)
    .map((q) => ({ key: q.key, label: columnOptionLabel(q) }));
}

/** Migrate legacy single function + aggregationColumns into measures list. */
export function normalizeAggregationMeasures(input: {
  aggregationMeasures?: AggregationMeasure[];
  aggregationFunction?: string | null;
  aggregationColumns?: string[];
}): AggregationMeasure[] {
  if (Array.isArray(input.aggregationMeasures) && input.aggregationMeasures.length > 0) {
    return input.aggregationMeasures.filter((m) => m.column.trim() && m.function.trim());
  }
  const fn = (input.aggregationFunction ?? "").trim();
  const cols = input.aggregationColumns ?? [];
  if (!fn) return [];
  return cols.filter((c) => c.trim()).map((column) => ({ column: column.trim(), function: fn }));
}

export function formatMeasureLabel(m: AggregationMeasure): string {
  return `${m.column} ${m.function}`;
}

export function isCompleteAggregationMeasure(m: AggregationMeasure): boolean {
  return Boolean(m.column.trim() && m.function.trim());
}

/** Prefer aggregationBlocks; migrate legacy flat measures into one block. */
export function normalizeAggregationBlocks(input: {
  aggregationBlocks?: AggregationBlock[];
  aggregationKey?: string | null;
  aggregationMeasures?: AggregationMeasure[];
  aggregationFunction?: string | null;
  aggregationColumns?: string[];
  primarySourceRef?: string;
}): AggregationBlock[] {
  if (Array.isArray(input.aggregationBlocks) && input.aggregationBlocks.length > 0) {
    return input.aggregationBlocks
      .map((b) => ({
        sourceRef: b.sourceRef.trim(),
        aggregationKey: b.aggregationKey.trim(),
        // Keep in-progress rows (列/関数未選択) so 「集計式を追加」 survives patchDesign.
        measures: (b.measures ?? []).map((m) => ({
          column: (m.column ?? "").trim(),
          function: (m.function ?? "").trim(),
        })),
        searchConditions: Array.isArray(b.searchConditions) ? [...b.searchConditions] : [],
        havingConditions: Array.isArray(b.havingConditions) ? [...b.havingConditions] : [],
      }))
      .filter((b) =>
        b.sourceRef || b.aggregationKey || b.measures.length > 0
        || b.searchConditions.length > 0 || b.havingConditions.length > 0
      );
  }
  const measures = normalizeAggregationMeasures(input);
  const key = (input.aggregationKey ?? "").trim();
  if (measures.length === 0 && !key) return [];
  const sourceRef = measures[0]
    ? sourceRefFromQualifiedKey(measures[0].column)
    : (input.primarySourceRef ?? "").trim();
  return [{
    sourceRef,
    aggregationKey: key,
    measures,
    searchConditions: [],
    havingConditions: [],
  }];
}

/** Legacy flat fields derived from blocks for runtime consumers expecting aggregationKey + measures. */
export function flattenAggregationBlocks(blocks: AggregationBlock[]): {
  aggregationKey: string;
  aggregationMeasures: AggregationMeasure[];
} {
  const aggregationMeasures = blocks.flatMap((b) =>
    b.measures.filter(isCompleteAggregationMeasure)
  );
  const aggregationKey = blocks.find((b) => b.aggregationKey.trim())?.aggregationKey.trim() ?? "";
  return { aggregationKey, aggregationMeasures };
}
