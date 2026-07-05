import type { ScreenOperationKind } from "../runtime/screenAuthoringIntent.ts";
import type { OperationEntityBindingDraft } from "./manifestScreenDesign.ts";

export function columnsForOperationKind(
  bindings: OperationEntityBindingDraft[],
  kind: ScreenOperationKind,
): string[] {
  return bindings.find((b) => b.operationKind === kind)?.entityTargetColumns ?? [];
}

export function setColumnsForOperationKind(
  bindings: OperationEntityBindingDraft[],
  kind: ScreenOperationKind,
  cols: string[],
): OperationEntityBindingDraft[] {
  const existing = bindings.find((b) => b.operationKind === kind);
  if (existing) {
    return bindings.map((b) =>
      b.operationKind === kind ? { ...b, entityTargetColumns: [...cols] } : b
    );
  }
  return [...bindings, { operationKind: kind, entityTargetColumns: [...cols] }];
}

export function setColumnAcrossOperationKinds(
  bindings: OperationEntityBindingDraft[],
  col: string,
  kinds: ScreenOperationKind[],
  include: boolean,
): OperationEntityBindingDraft[] {
  let next = bindings;
  for (const kind of kinds) {
    const current = columnsForOperationKind(next, kind);
    const nextCols = include
      ? current.includes(col) ? current : [...current, col]
      : current.filter((c) => c !== col);
    next = setColumnsForOperationKind(next, kind, nextCols);
  }
  return next;
}

export function isColumnFullySelected(
  bindings: OperationEntityBindingDraft[],
  kind: ScreenOperationKind,
  columnNames: string[],
): boolean {
  if (columnNames.length === 0) return false;
  const selected = new Set(columnsForOperationKind(bindings, kind));
  return columnNames.every((col) => selected.has(col));
}

export function isRowFullySelected(
  bindings: OperationEntityBindingDraft[],
  col: string,
  operationKinds: ScreenOperationKind[],
): boolean {
  if (operationKinds.length === 0) return false;
  return operationKinds.every((kind) =>
    columnsForOperationKind(bindings, kind).includes(col)
  );
}
