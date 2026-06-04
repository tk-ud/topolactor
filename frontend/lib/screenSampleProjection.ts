import type {
  ManifestScreenDesignDraft,
  OperationEntityBindingDraft,
} from "./manifestScreenDesign.ts";
import type { ScreenOperationKind } from "../runtime/screenAuthoringIntent.ts";

/** Union of entityTargetColumns for selected operation kinds, stable column order. */
export function projectionColumnsFromOperationBindings(
  operationKinds: ScreenOperationKind[],
  operationEntityBindings: OperationEntityBindingDraft[],
  columnOrder: string[],
): string[] {
  const selected = new Set<string>();
  for (const kind of operationKinds) {
    const binding = operationEntityBindings.find((b) => b.operationKind === kind);
    for (const col of binding?.entityTargetColumns ?? []) {
      const key = col.trim();
      if (key) selected.add(key);
    }
  }
  return columnOrder.filter((key) => selected.has(key));
}

/** Keep displayColumns / displayColumnMode aligned with operation bindings for save + runtime. */
export function displayShapePatchFromOperationBindings(
  design: Pick<
    ManifestScreenDesignDraft,
    "operationKinds" | "operationEntityBindings" | "displayColumnMode"
  >,
  columnOrder: string[],
): Pick<ManifestScreenDesignDraft, "displayColumns" | "displayColumnMode"> {
  const displayColumns = projectionColumnsFromOperationBindings(
    design.operationKinds,
    design.operationEntityBindings,
    columnOrder,
  );
  if (displayColumns.length > 0) {
    return { displayColumns, displayColumnMode: "selected" };
  }
  if (design.displayColumnMode === "none") {
    return { displayColumns: [], displayColumnMode: "none" };
  }
  return { displayColumns: [], displayColumnMode: "selected" };
}

/** One-time: legacy displayColumns → 一覧 operation binding when bindings empty. */
export function hydrateOperationBindingsFromLegacyDisplayColumns(
  design: ManifestScreenDesignDraft,
): ManifestScreenDesignDraft {
  const hasBindingColumns = design.operationEntityBindings.some(
    (b) => b.entityTargetColumns.length > 0,
  );
  if (hasBindingColumns || design.displayColumns.length === 0) return design;

  const primaryKind = design.operationKinds[0] ?? "list";
  const existing = design.operationEntityBindings.find((b) =>
    b.operationKind === primaryKind
  );
  const nextBindings = existing
    ? design.operationEntityBindings.map((b) =>
      b.operationKind === primaryKind
        ? { ...b, entityTargetColumns: [...design.displayColumns] }
        : b
    )
    : [
      ...design.operationEntityBindings,
      {
        operationKind: primaryKind,
        entityTargetColumns: [...design.displayColumns],
      },
    ];

  return { ...design, operationEntityBindings: nextBindings };
}
