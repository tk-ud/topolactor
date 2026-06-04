import { JSX } from "preact";
import {
  SCREEN_OPERATION_OPTIONS,
  type ScreenOperationKind,
} from "../runtime/screenAuthoringIntent.ts";
import {
  UX_FIELD_OPERATION_ENTITY,
} from "../content/adminUxTerms.ts";
import type { ManifestScreenDesignDraft } from "../lib/manifestScreenDesign.ts";

export type ContentsStep3FieldMatrixProps = {
  columnNames: string[];
  operationKinds: ScreenOperationKind[];
  operationEntityBindings: ManifestScreenDesignDraft["operationEntityBindings"];
  onToggleOperation: (kind: ScreenOperationKind, col: string) => void;
};

export default function ContentsStep3FieldMatrix({
  columnNames,
  operationKinds,
  operationEntityBindings,
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
      <p class="mb-2 text-xs font-semibold text-slate-700">{UX_FIELD_OPERATION_ENTITY}</p>
      <p class="mb-2 text-xs text-muted-xs">
        操作種別ごとに対象項目を選びます。選んだ列がサンプル表と保存時の表示列に反映されます。
      </p>
      <table class="min-w-full text-left text-xs">
        <thead>
          <tr class="bg-slate-50">
            <th class="border-b px-2 py-1 font-semibold text-slate-600">項目</th>
            {opLabels.map((label, i) => (
              <th
                key={operationKinds[i]}
                class="border-b px-2 py-1 font-semibold text-slate-600"
              >
                {label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {columnNames.map((col) => (
            <tr key={col} class="border-b last:border-0">
              <td class="px-2 py-1 font-mono text-slate-800">{col}</td>
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
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
