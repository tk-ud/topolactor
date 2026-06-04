import { JSX } from "preact";
import type { EnumDictionaryGroupDetail } from "../api/adminApi.ts";
import {
  diagnoseContentDataRows,
  type ConformanceColumnSpec,
  type ContentDataConformanceWarning,
} from "../lib/contentDataConformance.ts";
import {
  type ContentDataRowDraft,
  dataRowValues,
  markDataRowEdited,
  type DataRowLineageSource,
} from "../lib/manifestScreenDesign.ts";
import type { QualifiedColumnRef } from "../lib/manifestLogicalTables.ts";
import { UX_FIELD_ENUM_GROUP } from "../content/adminUxTerms.ts";

export type ContentsDataEditorProps = {
  qualifiedColumns: QualifiedColumnRef[];
  rows: ContentDataRowDraft[];
  enumGroupDetails: Record<string, EnumDictionaryGroupDetail>;
  unresolvedRelationColumnKeys?: ReadonlySet<string>;
  onRowsChange: (rows: ContentDataRowDraft[]) => void;
};

const SOURCE_LABELS: Record<DataRowLineageSource, string> = {
  manual: "手入力",
  import_preview: "取込プレビュー",
  import_applied: "取込適用",
  edited: "編集済",
};

function lineageLabel(row: ContentDataRowDraft): string {
  const base = SOURCE_LABELS[row.lineage.source] ?? row.lineage.source;
  if (row.lineage.snapshotId) {
    return `${base} (${row.lineage.snapshotId.slice(0, 8)}…)`;
  }
  return base;
}

function warningsForCell(
  warnings: ContentDataConformanceWarning[],
  rowIndex: number,
  columnKey: string,
): ContentDataConformanceWarning[] {
  return warnings.filter((w) =>
    w.rowIndex === rowIndex && w.columnKey === columnKey
  );
}

function warningsForRow(
  warnings: ContentDataConformanceWarning[],
  rowIndex: number,
): ContentDataConformanceWarning[] {
  return warnings.filter((w) => w.rowIndex === rowIndex && !w.columnKey);
}

