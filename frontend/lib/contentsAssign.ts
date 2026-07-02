import type { AggregateTriggerDefinitionPayload } from "./aggregateTriggerAuthoring.ts";
import type { AdminManifestScreenDataShapeInput } from "../api/adminApi.ts";
import type { ManifestScreenDesignDraft } from "./manifestScreenDesign.ts";
import { serializeContentDataRowsForShape } from "./manifestScreenDesign.ts";
import { parseSearchTargets } from "./manifestScreenDesign.ts";
import { normalizeRelationKeyColumn } from "./manifestLogicalTables.ts";
import type { ScreenDataShapeSummary } from "./manifestTopologyExtensions.ts";
import type { ContentsPipelineStep } from "../components/ContentsPipelineStepper.tsx";
import { isEnumBackedColumnDataType } from "../content/adminUxTerms.ts";
import { primaryOperationKind } from "../runtime/screenAuthoringIntent.ts";
import {
  enrichRelationIntentsWithRemoteTargets,
  primaryLogicalTableRef,
  primaryTableColumns,
} from "./manifestLogicalTables.ts";
import type { Step3RemoteTargetManifest } from "./manifestLogicalTables.ts";
import {
  assignAggregationBlocksFromDesign,
  assignHavingConditionsFromDesign,
  assignSearchConditionsFromDesign,
} from "./screenReadQueryWiring.ts";

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
    aggregationColumns: existing.aggregationColumns,
    aggregationFunction: existing.aggregationFunction || undefined,
    aggregationMeasures: existing.aggregationMeasures.length > 0
      ? existing.aggregationMeasures
      : undefined,
    aggregationBlocks: existing.aggregationBlocks.length > 0
      ? existing.aggregationBlocks
      : undefined,
    logicalTables: existing.logicalTables.length > 0
      ? existing.logicalTables
      : undefined,
    columns: existing.columns,
    screenOperationKind: kinds[0],
    screenOperationKinds: kinds,
    userFacingTopologyLabel: existing.userFacingTopologyLabel || undefined,
    relationIntents: existing.relationIntents,
    operationEntityBindings: existing.operationEntityBindings.map((b) => ({
      operationKind: b.operationKind,
      entityTargetColumns: b.entityTargetColumns.length > 0
        ? b.entityTargetColumns
        : b.entityTargetColumn
        ? [b.entityTargetColumn]
        : [],
    })),
    initialDataRows: existing.initialDataRows,
    searchConditions: existing.searchConditions ?? [],
    havingConditions: existing.havingConditions ?? [],
    displayColumnMode: existing.displayColumnMode ?? undefined,
  };
}

/**
 * Step-scoped assign payload: only the current step's fields come from the draft;
 * all other screen_data_shape fields are taken from existing backend state.
 */
export type BuildAssignPayloadOptions = {
  /** Active manifests from list_relationship_remote_targets (Step 2.5 infer). */
  relationshipRemoteTargets?: Step3RemoteTargetManifest[];
  aggregateTriggerDefinitions?: AggregateTriggerDefinitionPayload[];
};

export function buildAssignPayloadForStep(
  step: ContentsPipelineStep,
  manifestId: string,
  design: ManifestScreenDesignDraft,
  existing: ScreenDataShapeSummary | null,
  options?: BuildAssignPayloadOptions,
): AdminManifestScreenDataShapeInput {
  const base = shapePayloadFromExisting(manifestId, existing);

  if (step === 2) {
    const logicalTables = design.logicalTables.map((t) => ({
      tableName: t.tableName.trim(),
      columns: t.columns.filter((c) => c.name.trim()).map((c) => ({
        name: c.name.trim(),
        dataType: c.dataType,
        nullable: c.nullable,
        enumGroupId: isEnumBackedColumnDataType(c.dataType)
          ? c.enumGroupId?.trim() || undefined
          : undefined,
      })),
    })).filter((t) => t.columns.length > 0);
    const legacyColumns = primaryTableColumns(design.logicalTables)
      .filter((c) => c.name.trim());
    return {
      manifestId,
      logicalTables: logicalTables.length > 0 ? logicalTables : undefined,
      columns: legacyColumns.length > 0 ? legacyColumns : undefined,
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
      searchConditions: base.searchConditions,
      havingConditions: base.havingConditions,
      displayColumnMode: base.displayColumnMode,
    };
  }

  if (step === 2.5) {
    const remoteTargets: Step3RemoteTargetManifest[] =
      options?.relationshipRemoteTargets ?? [];
    const enriched = enrichRelationIntentsWithRemoteTargets(
      design.relationIntents,
      design.logicalTables,
      remoteTargets,
    );
    return {
      ...base,
      relationIntents: enriched
        .filter((r) => r.joinTableRef.trim() && r.localKey.trim() && r.remoteKey.trim())
        .map((r) => ({
          localTableRef: r.localTableRef.trim(),
          joinTableRef: r.joinTableRef.trim(),
          localKey: normalizeRelationKeyColumn(r.localKey),
          remoteKey: normalizeRelationKeyColumn(r.remoteKey),
          remoteManifestId: r.remoteManifestId?.trim() || undefined,
        })),
    };
  }

  const kinds = design.operationKinds.length > 0
    ? design.operationKinds
    : base.screenOperationKinds?.length
    ? base.screenOperationKinds
    : [primaryOperationKind(design)];

  const derivedTableRef = primaryLogicalTableRef(design.logicalTables) ||
    base.tableRef ||
    undefined;

  return {
    ...base,
    tableRef: derivedTableRef,
    dbTableName: derivedTableRef,
    importSchemaName: base.importSchemaName,
    searchTargets: design.searchKeyColumns.length > 0
      ? design.searchKeyColumns
      : parseSearchTargets(design.searchTargets).length > 0
      ? parseSearchTargets(design.searchTargets)
      : base.searchTargets,
    searchKeyColumns: design.searchKeyColumns,
    aggregationSpec: design.aggregationSpec || base.aggregationSpec,
    aggregationKey: design.aggregationKey,
    aggregationMeasures: design.aggregationMeasures.filter((m) =>
      m.column.trim() && m.function.trim()
    ),
    aggregationBlocks: assignAggregationBlocksFromDesign(design),
    displayColumns: design.displayColumns,
    columns: base.columns,
    logicalTables: base.logicalTables,
    relationIntents: base.relationIntents,
    screenOperationKind: kinds[0],
    screenOperationKinds: kinds,
    userFacingTopologyLabel: design.screenLabel.trim() || base.userFacingTopologyLabel,
    operationEntityBindings: design.operationEntityBindings.map((b) => ({
      operationKind: b.operationKind,
      entityTargetColumns: b.entityTargetColumns.filter((c) => c.trim()),
    })),
    initialDataRows: design.initialDataRows.length > 0
      ? serializeContentDataRowsForShape(design.initialDataRows)
      : base.initialDataRows,
    searchConditions: assignSearchConditionsFromDesign(design),
    havingConditions: assignHavingConditionsFromDesign(design),
    displayColumnMode: design.displayColumnMode,
    aggregateTriggerDefinitions: options?.aggregateTriggerDefinitions ?? [],
  };
}
