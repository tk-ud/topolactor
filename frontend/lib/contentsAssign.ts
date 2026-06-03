import type { AdminManifestScreenDataShapeInput } from "../api/adminApi.ts";
import type { ManifestScreenDesignDraft } from "./manifestScreenDesign.ts";
import { parseSearchTargets } from "./manifestScreenDesign.ts";
import type { ScreenDataShapeSummary } from "./manifestTopologyExtensions.ts";
import type { ContentsPipelineStep } from "../components/ContentsPipelineStepper.tsx";
import { primaryOperationKind } from "../runtime/screenAuthoringIntent.ts";

/** Base payload from persisted backend shape (full entry replace on assign). */
function shapePayloadFromExisting(
  manifestId: string,
  existing: ScreenDataShapeSummary | null,
): AdminManifestScreenDataShapeInput {
  if (!existing) {
    return { manifestId };
  }
  const kinds = existing.screenOperationKinds?.length
    ? existing.screenOperationKinds
    : existing.screenOperationKind
    ? [existing.screenOperationKind]
    : [];
  return {
    manifestId,
    tableRef: existing.tableRef || undefined,
    dbTableName: existing.tableRef || undefined,
    importSchemaName: existing.importSchemaName || undefined,
    searchTargets: existing.searchTargets,
    searchKeyColumns: existing.searchKeyColumns,
    aggregationSpec: existing.aggregationSpec || undefined,
    aggregationKey: existing.aggregationKey || undefined,
    displayColumns: existing.displayColumns,
    columns: existing.columns,
    screenOperationKind: kinds[0],
    screenOperationKinds: kinds,
    userFacingTopologyLabel: existing.userFacingTopologyLabel || undefined,
    relationIntents: existing.relationIntents,
    operationEntityBindings: existing.operationEntityBindings,
    initialDataRows: existing.initialDataRows,
  };
}

/**
 * Step-scoped assign payload: only the current step's fields come from the draft;
 * all other screen_data_shape fields are taken from existing backend state.
 */
export function buildAssignPayloadForStep(
  step: ContentsPipelineStep,
  manifestId: string,
  design: ManifestScreenDesignDraft,
  existing: ScreenDataShapeSummary | null,
): AdminManifestScreenDataShapeInput {
  const base = shapePayloadFromExisting(manifestId, existing);

  if (step === 2) {
    return {
      manifestId,
      columns: design.columns.filter((c) => c.name.trim()),
      userFacingTopologyLabel: design.screenLabel.trim() || base.userFacingTopologyLabel,
      tableRef: base.tableRef,
      dbTableName: base.dbTableName,
      importSchemaName: base.importSchemaName,
      searchTargets: base.searchTargets,
      searchKeyColumns: base.searchKeyColumns,
      aggregationSpec: base.aggregationSpec,
      aggregationKey: base.aggregationKey,
      displayColumns: base.displayColumns,
      screenOperationKind: base.screenOperationKind,
      screenOperationKinds: base.screenOperationKinds,
      relationIntents: base.relationIntents,
      operationEntityBindings: base.operationEntityBindings,
      initialDataRows: base.initialDataRows,
    };
  }

  if (step === 2.5) {
    return {
      ...base,
      relationIntents: design.relationIntents.filter((r) => r.joinTableRef.trim()),
    };
  }

  const kinds = design.operationKinds.length > 0
    ? design.operationKinds
    : base.screenOperationKinds?.length
    ? base.screenOperationKinds
    : [primaryOperationKind(design)];

  return {
    ...base,
    tableRef: design.tableRef || base.tableRef,
    dbTableName: design.tableRef || base.tableRef,
    importSchemaName: design.importSchemaName || base.importSchemaName,
    searchTargets: design.searchKeyColumns.length > 0
      ? design.searchKeyColumns
      : parseSearchTargets(design.searchTargets).length > 0
      ? parseSearchTargets(design.searchTargets)
      : base.searchTargets,
    searchKeyColumns: design.searchKeyColumns.length > 0
      ? design.searchKeyColumns
      : parseSearchTargets(design.searchTargets).length > 0
      ? parseSearchTargets(design.searchTargets)
      : base.searchKeyColumns,
    aggregationSpec: design.aggregationSpec || base.aggregationSpec,
    aggregationKey: design.aggregationKey || base.aggregationKey,
    displayColumns: design.displayColumns.length > 0
      ? design.displayColumns
      : base.displayColumns,
    columns: base.columns,
    relationIntents: base.relationIntents,
    screenOperationKind: kinds[0],
    screenOperationKinds: kinds,
    userFacingTopologyLabel: design.screenLabel.trim() || base.userFacingTopologyLabel,
    operationEntityBindings: design.operationEntityBindings.filter((b) =>
      b.entityTargetColumn.trim()
    ).length > 0
      ? design.operationEntityBindings.filter((b) => b.entityTargetColumn.trim())
      : base.operationEntityBindings,
    initialDataRows: design.initialDataRows.length > 0
      ? design.initialDataRows
      : base.initialDataRows,
  };
}
