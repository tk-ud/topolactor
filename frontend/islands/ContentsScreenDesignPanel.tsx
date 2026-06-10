import { useEffect, useRef, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  type AdminManifestDetail,
  type AdminManifestListItem,
  assignAdminManifestScreenDataShape,
  createAdminManifestDraft,
  getAdminManifest,
  getEnumDictionaryGroup,
  listAdminManifests,
  listEnumDictionaryGroups,
  listRelationshipRemoteTargets,
  type EnumDictionaryGroupDetail,
  type RelationshipRemoteTarget,
} from "../api/adminApi.ts";
import { AdminSubmitStatus } from "../components/AdminSubmitStatus.tsx";
import ContentsAggregationMeasuresEditor from "../components/ContentsAggregationMeasuresEditor.tsx";
import ProRawScreenDataShapeHints from "../components/ProRawScreenDataShapeHints.tsx";
import { syncRawFieldsFromStructured } from "../lib/screenDataShapeRawBinding.ts";
import ContentsStepCompletionBanner from "../components/ContentsStepCompletionBanner.tsx";
import ContentsPipelineStepper, {
  type ContentsPipelineStep,
} from "../components/ContentsPipelineStepper.tsx";
import ContentsDataEditor from "../components/ContentsDataEditor.tsx";
import ContentsStep3FieldMatrix from "../components/ContentsStep3FieldMatrix.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { buildAssignPayloadForStep } from "../lib/contentsAssign.ts";
import {
  displayShapePatchFromOperationBindings,
  hydrateOperationBindingsFromLegacyDisplayColumns,
  projectionColumnsForOperationKind,
  projectionColumnsFromOperationBindings,
} from "../lib/screenSampleProjection.ts";
import {
  aggregationSourceRefs,
  formatMeasureLabel,
  type AggregationBlock,
} from "../lib/aggregationMeasures.ts";
import {
  normalizeRelationKeyColumn,
  relationKeyColumnOptionsForTableRef,
  defaultLocalTableRef,
  emptyLogicalTable,
  qualifiedColumnKey,
  qualifiedColumnsFromLogicalTables,
  type QualifiedColumnRef,
  enrichRelationIntentsWithRemoteTargets,
  step3FieldSourceFromDesign,
  qualifyScreenDesignColumnKeys,
  namedLogicalTableRefs,
  primaryLogicalTableRef,
  primaryTableColumns,
} from "../lib/manifestLogicalTables.ts";
import { useConfirm } from "../hooks/useConfirm.tsx";
import {
  initialAdminSubmitStatus,
  runAdminSubmit,
  type AdminSubmitStatusState,
} from "../lib/adminSubmitUx.ts";
import {
  buildStep1DraftInput,
  getStoredScreenLabel,
  SCREEN_OPERATION_OPTIONS,
  screenOperationLabel,
  type ScreenOperationKind,
  setStoredScreenLabel,
} from "../runtime/screenAuthoringIntent.ts";
import {
  isValidTopologySystemName,
  topologySystemNameValidationError,
  topologySystemNameToPhysicalTable,
  topologySystemNameToRoute,
  topologySystemNameToUiBuilderKey,
} from "../lib/topologySystemName.ts";
import {
  clearManifestScreenDesignLocal,
  emptyManifestScreenDesign,
  loadManifestScreenDesignLocal,
  MANIFEST_SCREEN_DESIGN_LOCAL_CACHE_NOTE,
  type LogicalTableDraft,
  type ContentDataRowDraft,
  flattenInitialDataRowsForSample,
  patchAggregationBlocks,
  type ManifestScreenDesignDraft,
  type RelationIntentDraft,
  saveManifestScreenDesignLocal,
  screenDesignFromBackendShape,
} from "../lib/manifestScreenDesign.ts";
import { extractScreenDataShapeFromTopology } from "../lib/manifestTopologyExtensions.ts";
import {
  applyHavingConditions,
  applySearchConditions,
} from "../lib/searchConditionEval.ts";
import { evaluateMeasureOnRows } from "../lib/aggregationMeasureEval.ts";
import { importRecordToRowValues } from "../lib/contentDataConformance.ts";
import { AdminImportPanel } from "./AdminImport.tsx";
import {
  listImportSnapshotRecords,
  type AdminImportApplyResult,
  type AdminImportPreviewResult,
} from "../api/adminApi.ts";
import {
  COLUMN_TYPE_NORMAL_VIEW_OPTIONS,
  UX_COLUMN_TYPE_ADVANCED_LABEL,
  UX_COLUMN_TYPE_LABELS,
  isEnumBackedColumnDataType,
  UX_FIELD_AGGREGATION_KEY,
  UX_FIELD_AGGREGATION_MEASURES,
  UX_FIELD_ENUM_GROUP,
  UX_FIELD_OPERATION_ENTITY,
  UX_FIELD_ENUM_GROUP_NONE,
  UX_FIELD_INITIAL_DATA,
  UX_FIELD_NULLABLE,
  UX_FIELD_RELATION_INTENT,
  UX_FIELD_SAMPLE_VIEWING,
  UX_HUB_MANIFESTS_PAGE,
  UX_STATUS_LABELS,
  UX_UI_BUILDER,
} from "../content/adminUxTerms.ts";

type PanelError = { code?: string; message: string };
type DraftSource = "none" | "local" | "backend" | "merged";


