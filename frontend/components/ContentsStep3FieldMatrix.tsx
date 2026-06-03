import { JSX } from "preact";
import {
  SCREEN_OPERATION_OPTIONS,
  type ScreenOperationKind,
} from "../runtime/screenAuthoringIntent.ts";
import {
  UX_FIELD_DISPLAY_COLUMNS,
  UX_FIELD_SEARCH_KEY,
  UX_FIELD_STEP3_COLUMN_USAGE,
} from "../content/adminUxTerms.ts";
import type { ManifestScreenDesignDraft } from "../lib/manifestScreenDesign.ts";

export type ContentsStep3FieldMatrixProps = {
  columnNames: string[];
  operationKinds: ScreenOperationKind[];
  searchKeyColumns: string[];
  displayColumns: string[];
  operationEntityBindings: ManifestScreenDesignDraft["operationEntityBindings"];
  onToggleSearch: (col: string) => void;
  onToggleDisplay: (col: string) => void;
  onToggleOperation: (kind: ScreenOperationKind, col: string) => void;
};

export default function ContentsStep3FieldMatrix({
  columnNames,
  operationKinds,
  searchKeyColumns,
  displayColumns,
  operationEntityBindings,
  onToggleSearch,
  onToggleDisplay,
  onToggleOperation,
}: ContentsStep3FieldMatrixProps): JSX.Element {
  if (columnNames.length === 0) {
    return (
      <p class="text-xs text-slate-400 italic">
        Step 2 で項目を定義してください。
      </p>
    );
  }

  const opLabels = operationKinds.map((kind) =>
    SCREEN_OPERATION_OPTIONS.find((o) => o.kind === kind)?.label ?? kind
  );

  const isOpChecked = (kind: ScreenOperationKind, col: string): boolean => {
    const binding = operationEntityBindings.find((b) => b.operationKind === kind);
    return binding?.entityTargetColumns.includes(col) ?? false;
  };

  return (
    <div class="overflow-x-auto rounded border border-slate-200">
      <p class="mb-2 text-xs font-semibold text-slate-700">{UX_FIELD_STEP3_COLUMN_USAGE}</p>
      <p class="mb-2 text-xs text-muted-xs">
        検索キー・各操作・表示列を1つの表で設定します（項目リストの繰り返し表示を避けます）。
      </p>
      <table class="min-w-full text-left text-xs">
        <thead>
          <tr class="bg-slate-50">
            <th class="border-b px-2 py-1 font-semibold text-slate-600">項目</th>
            <th class="border-b px-2 py-1 font-semibold text-slate-600">
              {UX_FIELD_SEARCH_KEY}
            </th>
            {opLabels.map((label, i) => (
              <th
                key={operationKinds[i]}
                class="border-b px-2 py-1 font-semibold text-slate-600"
              >
                {label}
              </th>
            ))}
            <th class="border-b px-2 py-1 font-semibold text-slate-600">
              {UX_FIELD_DISPLAY_COLUMNS}
            </th>
          </tr>
        </thead>
        <tbody>
          {columnNames.map((col) => (
            <tr key={col} class="border-b last:border-0">
              <td class="px-2 py-1 font-mono text-slate-800">{col}</td>
              <td class="px-2 py-1 text-center">
                <input
                  type="checkbox"
                  checked={searchKeyColumns.includes(col)}
                  onChange={() => onToggleSearch(col)}
                  aria-label={`${col} 検索`}
                />
              </td>
              {operationKinds.map((kind) => (
                <td key={`${kind}-${col}`} class="px-2 py-1 text-center">
                  <input
                    type="checkbox"
                    checked={isOpChecked(kind, col)}
                    onChange={() => onToggleOperation(kind, col)}
                    aria-label={`${col} ${kind}`}
                  />
                </td>
              ))}
              <td class="px-2 py-1 text-center">
                <input
                  type="checkbox"
                  checked={displayColumns.includes(col)}
                  onChange={() => onToggleDisplay(col)}
                  aria-label={`${col} 表示`}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