export default function ContentsDataEditor({
  qualifiedColumns,
  rows,
  enumGroupDetails,
  unresolvedRelationColumnKeys,
  onRowsChange,
}: ContentsDataEditorProps): JSX.Element {
  const columnSpecs: ConformanceColumnSpec[] = qualifiedColumns.map((q) => ({
    key: q.key,
    dataType: q.column.dataType,
    nullable: q.column.nullable,
    enumGroupId: q.column.enumGroupId,
  }));

  const warnings = diagnoseContentDataRows(rows, columnSpecs, {
    isEnumGroupResolved: (groupId) => {
      const detail = enumGroupDetails[groupId];
      return Boolean(detail && detail.items.length > 0);
    },
    unresolvedRelationColumnKeys,
  });

  const patchRow = (rowIndex: number, colKey: string, value: string) => {
    const next = rows.map((row, i) => {
      if (i !== rowIndex) return row;
      return markDataRowEdited({
        ...row,
        values: { ...row.values, [colKey]: value },
      });
    });
    onRowsChange(next);
  };

  const removeRow = (rowIndex: number) => {
    onRowsChange(rows.filter((_, i) => i !== rowIndex));
  };

  const addRow = () => {
    const values: Record<string, string> = {};
    qualifiedColumns.forEach((q) => {
      values[q.key] = "";
    });
    onRowsChange([
      ...rows,
      { values, lineage: { source: "manual" } },
    ]);
  };

  if (qualifiedColumns.length === 0) {
    return (
      <p class="text-xs text-slate-400 italic">
        表示項目を設定してから初期データを追加してください（step 2）。
      </p>
    );
  }

  return (
    <div>
      {warnings.length > 0 && (
        <div
          class="mb-3 rounded border border-amber-200 bg-amber-50 p-2 text-xs text-amber-900"
          role="status"
          aria-label="型式診断（非ブロッキング）"
        >
          <p class="font-semibold">CI Attention — 型式・整合の注意 ({warnings.length})</p>
          <p class="text-amber-800">
            保存はブロックしません。値は topology intent として保持され、必要に応じて後から修正できます。
          </p>
          <ul class="mt-1 max-h-32 list-disc overflow-y-auto pl-4">
            {warnings.slice(0, 12).map((w, i) => (
              <li key={i}>
                行 {w.rowIndex + 1}
                {w.columnKey ? ` / ${w.columnKey}` : ""}: {w.message}
              </li>
            ))}
            {warnings.length > 12 && (
              <li class="list-none text-amber-700">…他 {warnings.length - 12} 件</li>
            )}
          </ul>
        </div>
      )}

      {rows.length > 0 && (
        <div class="mb-2 overflow-x-auto rounded border border-slate-200">
          <table class="min-w-full text-left text-xs">
            <thead>
              <tr>
                <th class="border-b px-2 py-1 bg-slate-50 font-semibold text-slate-600">
                  出所
                </th>
                {qualifiedColumns.map((q) => (
                  <th
                    key={q.key}
                    class="border-b px-2 py-1 font-semibold text-slate-600 bg-slate-50"
                  >
                    {q.key}
                  </th>
                ))}
                <th class="border-b px-2 py-1 bg-slate-50" />
              </tr>
            </thead>
            <tbody>
              {rows.map((row, ri) => {
                const rowWarnings = warningsForRow(warnings, ri);
                return (
                  <tr key={ri} class="border-b last:border-0 align-top">
                    <td class="px-1 py-1 text-slate-500 whitespace-nowrap">
                      {lineageLabel(row)}
                      {rowWarnings.length > 0 && (
                        <ul class="mt-0.5 text-amber-700">
                          {rowWarnings.map((w, wi) => (
                            <li key={wi}>{w.message}</li>
                          ))}
                        </ul>
                      )}
                    </td>
                    {qualifiedColumns.map((q) => {
                      const cellWarnings = warningsForCell(warnings, ri, q.key);
                      const groupId = q.column.enumGroupId?.trim();
                      const groupDetail = groupId
                        ? enumGroupDetails[groupId]
                        : undefined;
                      const values = dataRowValues(row);
                      if (groupId && (!groupDetail || groupDetail.items.length === 0)) {
                        return (
                          <td key={q.key} class="px-1 py-1">
                            <span class="text-xs text-red-600" role="alert">
                              {UX_FIELD_ENUM_GROUP}未解決
                            </span>
                          </td>
                        );
                      }
                      const inputClass = `w-full rounded border px-1 py-0.5 text-xs font-mono ${
                        cellWarnings.length > 0 ? "border-amber-400 bg-amber-50" : ""
                      }`;
                      if (groupId && groupDetail) {
                        return (
                          <td key={q.key} class="px-1 py-1">
                            <select
                              class={inputClass}
                              value={values[q.key] ?? ""}
                              onChange={(e) =>
                                patchRow(
                                  ri,
                                  q.key,
                                  (e.target as HTMLSelectElement).value,
                                )}
                            >
                              <option value="">（選択）</option>
                              {groupDetail.items.map((item) => (
                                <option key={item.indexNum} value={item.name}>
                                  {item.name}
                                </option>
                              ))}
                            </select>
                            {cellWarnings.map((w, wi) => (
                              <p key={wi} class="mt-0.5 text-amber-700">{w.message}</p>
                            ))}
                          </td>
                        );
                      }
                      return (
                        <td key={q.key} class="px-1 py-1">
                          <input
                            class={inputClass}
                            value={values[q.key] ?? ""}
                            onInput={(e) =>
                              patchRow(
                                ri,
                                q.key,
                                (e.target as HTMLInputElement).value,
                              )}
                          />
                          {cellWarnings.map((w, wi) => (
                            <p key={wi} class="mt-0.5 text-amber-700">{w.message}</p>
                          ))}
                        </td>
                      );
                    })}
                    <td class="px-1 py-1">
                      <button
                        type="button"
                        class="text-xs text-red-500 hover:text-red-700"
                        onClick={() => removeRow(ri)}
                        aria-label="削除"
                      >
                        削除
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      <button type="button" class="btn-secondary text-xs" onClick={addRow}>
        行を追加
      </button>
    </div>
  );
}