function SamplePreviewPanel({
  columns,
  aggregationBlocks,
  qualifiedColumns,
  operationKinds,
  operationEntityBindings,
  columnOrder,
  initialDataRows,
}: {
  columns: { name: string; dataType: string }[];
  aggregationBlocks: AggregationBlock[];
  qualifiedColumns: QualifiedColumnRef[];
  operationKinds: ScreenOperationKind[];
  operationEntityBindings: ManifestScreenDesignDraft["operationEntityBindings"];
  columnOrder: string[];
  initialDataRows: Record<string, string>[];
}): JSX.Element {
  const previewKinds = operationKinds.length > 0 ? operationKinds : (["list"] as ScreenOperationKind[]);
  const [previewOperationKind, setPreviewOperationKind] = useState<ScreenOperationKind>(
    previewKinds[0] ?? "list",
  );
  const effectivePreviewKind = previewKinds.includes(previewOperationKind)
    ? previewOperationKind
    : (previewKinds[0] ?? "list");
  const activeCols = projectionColumnsForOperationKind(
    effectivePreviewKind,
    operationEntityBindings,
    columnOrder,
  );
  const columnTypeByName = Object.fromEntries(columns.map((c) => [c.name, c.dataType]));
  const hasRows = initialDataRows.length > 0;
  const sourceLabels = aggregationSourceRefs(qualifiedColumns);
  const activeBlocks = aggregationBlocks.filter((b) =>
    b.measures.some((m) => m.column && m.function) || b.aggregationKey.trim()
    || (b.searchConditions ?? []).length > 0 || (b.havingConditions ?? []).length > 0
  );
  const primaryBlock = activeBlocks[0];
  const tableAggregationKey = primaryBlock?.aggregationKey.trim() ?? "";
  const primaryFilteredRows = primaryBlock
    ? applySearchConditions(
      initialDataRows,
      primaryBlock.searchConditions ?? [],
      columnTypeByName,
    )
    : initialDataRows;

  let groups = new Map<string, Record<string, string>[]>();
  if (tableAggregationKey && hasRows) {
    for (const row of primaryFilteredRows) {
      const key = row[tableAggregationKey]?.trim() || "(空)";
      const list = groups.get(key) ?? [];
      list.push(row);
      groups.set(key, list);
    }
    groups = applyHavingConditions(groups, primaryBlock?.havingConditions ?? []);
  }

  const renderTable = (rows: Record<string, string>[]) => (
    <table class="min-w-full text-left text-xs">
      <thead>
        <tr>
          {activeCols.map((c) => (
            <th key={c} class="border-b px-2 py-1 font-semibold text-slate-600">
              {c}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {rows.map((row, i) => (
          <tr key={i} class="border-b last:border-0">
            {activeCols.map((c) => (
              <td key={c} class="px-2 py-1 font-mono text-slate-700">
                {row[c] ?? ""}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  );

  return (
    <div class="rounded border border-slate-200 bg-slate-50 p-3 text-xs">
      <div class="mb-2 flex flex-wrap items-center gap-2">
        <p class="font-semibold text-slate-700">{UX_FIELD_SAMPLE_VIEWING}</p>
        <label class="flex items-center gap-1 text-slate-600">
          <span>操作種別</span>
          <select
            class="rounded border px-2 py-0.5 text-xs"
            value={effectivePreviewKind}
            onChange={(e) =>
              setPreviewOperationKind((e.target as HTMLSelectElement).value as ScreenOperationKind)}
          >
            {previewKinds.map((kind) => (
              <option key={kind} value={kind}>{screenOperationLabel(kind)}</option>
            ))}
          </select>
        </label>
      </div>
      {activeBlocks.length > 0 && hasRows && (
        <div class="mb-2 space-y-2">
          {activeBlocks.map((block, bi) => {
            const sourceLabel = (
              sourceLabels.find((s) => s.ref === block.sourceRef)?.label
              ?? block.sourceRef
            ) || `ブロック ${bi + 1}`;
            const blockKey = block.aggregationKey.trim();
            const blockFilteredRows = applySearchConditions(
              initialDataRows,
              block.searchConditions ?? [],
              columnTypeByName,
            );
            let blockGroups = new Map<string, Record<string, string>[]>();
            if (blockKey) {
              for (const row of blockFilteredRows) {
                const key = row[blockKey]?.trim() || "(空)";
                const list = blockGroups.get(key) ?? [];
                list.push(row);
                blockGroups.set(key, list);
              }
              blockGroups = applyHavingConditions(blockGroups, block.havingConditions ?? []);
            }
            const measures = block.measures.filter((m) => m.column && m.function);
            if (measures.length === 0 && !blockKey) return null;
            return (
              <div key={bi} class="rounded border border-slate-100 bg-white p-2">
                <p class="mb-1 font-semibold text-slate-600">
                  集計ブロック: <span class="font-mono">{sourceLabel}</span>
                </p>
                {(block.searchConditions ?? []).length > 0 && (
                  <p class="mb-1 text-slate-500">
                    検索条件: {block.searchConditions.length} 件（ブロック内）
                  </p>
                )}
                {blockKey && (
                  <p class="mb-1 text-slate-500">
                    {UX_FIELD_AGGREGATION_KEY}: <span class="font-mono">{blockKey}</span>
                  </p>
                )}
                {measures.length > 0 && (
                  <ul class="list-inside list-disc text-slate-600">
                    {(blockKey
                      ? [...blockGroups.entries()]
                      : [["(全体)", blockFilteredRows] as const]
                    ).map(([groupKey, rows]) =>
                      measures.map((m) => {
                        const val = evaluateMeasureOnRows(rows, m);
                        return (
                          <li key={`${bi}-${groupKey}-${m.column}-${m.function}`}>
                            {blockKey ? `${blockKey}=${groupKey}: ` : ""}
                            {formatMeasureLabel(m)} = {val ?? "—"}
                          </li>
                        );
                      })
                    )}
                  </ul>
                )}
              </div>
            );
          })}
        </div>
      )}
      {activeCols.length > 0
        ? (
          <p class="mb-1 text-slate-500">
            {UX_FIELD_OPERATION_ENTITY}（{screenOperationLabel(effectivePreviewKind)}）:{" "}
            <span class="font-mono">{activeCols.join(", ")}</span>
          </p>
        )
        : (
          <p class="mb-1 italic text-slate-400">
            {UX_FIELD_OPERATION_ENTITY}（{screenOperationLabel(effectivePreviewKind)}）で列を選ぶとサンプル表に反映されます
          </p>
        )}
      {hasRows && tableAggregationKey && groups.size > 0
        ? (
          <div class="mt-2 space-y-3">
            {[...groups.entries()].map(([groupKey, rows]) => (
              <div key={groupKey}>
                <p class="mb-1 font-semibold text-slate-600">
                  {tableAggregationKey} = <span class="font-mono">{groupKey}</span>
                  {" "}（{rows.length} 件）
                </p>
                {activeCols.length > 0 && <div class="overflow-x-auto">{renderTable(rows)}</div>}
              </div>
            ))}
          </div>
        )
        : hasRows && activeCols.length > 0
        ? <div class="mt-2 overflow-x-auto">{renderTable(primaryFilteredRows)}</div>
        : hasRows && tableAggregationKey
        ? (
          <p class="mt-1 italic text-slate-400">
            表示列を選ばないと集計キー単位の表は出ません（全体集計は表示列なしで保存可能）
          </p>
        )
        : hasRows && activeCols.length > 0
        ? <div class="mt-2 overflow-x-auto">{renderTable(primaryFilteredRows)}</div>
        : hasRows
        ? null
        : (
          <p class="mt-1 italic text-slate-400">
            初期データ行がありません（step 3 で追加してください）
          </p>
        )}
    </div>
  );
}

export type ContentsScreenDesignPanelProps = {
  sharedManifestId: string;
  onSharedManifestIdChange: (id: string) => void;
  manifests: AdminManifestListItem[];
  onManifestsChange: (m: AdminManifestListItem[]) => void;
  manifestsVersion: number;
  onManifestsReload: () => void;
};

export default function ContentsScreenDesignPanel({
  sharedManifestId,
  onSharedManifestIdChange,
  manifests,
  onManifestsChange,
  manifestsVersion,
  onManifestsReload,
}: ContentsScreenDesignPanelProps): JSX.Element {
  const selectedId = sharedManifestId;
  const setSelectedId = onSharedManifestIdChange;
  const [activeStep, setActiveStep] = useState<ContentsPipelineStep>(1);
  const [backendDetail, setBackendDetail] = useState<
    AdminManifestDetail | null
  >(null);
  const [design, setDesign] = useState<ManifestScreenDesignDraft>(
    emptyManifestScreenDesign(),
  );
  const [draftSource, setDraftSource] = useState<DraftSource>("none");
  const [errors, setErrors] = useState<PanelError[]>([]);
  const [submitStatus, setSubmitStatus] = useState<AdminSubmitStatusState>(
    initialAdminSubmitStatus(),
  );
  const [loading, setLoading] = useState(false);
  const [completedThroughStep, setCompletedThroughStep] = useState<
    ContentsPipelineStep | null
  >(null);
  const [showStep3Completion, setShowStep3Completion] = useState(false);
  const step3CompletionRef = useRef<HTMLDivElement>(null);
  const [showAdvancedSearch, setShowAdvancedSearch] = useState(false);
  const [showAdvancedAggregation, setShowAdvancedAggregation] = useState(false);
  const [lastImportSnapshotId, setLastImportSnapshotId] = useState<string | null>(null);
  const { confirm, ConfirmDialogHost } = useConfirm();
  const [manifestLabels, setManifestLabels] = useState<Record<string, string>>({});
  const [remoteTargets, setRemoteTargets] = useState<RelationshipRemoteTarget[]>([]);
  const [remoteTargetsError, setRemoteTargetsError] = useState<string | null>(null);
  const [enumGroups, setEnumGroups] = useState<
    { groupId: string; groupName: string }[]
  >([]);
  const [enumGroupDetails, setEnumGroupDetails] = useState<
    Record<string, EnumDictionaryGroupDetail>
  >({});
  const [enumDictionaryError, setEnumDictionaryError] = useState<string | null>(
    null,
  );

  const needsEnumDictionaryCatalog = design.logicalTables.some((t) =>
    t.columns.some((c) => isEnumBackedColumnDataType(c.dataType))
  );

  useEffect(() => {
    if (activeStep !== 2 && activeStep !== 3) return;
    if (!needsEnumDictionaryCatalog) {
      setEnumGroups([]);
      setEnumDictionaryError(null);
      return;
    }
    void (async () => {
      try {
        const groups = await listEnumDictionaryGroups();
        if (groups === null) {
          setEnumGroups([]);
          setEnumDictionaryError("候補グループ一覧を読み込めませんでした。");
        } else {
          setEnumGroups(groups);
          setEnumDictionaryError(null);
        }
      } catch (err) {
        setEnumGroups([]);
        setEnumDictionaryError(
          err instanceof Error ? err.message : "候補グループ一覧の読み込みに失敗しました。",
        );
      }
    })();
  }, [activeStep, needsEnumDictionaryCatalog]);

  useEffect(() => {
    if ((activeStep !== 2.5 && activeStep !== 3) || !selectedId) return;
    void (async () => {
      const targets = await listRelationshipRemoteTargets(selectedId);
      if (targets === null) {
        setRemoteTargets([]);
        setRemoteTargetsError("有効マニフェストの参照先一覧を読み込めませんでした。");
      } else if (targets.length === 0) {
        setRemoteTargets([]);
        setRemoteTargetsError(
          "参照先に使える有効マニフェスト（論理テーブル付き）がありません。DB に auth.user 境界マニフェスト（seed 091）が入っているか確認してください。",
        );
      } else {
        setRemoteTargets(targets);
        setRemoteTargetsError(null);
      }
    })();
  }, [activeStep, selectedId]);

  const loadManifestLabels = async (items: AdminManifestListItem[]) => {
    const labels: Record<string, string> = {};
    await Promise.all(
      items.map(async (item) => {
        const stored = getStoredScreenLabel(item.manifestId);
        if (stored) {
          labels[item.manifestId] = stored;
          return;
        }
        const detail = await getAdminManifest(item.manifestId);
        if (!detail) return;
        const shape = extractScreenDataShapeFromTopology(detail.topologyRawJson);
        const label = shape.userFacingTopologyLabel?.trim();
        if (label) labels[item.manifestId] = label;
      }),
    );
    setManifestLabels(labels);
  };

  const loadManifests = async () => {
    const m = await listAdminManifests("draft");
    if (m) {
      onManifestsChange(m);
      await loadManifestLabelsWithSystemName(m);
    }
  };

  const draftOptionLabel = (m: AdminManifestListItem, index: number): string => {
    const label = manifestLabels[m.manifestId] ??
      getStoredScreenLabel(m.manifestId);
    const status = UX_STATUS_LABELS[m.status] ?? m.status;
    if (label) return `${label} [${status}]`;
    return `下書き ${index + 1} [${status}]`;
  };

  const loadManifestLabelsWithSystemName = async (items: AdminManifestListItem[]) => {
    const labels: Record<string, string> = {};
    await Promise.all(
      items.map(async (item) => {
        const stored = getStoredScreenLabel(item.manifestId);
        if (stored) { labels[item.manifestId] = stored; return; }
        const detail = await getAdminManifest(item.manifestId);
        if (!detail) return;
        const shape = extractScreenDataShapeFromTopology(detail.topologyRawJson);
        const display = shape.userFacingTopologyLabel?.trim();
        const sysName = shape.topologySystemName?.trim();
        const visible = display || sysName;
        if (visible) labels[item.manifestId] = visible;
      }),
    );
    setManifestLabels(labels);
  };

  const loadSelectedManifest = async (manifestId: string) => {
    const detail = await getAdminManifest(manifestId);
    setBackendDetail(detail);
    if (!detail) {
      const local = loadManifestScreenDesignLocal(manifestId);
      if (local) {
        setDesign(local);
        setDraftSource("local");
      } else {
        setDesign(emptyManifestScreenDesign());
        setDraftSource("none");
      }
      return;
    }

    const shape = extractScreenDataShapeFromTopology(detail.topologyRawJson);
    const summaryLayer = detail.summary?.dispatcherMapping?.layer ?? "";
    const opFromBackend = shape.screenOperationKind as
      | ScreenOperationKind
      | null;
    const operationKind: ScreenOperationKind = opFromBackend ??
      (summaryLayer.includes("detail") ? "detail" : "list");

    const fromBackend = screenDesignFromBackendShape(shape, operationKind);
    fromBackend.topologySystemName = shape.topologySystemName ?? "";
    fromBackend.screenLabel = shape.userFacingTopologyLabel ??
      getStoredScreenLabel(manifestId) ?? "";
    if (shape.screenOperationKinds.length > 0) {
      fromBackend.operationKinds = shape.screenOperationKinds as ScreenOperationKind[];
      fromBackend.operationKind = fromBackend.operationKinds[0];
    }

    const local = loadManifestScreenDesignLocal(manifestId);
    const mergeDesign = (base: ManifestScreenDesignDraft) =>
      hydrateOperationBindingsFromLegacyDisplayColumns(
        qualifyScreenDesignColumnKeys(base),
      );

    if (local) {
      setDesign(mergeDesign({
        ...fromBackend,
        ...local,
        screenLabel: local.screenLabel || fromBackend.screenLabel,
      }));
      setDraftSource("merged");
    } else {
      setDesign(mergeDesign(fromBackend));
      setDraftSource(
        shape.logicalTables.length > 0 ||
          (shape.screenOperationKinds?.length ?? 0) > 0 ||
          shape.searchKeyColumns.length > 0
          ? "backend"
          : "none",
      );
    }
  };

  useEffect(() => {
    loadManifests();
  }, [manifestsVersion]);

  useEffect(() => {
    if (!selectedId) {
      setDesign(emptyManifestScreenDesign());
      setBackendDetail(null);
      setDraftSource("none");
      setCompletedThroughStep(null);
      setShowStep3Completion(false);
      return;
    }
    loadSelectedManifest(selectedId);
  }, [selectedId]);

  useEffect(() => {
    if (!showStep3Completion) return;
    step3CompletionRef.current?.scrollIntoView({
      behavior: "smooth",
      block: "start",
    });
  }, [showStep3Completion]);

  const patchDesign = (patch: Partial<ManifestScreenDesignDraft>) => {
    setDesign((prev) => {
      const merged = { ...prev, ...patch };
      const next = syncRawFieldsFromStructured(merged, patch);
      if (selectedId) {
        saveManifestScreenDesignLocal(selectedId, next);
        setDraftSource((s) => (s === "backend" ? "merged" : "local"));
      }
      return next;
    });
  };

  useEffect(() => {
    if (remoteTargets.length === 0 || design.relationIntents.length === 0) return;
    const remoteManifests = remoteTargets.map((t) => ({
      manifestId: t.manifestId,
      logicalTables: t.logicalTables.map((lt) => ({
        tableName: lt.tableName,
        columns: lt.columns,
      })),
    }));
    const enriched = enrichRelationIntentsWithRemoteTargets(
      design.relationIntents,
      design.logicalTables,
      remoteManifests,
    );
    const changed = enriched.some((rel, i) =>
      rel.remoteManifestId !== design.relationIntents[i]?.remoteManifestId
    );
    if (changed) {
      patchDesign({ relationIntents: enriched });
    }
  }, [remoteTargets, design.logicalTables, design.relationIntents]);

  const handleStep1Submit = async () => {
    const nameErr = topologySystemNameValidationError(design.topologySystemName);
    if (nameErr) {
      setErrors([{ message: nameErr }]);
      return;
    }
    setErrors([]);
    const created = await runAdminSubmit(
      setSubmitStatus,
      setLoading,
      {
        confirmFn: confirm,
        confirmKind: "create",
        successMessage: "Step 1: 空の下書きを登録しました。",
        errorMessage: "下書きを作成できませんでした。",
        onSuccess: async (result) => {
          const sysName = design.topologySystemName.trim();
          const label = design.screenLabel.trim();
          setSelectedId(result.manifestId);
          if (label) setStoredScreenLabel(result.manifestId, label);
          saveManifestScreenDesignLocal(result.manifestId, design);
          await assignAdminManifestScreenDataShape({
            manifestId: result.manifestId,
            topologySystemName: sysName,
            userFacingTopologyLabel: label || undefined,
          });
          const visibleLabel = label || sysName;
          setManifestLabels((prev) => ({ ...prev, [result.manifestId]: visibleLabel }));
          onManifestsReload();
          await loadManifests();
          await loadSelectedManifest(result.manifestId);
          setActiveStep(2);
          setCompletedThroughStep(1);
          setShowStep3Completion(false);
        },
        run: async () => {
          const draftInput = buildStep1DraftInput(null);
          return await createAdminManifestDraft(draftInput);
        },
      },
    );
    void created;
  };

  const handleStepAssign = async (step: ContentsPipelineStep) => {
    if (!selectedId) {
      setErrors([{ message: "先に Step 1 で下書きを作成するか、下書きページを選択してください。" }]);
      return;
    }
    if (!canSaveDesign) {
      setErrors([{
        code: "MANIFEST_NOT_DRAFT",
        message: "有効状態のページは保存できません。下書きを選択してください。",
      }]);
      return;
    }
    setErrors([]);
    setShowStep3Completion(false);
    if (step === 2 || step === 3) {
      const enumErr = validateEnumGroupResolution(step);
      if (enumErr) {
        setErrors([{ message: enumErr }]);
        return;
      }
    }
    if (step === 3 && step3FieldSourceErrors.length > 0) {
      setErrors(step3FieldSourceErrors.map((message) => ({ message })));
      return;
    }
    const shape = backendDetail
      ? extractScreenDataShapeFromTopology(backendDetail.topologyRawJson)
      : null;
    const payload = buildAssignPayloadForStep(step, selectedId, design, shape, {
      relationshipRemoteTargets: remoteTargets.map((t) => ({
        manifestId: t.manifestId,
        logicalTables: t.logicalTables.map((lt) => ({
          tableName: lt.tableName,
          columns: lt.columns,
        })),
      })),
    });
    const ok = await runAdminSubmit(
      setSubmitStatus,
      setLoading,
      {
        confirmFn: confirm,
        confirmKind: "save",
        successMessage: step === 3
          ? "Step 3 を保存しました。Step 1〜3 が完了しました。"
          : step === 2.5
          ? "Step 2.5 を保存しました。続けて Step 3 を設定してください。"
          : step === 2
          ? "Step 2 を保存しました。続けて Step 2.5（関連）へ進みます。"
          : `Step ${step} を保存しました。`,
        onSuccess: async () => {
          if (design.screenLabel.trim()) {
            const label = design.screenLabel.trim();
            setStoredScreenLabel(selectedId, label);
            setManifestLabels((prev) => ({ ...prev, [selectedId]: label }));
          }
          clearManifestScreenDesignLocal(selectedId);
          onManifestsReload();
          await loadManifests();
          await loadSelectedManifest(selectedId);
          setDraftSource("backend");
          if (step === 2) {
            setCompletedThroughStep(2);
            setActiveStep(2.5);
          } else if (step === 2.5) {
            setCompletedThroughStep(2.5);
            setActiveStep(3);
          } else if (step === 3) {
            setCompletedThroughStep(3);
            setShowStep3Completion(true);
          }
        },
        run: async () => {
          await assignAdminManifestScreenDataShape(payload);
        },
      },
    );
    void ok;
  };

  const selectedManifest = manifests.find((m) => m.manifestId === selectedId);
  const selectedStatus = backendDetail?.status ?? selectedManifest?.status ?? "";
  const canSaveDesign = Boolean(selectedId) &&
    selectedStatus.toLowerCase() === "draft";

  const draftSourceLabel = {
    none: "未読込",
    local: "この端末の未保存変更",
    backend: "保存済み",
    merged: "保存済み + この端末の未反映変更",
  }[draftSource];

  const localQualifiedColumns = qualifiedColumnsFromLogicalTables(design.logicalTables);
  const step3RemoteTargetManifests = remoteTargets.map((t) => ({
    manifestId: t.manifestId,
    logicalTables: t.logicalTables.map((lt) => ({
      tableName: lt.tableName,
      columns: lt.columns,
    })),
  }));
  const step3FieldSource = step3FieldSourceFromDesign(
    design.logicalTables,
    design.relationIntents,
    step3RemoteTargetManifests,
  );
  /** Step 3 keys: local logical tables + Step 2.5 resolved relation targets. */
  const qualifiedColumns = activeStep === 3
    ? step3FieldSource.qualifiedColumns
    : localQualifiedColumns;
  const step3FieldSourceErrors = activeStep === 3
    ? step3FieldSource.unresolvedErrors
    : [];
  const columnKeys = qualifiedColumns.map((q) => q.key);
  const logicalTableRefs = namedLogicalTableRefs(design.logicalTables);
  const toggleOperationKind = (kind: ScreenOperationKind) => {
    const has = design.operationKinds.includes(kind);
    const nextKinds = has
      ? design.operationKinds.filter((k) => k !== kind)
      : [...design.operationKinds, kind];
    const resolvedKinds: ScreenOperationKind[] = nextKinds.length > 0 ? nextKinds : ["list"];
    const nextBindings = has
      ? design.operationEntityBindings.filter((b) => b.operationKind !== kind)
      : [
        ...design.operationEntityBindings,
        { operationKind: kind, entityTargetColumns: [] },
      ];
    patchDesign({
      operationKinds: resolvedKinds,
      operationKind: (resolvedKinds[0] ?? "list") as ScreenOperationKind,
      operationEntityBindings: nextBindings,
      ...displayShapePatchFromOperationBindings(
        {
          operationKinds: resolvedKinds,
          operationEntityBindings: nextBindings,
          displayColumnMode: design.displayColumnMode,
        },
        columnKeys,
      ),
    });
  };

  const toggleEntityBindingColumn = (
    kind: ScreenOperationKind,
    colName: string,
  ) => {
    const existing = design.operationEntityBindings.find((b) =>
      b.operationKind === kind
    );
    const current = existing?.entityTargetColumns ?? [];
    const nextCols = current.includes(colName)
      ? current.filter((c) => c !== colName)
      : [...current, colName];
    const nextBindings = existing
      ? design.operationEntityBindings.map((b) =>
        b.operationKind === kind ? { ...b, entityTargetColumns: nextCols } : b
      )
      : [...design.operationEntityBindings, {
        operationKind: kind,
        entityTargetColumns: nextCols,
      }];
    patchDesign({
      operationEntityBindings: nextBindings,
      ...displayShapePatchFromOperationBindings(
        {
          operationKinds: design.operationKinds,
          operationEntityBindings: nextBindings,
          displayColumnMode: design.displayColumnMode,
        },
        columnKeys,
      ),
    });
  };

  useEffect(() => {
    if (activeStep !== 3) return;
    const groupIds = [
      ...new Set(
        qualifiedColumns
          .map((q) => q.column.enumGroupId?.trim())
          .filter((id): id is string => Boolean(id)),
      ),
    ];
    if (groupIds.length === 0) return;
    void (async () => {
      for (const groupId of groupIds) {
        if (enumGroupDetails[groupId]) continue;
        try {
          const detail = await getEnumDictionaryGroup(groupId);
          if (detail) {
            setEnumGroupDetails((prev) => ({ ...prev, [groupId]: detail }));
          }
        } catch (err) {
          setEnumDictionaryError(
            err instanceof Error ? err.message : "候補グループの読み込みに失敗しました。",
          );
        }
      }
    })();
  }, [activeStep, design.logicalTables]);

  const validateEnumGroupResolution = (step: ContentsPipelineStep): string | null => {
    const cols = step === 3 ? step3FieldSource.qualifiedColumns : localQualifiedColumns;
    for (const q of cols) {
      if (!isEnumBackedColumnDataType(q.column.dataType)) continue;
      const groupId = q.column.enumGroupId?.trim();
      if (!groupId) {
        return `列「${q.key}」は列挙（enum）型です。${UX_FIELD_ENUM_GROUP}を選択してください。`;
      }
      if (step === 2) {
        if (!enumGroups.some((g) => g.groupId === groupId)) {
          return `列「${q.key}」の${UX_FIELD_ENUM_GROUP}が辞書に存在しません。`;
        }
        continue;
      }
      const detail = enumGroupDetails[groupId];
      if (!detail || detail.items.length === 0) {
        return `列「${q.key}」の${UX_FIELD_ENUM_GROUP}を解決できません。保存前に候補を読み込んでください。`;
      }
    }
    return null;
  };

  const patchLogicalTables = (logicalTables: LogicalTableDraft[]) => {
    patchDesign(qualifyScreenDesignColumnKeys({
      ...design,
      logicalTables,
      columns: primaryTableColumns(logicalTables),
    }));
  };

  const patchLogicalTable = (
    tableIndex: number,
    patch: Partial<LogicalTableDraft>,
  ) => {
    const next = design.logicalTables.map((t, i) =>
      i === tableIndex ? { ...t, ...patch } : t
    );
    patchLogicalTables(next);
  };

  const patchLogicalTableColumn = (
    tableIndex: number,
    colIndex: number,
    patch: Partial<ManifestScreenDesignDraft["columns"][number]>,
  ) => {
    const table = design.logicalTables[tableIndex];
    if (!table) return;
    const columns = table.columns.map((c, i) =>
      i === colIndex ? { ...c, ...patch } : c
    );
    patchLogicalTable(tableIndex, { columns });
  };

  const addLogicalTable = () => {
    patchLogicalTables([...design.logicalTables, emptyLogicalTable()]);
  };

  const removeLogicalTable = (tableIndex: number) => {
    const next = design.logicalTables.filter((_, i) => i !== tableIndex);
    patchLogicalTables(next.length > 0 ? next : [emptyLogicalTable()]);
  };

  const removeLogicalTableColumn = (tableIndex: number, colIndex: number) => {
    const table = design.logicalTables[tableIndex];
    if (!table || table.columns.length <= 1) return;
    const removedName = table.columns[colIndex]?.name.trim();
    const columns = table.columns.filter((_, i) => i !== colIndex);
    const nextTables = design.logicalTables.map((t, i) =>
      i === tableIndex ? { ...t, columns } : t
    );
    const patch: Partial<ManifestScreenDesignDraft> = {
      logicalTables: nextTables,
      columns: primaryTableColumns(nextTables),
    };
    if (removedName) {
      const tableRef = table.tableName.trim();
      const removedKey = qualifiedColumnKey(tableRef, removedName);
      const drop = (k: string) =>
        k !== removedKey && k !== removedName &&
        !(tableRef && k === `${tableRef}.${removedName}`);
      patch.searchKeyColumns = design.searchKeyColumns.filter(drop);
      patch.displayColumns = design.displayColumns.filter(drop);
      patch.aggregationColumns = design.aggregationColumns.filter(drop);
      patch.operationEntityBindings = design.operationEntityBindings.map((b) => ({
        ...b,
        entityTargetColumns: b.entityTargetColumns.filter(drop),
      }));
      patch.initialDataRows = design.initialDataRows.map((row) => {
        const next = { ...row.values };
        delete next[removedKey];
        delete next[removedName];
        return { values: next, lineage: { ...row.lineage } };
      });
      const nextBlocks = design.aggregationBlocks.map((b) => ({
        ...b,
        aggregationKey: drop(b.aggregationKey) ? "" : b.aggregationKey,
        measures: b.measures.filter((m) => !drop(m.column)),
        searchConditions: (b.searchConditions ?? []).filter((c) => !drop(c.column)),
        havingConditions: (b.havingConditions ?? []).filter((h) => !drop(h.column)),
      })).filter((b) =>
        b.sourceRef || b.aggregationKey || b.measures.length > 0
        || (b.searchConditions?.length ?? 0) > 0 || (b.havingConditions?.length ?? 0) > 0
      );
      Object.assign(
        patch,
        patchAggregationBlocks(nextBlocks, primaryLogicalTableRef(design.logicalTables)),
      );
    }
    patchDesign(patch);
  };

  const addRelationIntent = () => {
    patchDesign({
      relationIntents: [
        ...design.relationIntents,
        {
          localTableRef: defaultLocalTableRef(
            design.logicalTables,
            primaryLogicalTableRef(design.logicalTables),
          ),
          joinTableRef: "",
          localKey: "",
          remoteKey: "",
        },
      ],
    });
  };

  const relationKeyOptionsFor = (tableRef: string) =>
    relationKeyColumnOptionsForTableRef(design.logicalTables, tableRef);

  const relationKeyOptionsForActiveManifest = (
    manifestId: string,
    tableRef: string,
  ) => {
    const manifest = remoteTargets.find((t) => t.manifestId === manifestId);
    const table = manifest?.logicalTables.find((t) => t.tableName === tableRef);
    if (!table) return [];
    return table.columns
      .filter((c) => c.name.trim())
      .map((c) => ({
        value: normalizeRelationKeyColumn(c.name),
        label: c.name,
      }));
  };

  const remoteTargetLabel = (t: RelationshipRemoteTarget): string => {
    const key = t.manifestKey?.trim();
    return key ? `${key} (${t.manifestId.slice(0, 8)}…)` : t.manifestId;
  };

  const defaultRemoteManifestId = (): string => {
    const authBoundary = remoteTargets.find((t) =>
      t.logicalTables.some((lt) => lt.tableName === "auth.user")
    );
    return authBoundary?.manifestId ?? remoteTargets[0]?.manifestId ?? "";
  };

  const patchRelationIntent = (
    index: number,
    patch: Partial<RelationIntentDraft>,
  ) => {
    const next = design.relationIntents.map((r, i) =>
      i === index ? { ...r, ...patch } : r
    );
    patchDesign({ relationIntents: next });
  };

  const removeRelationIntent = (index: number) => {
    patchDesign({
      relationIntents: design.relationIntents.filter((_, i) => i !== index),
    });
  };

  const columnKeysForImport = qualifiedColumns.map((q) => q.key);

  const mergeImportPreviewToEditor = (preview: AdminImportPreviewResult) => {
    setLastImportSnapshotId(preview.snapshotId);
    mergeSnapshotRowsIntoEditor(
      preview.snapshotId,
      rowsFromSnapshotPreview(preview, "import_preview"),
    );
  };

  const rowsFromSnapshotPreview = (
    preview: AdminImportPreviewResult,
    source: "import_preview" | "import_applied",
  ): ContentDataRowDraft[] =>
    preview.records.map((r) => ({
      values: importRecordToRowValues(
        r.records as Record<string, unknown>,
        columnKeysForImport,
      ),
      lineage: {
        source,
        snapshotId: preview.snapshotId,
        rowIndex: r.rowIndex,
      },
    }));

  const mergeSnapshotRowsIntoEditor = (
    snapshotId: string,
    newRows: ContentDataRowDraft[],
  ) => {
    const kept = design.initialDataRows.filter(
      (r) => r.lineage.snapshotId !== snapshotId,
    );
    patchDesign({ initialDataRows: [...kept, ...newRows] });
  };

  const reloadImportSnapshotToEditor = async (snapshotId: string) => {
    const preview = await listImportSnapshotRecords(snapshotId);
    setLastImportSnapshotId(snapshotId);
    mergeSnapshotRowsIntoEditor(
      snapshotId,
      rowsFromSnapshotPreview(preview, "import_applied"),
    );
  };

  const handleImportApplied = (result: AdminImportApplyResult) => {
    setLastImportSnapshotId(result.snapshotId);
    void reloadImportSnapshotToEditor(result.snapshotId);
  };

  return (
    <section class="mb-8 rounded border p-4">
      <h2 class="section-title">ページ内容の設定</h2>
      <p class="mb-3 text-xs text-muted-xs">
        ページに表示する項目、操作、検索、集計、データ入力を設定します。
        作成済みページ同士のつながりや表示順は
        <a href="/admin/manifests" class="link font-semibold">
          {UX_HUB_MANIFESTS_PAGE}
        </a>
        で設定してください。
      </p>

      <ValidationErrorPanel
        errors={errors.map((e) => ({ message: e.message, code: e.code }))}
        title="入力・保存エラー"
      />
      <AdminSubmitStatus status={submitStatus} />
      {showStep3Completion && submitStatus.outcome === "success" && (
        <div ref={step3CompletionRef}>
          <ContentsStepCompletionBanner
            title="Step 3 まで完了しました"
            body={`「${design.screenLabel.trim() || "このページ"}」の操作・検索・集計の設定を保存しました。次は ${UX_UI_BUILDER} で部品とレイアウトを作成します。`}
            primaryHref="/admin/ui-builder"
            primaryLabel={`${UX_UI_BUILDER}（Step 4）へ進む`}
            onDismiss={() => setShowStep3Completion(false)}
          />
        </div>
      )}

      <ContentsPipelineStepper
        activeStep={activeStep}
        completedThroughStep={completedThroughStep}
        onStepChange={(s) => {
          setShowStep3Completion(false);
          setActiveStep(s);
        }}
      />

      <p class="mb-2 text-xs text-muted-xs">
        データ出所: <strong>{draftSourceLabel}</strong>
        {draftSource === "local" || draftSource === "merged"
          ? ` — ${MANIFEST_SCREEN_DESIGN_LOCAL_CACHE_NOTE}`
          : ""}
      </p>

      {backendDetail?.summary?.dispatcherMapping && (
        <details class="mb-4 rounded border border-slate-200 bg-slate-50 p-3 text-xs">
          <summary class="cursor-pointer font-semibold">
            技術情報 — 操作の送信先
          </summary>
          <p class="mt-2 font-mono text-[10px] text-muted-xs">
            dispatcher: {backendDetail.summary.dispatcherMapping.role}/
            {backendDetail.summary.dispatcherMapping.target}/
            {backendDetail.summary.dispatcherMapping.layer}/
            {backendDetail.summary.dispatcherMapping.action}
          </p>
        </details>
      )}

      <div class="mb-3 flex flex-wrap gap-2">
        {activeStep === 1 && (
          <button
            type="button"
            class="btn-primary"
            disabled={loading}
            onClick={handleStep1Submit}
          >
            Step 1 を登録
          </button>
        )}
        {(activeStep === 2 || activeStep === 2.5 || activeStep === 3) && (
          <button
            type="button"
            class="btn-primary"
            disabled={loading || !canSaveDesign}
            onClick={() => handleStepAssign(activeStep)}
          >
            Step {activeStep} を保存
          </button>
        )}
      </div>
      {selectedId && !canSaveDesign && (
        <p class="mb-2 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          選択中のページは<strong>有効</strong>です。下書きのみ編集できます。
        </p>
      )}

      {activeStep === 1 && (
        <div class="mb-3 space-y-3">
          <label class="block text-xs">
            <span class="font-semibold">トポロジーID <span class="text-red-500">*</span></span>
            <span class="ml-1 text-slate-500">（英小文字・数字・ハイフンのみ、例: customer-management）</span>
            <input
              class="mt-1 w-full rounded border px-2 py-1 font-mono"
              placeholder="例: customer-management"
              value={design.topologySystemName}
              onInput={(e) => {
                const val = (e.target as HTMLInputElement).value;
                patchDesign({ topologySystemName: val });
              }}
            />
            {design.topologySystemName.trim() && !isValidTopologySystemName(design.topologySystemName) && (
              <span class="mt-0.5 block text-xs text-red-600">
                {topologySystemNameValidationError(design.topologySystemName)}
              </span>
            )}
          </label>
          {design.topologySystemName.trim() && isValidTopologySystemName(design.topologySystemName) && (
            <div class="rounded border border-slate-200 bg-slate-50 p-2 text-xs text-slate-600">
              <p class="font-semibold mb-1">生成される識別子（自動）</p>
              <ul class="space-y-0.5 font-mono text-[11px]">
                <li>Route: {topologySystemNameToRoute(design.topologySystemName)}</li>
                <li>Primary Table: {topologySystemNameToPhysicalTable(design.topologySystemName)}</li>
                <li>UI Builder Key: {topologySystemNameToUiBuilderKey(design.topologySystemName)}</li>
              </ul>
            </div>
          )}
          <label class="block text-xs">
            表示名（任意）
            <span class="ml-1 text-slate-500">日本語OK・後から変更可</span>
            <input
              class="mt-1 w-full rounded border px-2 py-1"
              placeholder="例: 顧客管理"
              value={design.screenLabel}
              onInput={(e) =>
                patchDesign({
                  screenLabel: (e.target as HTMLInputElement).value,
                })}
            />
          </label>
        </div>
      )}
      {activeStep === 3 && (
        <label class="mb-3 block text-xs">
          ページ名
          <input
            class="mt-1 w-full rounded border px-2 py-1"
            value={design.screenLabel}
            onInput={(e) =>
              patchDesign({
                screenLabel: (e.target as HTMLInputElement).value,
              })}
          />
        </label>
      )}

      {activeStep !== 1 && (
        <label class="mb-3 block text-xs">
          対象の下書きページ（draft のみ）
          <select
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            value={selectedId}
            onChange={(e) => setSelectedId((e.target as HTMLSelectElement).value)}
          >
            <option value="">— 選択 —</option>
            {manifests.map((m, index) => (
              <option key={m.manifestId} value={m.manifestId}>
                {draftOptionLabel(m, index)}
              </option>
            ))}
          </select>
        </label>
      )}

      {activeStep === 3 && (
        <div class="mb-4 text-xs">
          <p class="font-medium mb-1">操作種別（複数選択可）</p>
          <div class="flex flex-wrap gap-2">
            {SCREEN_OPERATION_OPTIONS.map((o) => (
              <label key={o.kind} class="flex items-center gap-1">
                <input
                  type="checkbox"
                  checked={design.operationKinds.includes(o.kind)}
                  onChange={() => toggleOperationKind(o.kind)}
                />
                {o.label}
              </label>
            ))}
          </div>
          <div class="mt-3">
            {design.operationKinds.length === 0
              ? (
                <p class="text-xs italic text-slate-400">
                  操作種別を1つ以上選択すると、操作ごとの対象項目を設定できます。
                </p>
              )
              : (
                <ContentsStep3FieldMatrix
                  columnNames={columnKeys}
                  operationKinds={design.operationKinds}
                  operationEntityBindings={design.operationEntityBindings}
                  onToggleOperation={toggleEntityBindingColumn}
                />
              )}
          </div>
        </div>
      )}

      {activeStep === 2 && (
        <>
      <h3 class="mt-4 text-xs font-semibold">テーブル定義（複数登録可）</h3>
      <p class="mb-2 text-xs text-muted-xs">
        1つの下書きに複数テーブルを登録できます。Step 2.5 の関連設定で参照先テーブル名を使います。
      </p>
      {needsEnumDictionaryCatalog && enumDictionaryError && (
        <p class="mb-2 text-xs text-red-600" role="alert">{enumDictionaryError}</p>
      )}
      {design.logicalTables.map((table, tableIndex) => {
        const isPrimary = tableIndex === 0;
        const derivedPrimaryName = design.topologySystemName.trim() && isValidTopologySystemName(design.topologySystemName)
          ? topologySystemNameToPhysicalTable(design.topologySystemName)
          : "";
        return (
        <div
          key={tableIndex}
          class="mb-4 rounded border border-slate-200 p-3"
        >
          {isPrimary ? (
            <div class="mb-2 text-xs">
              <p class="font-semibold">テーブル名（プライマリ・自動生成）</p>
              <p class="mt-1 rounded border border-slate-100 bg-slate-50 px-2 py-1 font-mono text-slate-700">
                {derivedPrimaryName || <span class="italic text-slate-400">Step 1 でトポロジーIDを入力してください</span>}
              </p>
            </div>
          ) : (
            <label class="mb-2 block text-xs font-semibold">
              テーブルID <span class="text-red-500">*</span>
              <span class="ml-1 font-normal text-slate-500">（英小文字・数字・ハイフン、例: customer-contacts）</span>
              <input
                class="mt-1 w-full rounded border px-2 py-1 text-xs font-mono"
                placeholder="例: customer-contacts"
                value={table.tableName}
                onInput={(e) =>
                  patchLogicalTable(tableIndex, {
                    tableName: (e.target as HTMLInputElement).value,
                  })}
              />
              {table.tableName.trim() && isValidTopologySystemName(table.tableName) && (
                <span class="mt-0.5 block font-mono font-normal text-slate-500">
                  Physical Table: {topologySystemNameToPhysicalTable(table.tableName)}
                </span>
              )}
            </label>
          )}
          {table.columns.map((col, colIndex) => {
            const showEnumGroup = isEnumBackedColumnDataType(col.dataType);
            return (
            <div
              key={colIndex}
              class={`mt-2 grid gap-2 sm:items-start ${
                showEnumGroup
                  ? "sm:grid-cols-[1fr_1fr_1fr_auto_auto]"
                  : "sm:grid-cols-[1fr_1fr_auto_auto]"
              }`}
            >
              <input
                class="rounded border px-2 py-1 text-xs font-mono"
                placeholder="項目名"
                value={col.name}
                onInput={(e) =>
                  patchLogicalTableColumn(tableIndex, colIndex, {
                    name: (e.target as HTMLInputElement).value,
                  })}
              />
              <div>
                <select
                  class="w-full rounded border px-2 py-1 text-xs font-mono"
                  value={COLUMN_TYPE_NORMAL_VIEW_OPTIONS.includes(col.dataType)
                    ? col.dataType
                    : "__advanced__"}
                  onChange={(e) => {
                    const val = (e.target as HTMLSelectElement).value;
                    if (val === "__advanced__") {
                      patchLogicalTableColumn(tableIndex, colIndex, {
                        dataType: "",
                        enumGroupId: undefined,
                      });
                    } else {
                      patchLogicalTableColumn(tableIndex, colIndex, {
                        dataType: val,
                        enumGroupId: isEnumBackedColumnDataType(val)
                          ? col.enumGroupId
                          : undefined,
                      });
                    }
                  }}
                >
                  {COLUMN_TYPE_NORMAL_VIEW_OPTIONS.map((t) => (
                    <option key={t} value={t}>
                      {UX_COLUMN_TYPE_LABELS[t] ?? t} ({t})
                    </option>
                  ))}
                  <option value="__advanced__">
                    {UX_COLUMN_TYPE_ADVANCED_LABEL}
                  </option>
                </select>
                {!COLUMN_TYPE_NORMAL_VIEW_OPTIONS.includes(col.dataType) && (
                  <input
                    class="mt-1 w-full rounded border px-2 py-1 text-xs font-mono"
                    placeholder="詳細な型名"
                    value={col.dataType}
                    onInput={(e) => {
                      const dataType = (e.target as HTMLInputElement).value;
                      patchLogicalTableColumn(tableIndex, colIndex, {
                        dataType,
                        enumGroupId: isEnumBackedColumnDataType(dataType)
                          ? col.enumGroupId
                          : undefined,
                      });
                    }}
                  />
                )}
              </div>
              {showEnumGroup && (
                <div>
                  <label class="mb-0.5 block text-[10px] text-slate-500">
                    {UX_FIELD_ENUM_GROUP}
                  </label>
                  <select
                    class="w-full rounded border px-2 py-1 text-xs"
                    value={col.enumGroupId ?? ""}
                    onChange={(e) =>
                      patchLogicalTableColumn(tableIndex, colIndex, {
                        enumGroupId: (e.target as HTMLSelectElement).value || undefined,
                      })}
                  >
                    <option value="">{UX_FIELD_ENUM_GROUP_NONE}</option>
                    {enumGroups.map((g) => (
                      <option key={g.groupId} value={g.groupId}>
                        {g.groupName}
                      </option>
                    ))}
                  </select>
                </div>
              )}
              <label class="flex items-center gap-2 text-xs sm:pt-1">
                <input
                  type="checkbox"
                  checked={col.nullable}
                  onChange={(e) =>
                    patchLogicalTableColumn(tableIndex, colIndex, {
                      nullable: (e.target as HTMLInputElement).checked,
                    })}
                />
                {UX_FIELD_NULLABLE}
              </label>
              {table.columns.length > 1 && (
                <button
                  type="button"
                  class="text-xs text-red-500 hover:text-red-700 sm:pt-1"
                  onClick={() => removeLogicalTableColumn(tableIndex, colIndex)}
                  aria-label={`項目 ${col.name || colIndex + 1} を削除`}
                >
                  削除
                </button>
              )}
            </div>
          );
          })}
          <div class="mt-2 flex flex-wrap gap-2">
            <button
              type="button"
              class="btn-secondary text-xs"
              onClick={() => {
                const columns = [
                  ...table.columns,
                  { name: "", dataType: "text", nullable: true },
                ];
                patchLogicalTable(tableIndex, { columns });
              }}
            >
              項目を追加
            </button>
            {design.logicalTables.length > 1 && (
              <button
                type="button"
                class="text-xs text-red-500 hover:text-red-700"
                onClick={() => removeLogicalTable(tableIndex)}
              >
                このテーブルを削除
              </button>
            )}
          </div>
        </div>
        );
      })}
      <button
        type="button"
        class="btn-secondary text-xs"
        onClick={addLogicalTable}
      >
        テーブルを追加
      </button>
        </>
      )}

      {activeStep === 3 && step3FieldSourceErrors.length > 0 && (
        <ValidationErrorPanel
          errors={step3FieldSourceErrors.map((message) => ({ message }))}
        />
      )}

      {activeStep === 3 && qualifiedColumns.length > 0 && (
        <p class="mt-4 text-xs text-muted-xs">
          項目候補は Step 2 の論理テーブルと Step 2.5 で接続した参照先テーブルから構成されます（
          {columnKeys.join(", ")}）。型の変更は Step 2、関連の変更は Step 2.5 で行ってください。
        </p>
      )}

      {activeStep === 2.5 && (
        <>
      <h3 class="mt-4 text-xs font-semibold">{UX_FIELD_RELATION_INTENT}</h3>
      <p class="mb-2 text-xs text-muted-xs">
        参照元は編集中の草稿テーブルのみ。参照先は草稿内テーブルまたは有効（active）マニフェストのテーブルを選べます。
      </p>
      {remoteTargetsError && (
        <p class="mb-2 text-xs text-amber-700">{remoteTargetsError}</p>
      )}
      {design.relationIntents.map((rel, ri) => {
        const localTable = rel.localTableRef ||
          defaultLocalTableRef(
            design.logicalTables,
            primaryLogicalTableRef(design.logicalTables),
          );
        const localKeyOptions = relationKeyOptionsFor(localTable);
        const remoteIsActive = Boolean(rel.remoteManifestId?.trim());
        const remoteKeyOptions = remoteIsActive
          ? relationKeyOptionsForActiveManifest(rel.remoteManifestId!, rel.joinTableRef)
          : relationKeyOptionsFor(rel.joinTableRef);
        const activeManifest = remoteTargets.find((t) =>
          t.manifestId === rel.remoteManifestId
        );
        const activeTableOptions = activeManifest?.logicalTables ?? [];
        return (
        <div
          key={ri}
          class="mb-2 rounded border border-slate-200 p-3 text-xs"
        >
          <div class="grid gap-2 sm:grid-cols-[1fr_auto_1fr] sm:items-end">
            <div class="space-y-2">
              <p class="font-semibold text-slate-600">参照元（このページ側）</p>
              <label class="block">
                テーブル
                <select
                  class="mt-1 w-full rounded border px-2 py-1 font-mono"
                  value={localTable}
                  onChange={(e) => {
                    const table = (e.target as HTMLSelectElement).value;
                    patchRelationIntent(ri, { localTableRef: table, localKey: "" });
                  }}
                >
                  <option value="">— 選択 —</option>
                  {logicalTableRefs.map((name) => (
                    <option key={name} value={name}>{name}</option>
                  ))}
                </select>
              </label>
              <label class="block">
                項目
                <select
                  class="mt-1 w-full rounded border px-2 py-1 font-mono"
                  value={normalizeRelationKeyColumn(rel.localKey)}
                  onChange={(e) =>
                    patchRelationIntent(ri, {
                      localKey: (e.target as HTMLSelectElement).value,
                    })}
                >
                  <option value="">— 選択 —</option>
                  {localKeyOptions.map((opt) => (
                    <option key={opt.value} value={opt.value}>{opt.label}</option>
                  ))}
                </select>
              </label>
            </div>
            <p class="hidden py-2 text-center font-bold text-slate-400 sm:block">→</p>
            <div class="space-y-2">
              <p class="font-semibold text-slate-600">参照先</p>
              <label class="block">
                参照先の種類
                <select
                  class="mt-1 w-full rounded border px-2 py-1"
                  value={remoteIsActive ? "active" : "draft"}
                  onChange={(e) => {
                    const mode = (e.target as HTMLSelectElement).value;
                    if (mode === "active") {
                      const manifestId = defaultRemoteManifestId();
                      const tables = remoteTargets.find((t) =>
                        t.manifestId === manifestId
                      )?.logicalTables ?? [];
                      const authUser = tables.find((t) => t.tableName === "auth.user");
                      patchRelationIntent(ri, {
                        remoteManifestId: manifestId,
                        joinTableRef: authUser ? "auth.user" : "",
                        remoteKey: "",
                      });
                    } else {
                      patchRelationIntent(ri, {
                        remoteManifestId: undefined,
                        joinTableRef: "",
                        remoteKey: "",
                      });
                    }
                  }}
                >
                  <option value="draft">この草稿のテーブル</option>
                  <option value="active">有効マニフェストのテーブル</option>
                </select>
              </label>
              {remoteIsActive && remoteTargets.length === 0 && (
                <p class="text-xs text-amber-700">
                  有効なページを選べません。一覧が空のときは初期データまたは
                  <code class="rounded bg-gray-100 px-1">db/patches/add_auth_relationship_remote_boundary_manifest.sql</code>
                  を適用してください。
                </p>
              )}
              {remoteIsActive && remoteTargets.length > 0 && (
                <label class="block">
                  有効なページ
                  <select
                    class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
                    value={rel.remoteManifestId ?? ""}
                    onChange={(e) => {
                      patchRelationIntent(ri, {
                        remoteManifestId: (e.target as HTMLSelectElement).value,
                        joinTableRef: "",
                        remoteKey: "",
                      });
                    }}
                  >
                    <option value="">— マニフェスト —</option>
                    {remoteTargets.map((t) => (
                      <option key={t.manifestId} value={t.manifestId}>
                        {remoteTargetLabel(t)}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              <label class="block">
                テーブル
                <select
                  class="mt-1 w-full rounded border px-2 py-1 font-mono"
                  value={rel.joinTableRef}
                  onChange={(e) => {
                    patchRelationIntent(ri, {
                      joinTableRef: (e.target as HTMLSelectElement).value,
                      remoteKey: "",
                    });
                  }}
                >
                  <option value="">— 選択 —</option>
                  {remoteIsActive
                    ? activeTableOptions.map((t) => (
                      <option key={t.tableName} value={t.tableName}>{t.tableName}</option>
                    ))
                    : logicalTableRefs.filter((n) => n !== localTable).map((name) => (
                      <option key={name} value={name}>{name}</option>
                    ))}
                </select>
              </label>
              <label class="block">
                項目
                <select
                  class="mt-1 w-full rounded border px-2 py-1 font-mono"
                  value={normalizeRelationKeyColumn(rel.remoteKey)}
                  onChange={(e) =>
                    patchRelationIntent(ri, {
                      remoteKey: (e.target as HTMLSelectElement).value,
                    })}
                >
                  <option value="">— 選択 —</option>
                  {remoteKeyOptions.map((opt) => (
                    <option key={opt.value} value={opt.value}>{opt.label}</option>
                  ))}
                </select>
              </label>
            </div>
          </div>
          <p class="mt-2 font-mono text-[10px] text-slate-500">
            {localTable || "?"}.{rel.localKey || "?"} →{" "}
            {remoteIsActive ? `[active ${rel.remoteManifestId?.slice(0, 8) ?? "?"}…]` : ""}
            {rel.joinTableRef || "?"}.{rel.remoteKey || "?"}
          </p>
          <div class="mt-2 flex justify-end">
            <button
              type="button"
              class="text-xs text-red-500 hover:text-red-700"
              onClick={() => removeRelationIntent(ri)}
            >
              削除
            </button>
          </div>
        </div>
        );
      })}
      <button
        type="button"
        class="btn-secondary text-xs"
        onClick={addRelationIntent}
      >
        関連付けを追加
      </button>
        </>
      )}

      {activeStep === 3 && (
        <>
      <h3 class="mt-5 text-xs font-semibold">データ入力 — {UX_FIELD_INITIAL_DATA}</h3>
      <p class="mb-2 text-xs text-muted-xs">
        初期表示のデータ候補。手入力・CSV/JSON 取り込み（プレビュー → 適用）のいずれも、下の同一グリッドで編集・保存できます。
      </p>
      <AdminImportPanel
        embedded
        defaultManifestId={selectedId}
        lockManifestId={!!selectedId}
        onMergePreviewToEditor={mergeImportPreviewToEditor}
        onApplied={handleImportApplied}
      />
      {lastImportSnapshotId && (
        <div class="mb-2 flex flex-wrap gap-2">
          <button
            type="button"
            class="btn-secondary text-xs"
            onClick={() => void reloadImportSnapshotToEditor(lastImportSnapshotId)}
          >
            スナップショットから再読込 ({lastImportSnapshotId.slice(0, 8)}…)
          </button>
        </div>
      )}
      <ContentsDataEditor
        qualifiedColumns={qualifiedColumns}
        rows={design.initialDataRows}
        enumGroupDetails={enumGroupDetails}
        relationIntents={design.relationIntents}
        onRowsChange={(initialDataRows) => patchDesign({ initialDataRows })}
      />

      <h3 class="mt-5 text-xs font-semibold">集計・サンプル</h3>
      <ContentsAggregationMeasuresEditor
        qualifiedColumns={qualifiedColumns}
        blocks={design.aggregationBlocks ?? []}
        onBlocksChange={(nextBlocks) =>
          patchDesign(patchAggregationBlocks(nextBlocks))}
      />


      <div class="mt-3">
        <SamplePreviewPanel
          columns={qualifiedColumns.map((q) => ({
            name: q.key,
            dataType: q.column.dataType,
          }))}
          aggregationBlocks={design.aggregationBlocks}
          qualifiedColumns={qualifiedColumns}
          operationKinds={design.operationKinds}
          operationEntityBindings={design.operationEntityBindings}
          columnOrder={columnKeys}
          initialDataRows={flattenInitialDataRowsForSample(design.initialDataRows)}
        />
      </div>

      <ProRawScreenDataShapeHints
        exampleQualifiedColumn={qualifiedColumns[0]?.key ?? "employees.name"}
        searchKeyColumns={design.searchKeyColumns}
        aggregationBlocks={design.aggregationBlocks ?? []}
        defaultSourceRef={primaryLogicalTableRef(design.logicalTables)}
        onSearchKeyColumnsChange={(searchKeyColumns) => patchDesign({ searchKeyColumns })}
        onAggregationBlocksChange={(blocks) =>
          patchDesign(patchAggregationBlocks(blocks))}
      />

      {showStep3Completion && submitStatus.outcome === "success" && (
        <div class="mt-4 flex flex-wrap items-center gap-3 rounded-lg border border-green-300 bg-green-50 px-4 py-3 text-sm text-green-900">
          <span class="font-medium">保存済み — 次は Step 4 です</span>
          <a
            href="/admin/ui-builder"
            class="btn-primary text-sm no-underline"
          >
            {UX_UI_BUILDER}へ進む
          </a>
        </div>
      )}
        </>
      )}

      {(activeStep === 2 || activeStep === 2.5 || activeStep === 3) && (
        <div class="mt-6 flex flex-wrap gap-2 border-t pt-4">
          <button
            type="button"
            class="btn-primary"
            disabled={loading || !canSaveDesign}
            onClick={() => handleStepAssign(activeStep)}
          >
            Step {activeStep} を保存（フォーム下部）
          </button>
        </div>
      )}
      <ConfirmDialogHost />
    </section>
  );
}
