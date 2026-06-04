import { useEffect, useRef, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  type AdminManifestDetail,
  type AdminManifestListItem,
  assignAdminManifestScreenDataShape,
  createAdminManifestDraft,
  getAdminManifest,
  listAdminManifests,
  listRelationshipRemoteTargets,
  type RelationshipRemoteTarget,
} from "../api/adminApi.ts";
import { AdminSubmitStatus } from "../components/AdminSubmitStatus.tsx";
import ContentsAggregationMeasuresEditor from "../components/ContentsAggregationMeasuresEditor.tsx";
import ContentsStepCompletionBanner from "../components/ContentsStepCompletionBanner.tsx";
import ContentsPipelineStepper, {
  type ContentsPipelineStep,
} from "../components/ContentsPipelineStepper.tsx";
import ContentsStep3FieldMatrix from "../components/ContentsStep3FieldMatrix.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { buildAssignPayloadForStep } from "../lib/contentsAssign.ts";
import { formatMeasureLabel } from "../lib/aggregationMeasures.ts";
import {
  normalizeRelationKeyColumn,
  relationKeyColumnOptionsForTableRef,
  defaultLocalTableRef,
  emptyLogicalTable,
  qualifiedColumnKey,
  qualifiedColumnsFromLogicalTables,
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
  type ScreenOperationKind,
  setStoredScreenLabel,
} from "../runtime/screenAuthoringIntent.ts";
import {
  clearManifestScreenDesignLocal,
  emptyManifestScreenDesign,
  loadManifestScreenDesignLocal,
  MANIFEST_SCREEN_DESIGN_LOCAL_CACHE_NOTE,
  type LogicalTableDraft,
  type ManifestScreenDesignDraft,
  parseSearchTargets,
  type RelationIntentDraft,
  saveManifestScreenDesignLocal,
  screenDesignFromBackendShape,
} from "../lib/manifestScreenDesign.ts";
import { extractScreenDataShapeFromTopology } from "../lib/manifestTopologyExtensions.ts";
import { AdminImportPanel } from "./AdminImport.tsx";
import {
  COLUMN_TYPE_NORMAL_VIEW_OPTIONS,
  UX_COLUMN_TYPE_ADVANCED_LABEL,
  UX_COLUMN_TYPE_LABELS,
  UX_FIELD_AGGREGATION_KEY,
  UX_FIELD_AGGREGATION_MEASURES,
  UX_FIELD_DISPLAY_COLUMNS,
  UX_FIELD_INITIAL_DATA,
  UX_FIELD_NULLABLE,
  UX_FIELD_RELATION_INTENT,
  UX_FIELD_SAMPLE_VIEWING,
  UX_FIELD_SEARCH_KEY,
  UX_HUB_MANIFESTS_PAGE,
  UX_STATUS_LABELS,
  UX_UI_BUILDER,
} from "../content/adminUxTerms.ts";

type PanelError = { code?: string; message: string };
type DraftSource = "none" | "local" | "backend" | "merged";

/** Sample preview: groups rows by aggregation key when set. */
function computeMeasure(
  values: number[],
  fn: string,
): number | null {
  if (values.length === 0) return null;
  switch (fn) {
    case "sum":
      return values.reduce((a, b) => a + b, 0);
    case "avg":
      return values.reduce((a, b) => a + b, 0) / values.length;
    case "max":
      return Math.max(...values);
    case "min":
      return Math.min(...values);
    case "count":
      return values.length;
    default:
      return null;
  }
}

function SamplePreviewPanel({
  columns,
  aggregationKey,
  aggregationMeasures,
  displayColumns,
  initialDataRows,
}: {
  columns: { name: string; dataType: string }[];
  aggregationKey: string;
  aggregationMeasures: { column: string; function: string }[];
  displayColumns: string[];
  initialDataRows: Record<string, string>[];
}): JSX.Element {
  const activeCols = displayColumns;
  const hasRows = initialDataRows.length > 0;

  const groups = new Map<string, Record<string, string>[]>();
  if (aggregationKey && hasRows) {
    for (const row of initialDataRows) {
      const key = row[aggregationKey]?.trim() || "(空)";
      const list = groups.get(key) ?? [];
      list.push(row);
      groups.set(key, list);
    }
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
      <p class="mb-2 font-semibold text-slate-700">{UX_FIELD_SAMPLE_VIEWING}</p>
      {aggregationKey && (
        <p class="mb-1 text-slate-500">
          {UX_FIELD_AGGREGATION_KEY}:{" "}
          <span class="font-mono">{aggregationKey}</span>
        </p>
      )}
      {aggregationMeasures.length > 0 && (
        <p class="mb-1 text-slate-500">
          {UX_FIELD_AGGREGATION_MEASURES}:{" "}
          <span class="font-mono">
            {aggregationMeasures.map(formatMeasureLabel).join(", ")}
          </span>
        </p>
      )}
      {aggregationMeasures.length > 0 && hasRows && (
        <ul class="mb-2 list-inside list-disc text-slate-600">
          {(aggregationKey
            ? [...groups.entries()]
            : [["(全体)", initialDataRows] as const]
          ).map(([groupKey, rows]) =>
            aggregationMeasures.map((m) => {
              const nums = rows
                .map((r) => Number(r[m.column]))
                .filter((n) => !Number.isNaN(n));
              const val = computeMeasure(nums, m.function);
              return (
                <li key={`${groupKey}-${m.column}-${m.function}`}>
                  {aggregationKey ? `${aggregationKey}=${groupKey}: ` : ""}
                  {formatMeasureLabel(m)} = {val ?? "—"}
                </li>
              );
            })
          )}
        </ul>
      )}
      {activeCols.length > 0
        ? (
          <p class="mb-1 text-slate-500">
            {UX_FIELD_DISPLAY_COLUMNS}:{" "}
            <span class="font-mono">{activeCols.join(", ")}</span>
          </p>
        )
        : (
          <p class="mb-1 italic text-slate-400">
            {UX_FIELD_DISPLAY_COLUMNS}: 未選択（全体集計のプレビュー）
          </p>
        )}
      {hasRows && aggregationKey && groups.size > 0
        ? (
          <div class="mt-2 space-y-3">
            {[...groups.entries()].map(([groupKey, rows]) => (
              <div key={groupKey}>
                <p class="mb-1 font-semibold text-slate-600">
                  {aggregationKey} = <span class="font-mono">{groupKey}</span>
                  {" "}（{rows.length} 件）
                </p>
                <div class="overflow-x-auto">{renderTable(rows)}</div>
              </div>
            ))}
          </div>
        )
        : hasRows && activeCols.length > 0
        ? <div class="mt-2 overflow-x-auto">{renderTable(initialDataRows)}</div>
        : hasRows && aggregationKey
        ? (
          <p class="mt-1 italic text-slate-400">
            表示列を選ばないと集計キー単位の表は出ません（全体集計は表示列なしで保存可能）
          </p>
        )
        : hasRows
        ? <div class="mt-2 overflow-x-auto">{renderTable(initialDataRows)}</div>
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
  const [dataInputMode, setDataInputMode] = useState<"manual" | "import">("manual");
  const { confirm, ConfirmDialogHost } = useConfirm();
  const [manifestLabels, setManifestLabels] = useState<Record<string, string>>({});
  const [remoteTargets, setRemoteTargets] = useState<RelationshipRemoteTarget[]>([]);
  const [remoteTargetsError, setRemoteTargetsError] = useState<string | null>(null);

  useEffect(() => {
    if (activeStep !== 2.5 || !selectedId) return;
    void (async () => {
      const targets = await listRelationshipRemoteTargets(selectedId);
      if (targets === null) {
        setRemoteTargets([]);
        setRemoteTargetsError("有効マニフェストの参照先一覧を読み込めませんでした。");
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
      await loadManifestLabels(m);
    }
  };

  const draftOptionLabel = (m: AdminManifestListItem, index: number): string => {
    const label = manifestLabels[m.manifestId] ??
      getStoredScreenLabel(m.manifestId);
    const status = UX_STATUS_LABELS[m.status] ?? m.status;
    if (label) return `${label} [${status}]`;
    return `下書き ${index + 1} [${status}]`;
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
    fromBackend.screenLabel = shape.userFacingTopologyLabel ??
      getStoredScreenLabel(manifestId) ?? "";
    if (shape.screenOperationKinds.length > 0) {
      fromBackend.operationKinds = shape.screenOperationKinds as ScreenOperationKind[];
      fromBackend.operationKind = fromBackend.operationKinds[0];
    }

    const local = loadManifestScreenDesignLocal(manifestId);
    if (local) {
      setDesign(qualifyScreenDesignColumnKeys({
        ...fromBackend,
        ...local,
        screenLabel: local.screenLabel || fromBackend.screenLabel,
      }));
      setDraftSource("merged");
    } else {
      setDesign(fromBackend);
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
      const next = { ...prev, ...patch };
      if (selectedId) {
        saveManifestScreenDesignLocal(selectedId, next);
        setDraftSource((s) => (s === "backend" ? "merged" : "local"));
      }
      return next;
    });
  };

  const handleStep1Submit = async () => {
    if (!design.screenLabel.trim()) {
      setErrors([{ message: "トポロジー表示名（ページ名）を入力してください。" }]);
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
          const label = design.screenLabel.trim();
          setSelectedId(result.manifestId);
          setStoredScreenLabel(result.manifestId, label);
          saveManifestScreenDesignLocal(result.manifestId, design);
          if (label) {
            await assignAdminManifestScreenDataShape({
              manifestId: result.manifestId,
              userFacingTopologyLabel: label,
            });
          }
          setManifestLabels((prev) => ({ ...prev, [result.manifestId]: label }));
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
    const shape = backendDetail
      ? extractScreenDataShapeFromTopology(backendDetail.topologyRawJson)
      : null;
    const payload = buildAssignPayloadForStep(step, selectedId, design, shape);
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

  const toggleOperationKind = (kind: ScreenOperationKind) => {
    const has = design.operationKinds.includes(kind);
    const next = has
      ? design.operationKinds.filter((k) => k !== kind)
      : [...design.operationKinds, kind];
    const nextBindings = has
      ? design.operationEntityBindings.filter((b) => b.operationKind !== kind)
      : [
        ...design.operationEntityBindings,
        { operationKind: kind, entityTargetColumns: [] },
      ];
    patchDesign({
      operationKinds: next.length > 0 ? next : ["list"],
      operationKind: (next[0] ?? "list") as ScreenOperationKind,
      operationEntityBindings: nextBindings,
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
    const next = existing
      ? design.operationEntityBindings.map((b) =>
        b.operationKind === kind ? { ...b, entityTargetColumns: nextCols } : b
      )
      : [...design.operationEntityBindings, {
        operationKind: kind,
        entityTargetColumns: nextCols,
      }];
    patchDesign({ operationEntityBindings: next });
  };

  const draftSourceLabel = {
    none: "未読込",
    local: "この端末の未保存変更",
    backend: "保存済み",
    merged: "保存済み + この端末の未反映変更",
  }[draftSource];

  /** Step 3 keys: `tableRef.columnName` when table is named (e.g. employees.name). */
  const qualifiedColumns = qualifiedColumnsFromLogicalTables(design.logicalTables);
  const columnKeys = qualifiedColumns.map((q) => q.key);
  const logicalTableRefs = namedLogicalTableRefs(design.logicalTables);

  const toggleSearchKey = (colName: string) => {
    const next = design.searchKeyColumns.includes(colName)
      ? design.searchKeyColumns.filter((k) => k !== colName)
      : [...design.searchKeyColumns, colName];
    patchDesign({ searchKeyColumns: next });
  };

  const toggleDisplayColumn = (colName: string) => {
    const next = design.displayColumns.includes(colName)
      ? design.displayColumns.filter((k) => k !== colName)
      : [...design.displayColumns, colName];
    patchDesign({ displayColumns: next });
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
        const next = { ...row };
        delete next[removedKey];
        delete next[removedName];
        return next;
      });
      if (patch.aggregationKey === removedKey || patch.aggregationKey === removedName) {
        patch.aggregationKey = "";
      }
      patch.aggregationMeasures = design.aggregationMeasures.filter((m) =>
        drop(m.column)
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

  const addInitialDataRow = () => {
    const emptyRow: Record<string, string> = {};
    qualifiedColumns.forEach((q) => {
      emptyRow[q.key] = "";
    });
    patchDesign({ initialDataRows: [...design.initialDataRows, emptyRow] });
  };

  const patchInitialDataRow = (
    rowIndex: number,
    colKey: string,
    value: string,
  ) => {
    const next = design.initialDataRows.map((row, i) =>
      i === rowIndex ? { ...row, [colKey]: value } : row
    );
    patchDesign({ initialDataRows: next });
  };

  const removeInitialDataRow = (rowIndex: number) => {
    patchDesign({
      initialDataRows: design.initialDataRows.filter((_, i) => i !== rowIndex),
    });
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

      {(activeStep === 1 || activeStep === 3) && (
        <label class="mb-3 block text-xs">
          {activeStep === 1 ? "トポロジー表示名（必須）" : "ページ名"}
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
                  操作種別を1つ以上選択すると、下の表で検索・操作・表示を設定できます。
                </p>
              )
              : (
                <ContentsStep3FieldMatrix
                  columnNames={columnKeys}
                  operationKinds={design.operationKinds}
                  searchKeyColumns={design.searchKeyColumns}
                  displayColumns={design.displayColumns}
                  operationEntityBindings={design.operationEntityBindings}
                  onToggleSearch={toggleSearchKey}
                  onToggleDisplay={toggleDisplayColumn}
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
      {design.logicalTables.map((table, tableIndex) => (
        <div
          key={tableIndex}
          class="mb-4 rounded border border-slate-200 p-3"
        >
          <label class="mb-2 block text-xs font-semibold">
            テーブル名
            <input
              class="mt-1 w-full rounded border px-2 py-1 text-xs font-mono"
              placeholder="例: employees"
              value={table.tableName}
              onInput={(e) =>
                patchLogicalTable(tableIndex, {
                  tableName: (e.target as HTMLInputElement).value,
                })}
            />
          </label>
          {table.columns.map((col, colIndex) => (
            <div
              key={colIndex}
              class="mt-2 grid gap-2 sm:grid-cols-[1fr_1fr_auto_auto] sm:items-start"
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
                      patchLogicalTableColumn(tableIndex, colIndex, { dataType: "" });
                    } else {
                      patchLogicalTableColumn(tableIndex, colIndex, { dataType: val });
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
                    onInput={(e) =>
                      patchLogicalTableColumn(tableIndex, colIndex, {
                        dataType: (e.target as HTMLInputElement).value,
                      })}
                  />
                )}
              </div>
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
          ))}
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
      ))}
      <button
        type="button"
        class="btn-secondary text-xs"
        onClick={addLogicalTable}
      >
        テーブルを追加
      </button>
        </>
      )}

      {activeStep === 3 && qualifiedColumns.length > 0 && (
        <p class="mt-4 text-xs text-muted-xs">
          表示項目は Step 2 で定義済み（{columnKeys.join(", ")}）。
          変更する場合は Step 2 に戻ってください。
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
                      patchRelationIntent(ri, {
                        remoteManifestId: remoteTargets[0]?.manifestId ?? "",
                        joinTableRef: "",
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
              {remoteIsActive && (
                <label class="block">
                  有効マニフェスト
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
        初期表示のデータ候補（add のみの既定セマンティクス）。手入力または CSV・JSON 取り込み（プレビュー → 明示適用）を選べます。
      </p>
      <div class="mb-3 flex gap-2" role="tablist" aria-label="データ入力方法">
        <button
          type="button"
          role="tab"
          aria-selected={dataInputMode === "manual"}
          class={`rounded px-3 py-1 text-xs ${
            dataInputMode === "manual"
              ? "bg-blue-100 font-semibold text-blue-900"
              : "border border-slate-200 text-slate-600"
          }`}
          onClick={() => setDataInputMode("manual")}
        >
          手入力
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={dataInputMode === "import"}
          class={`rounded px-3 py-1 text-xs ${
            dataInputMode === "import"
              ? "bg-blue-100 font-semibold text-blue-900"
              : "border border-slate-200 text-slate-600"
          }`}
          onClick={() => setDataInputMode("import")}
        >
          CSV・JSON 取り込み
        </button>
      </div>
      {dataInputMode === "manual"
        ? (
          qualifiedColumns.length === 0
            ? (
              <p class="text-xs text-slate-400 italic">
                表示項目を設定してから初期データを追加してください（step 2）。
              </p>
            )
            : (
              <>
                {design.initialDataRows.length > 0 && (
                  <div class="mb-2 overflow-x-auto rounded border border-slate-200">
                    <table class="min-w-full text-left text-xs">
                      <thead>
                        <tr>
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
                        {design.initialDataRows.map((row, ri) => (
                          <tr key={ri} class="border-b last:border-0">
                            {qualifiedColumns.map((q) => (
                              <td key={q.key} class="px-1 py-1">
                                <input
                                  class="w-full rounded border px-1 py-0.5 text-xs font-mono"
                                  value={row[q.key] ?? ""}
                                  onInput={(e) =>
                                    patchInitialDataRow(
                                      ri,
                                      q.key,
                                      (e.target as HTMLInputElement).value,
                                    )}
                                />
                              </td>
                            ))}
                            <td class="px-1 py-1">
                              <button
                                type="button"
                                class="text-xs text-red-500 hover:text-red-700"
                                onClick={() => removeInitialDataRow(ri)}
                                aria-label="削除"
                              >
                                削除
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
                <button
                  type="button"
                  class="btn-secondary text-xs"
                  onClick={addInitialDataRow}
                >
                  行を追加
                </button>
              </>
            )
        )
        : (
          <AdminImportPanel
            embedded
            defaultManifestId={selectedId}
            lockManifestId={!!selectedId}
          />
        )}

      <h3 class="mt-5 text-xs font-semibold">集計・サンプル</h3>
      <p class="mb-2 text-xs text-muted-xs">
        {UX_FIELD_SEARCH_KEY}・{UX_FIELD_DISPLAY_COLUMNS} は上の表で設定済みです。ここで集計式を複数登録できます。
      </p>
      <ContentsAggregationMeasuresEditor
        columnNames={columnKeys}
        aggregationKey={design.aggregationKey}
        measures={design.aggregationMeasures}
        onAggregationKeyChange={(aggregationKey) => patchDesign({ aggregationKey })}
        onMeasuresChange={(aggregationMeasures) => patchDesign({ aggregationMeasures })}
      />
      <details class="mt-2">
        <summary class="cursor-pointer text-xs text-slate-500">
          詳細 / raw 入力（検索・集計）
        </summary>
        <label class="mt-1 block text-xs text-slate-500">
          検索対象（カンマ区切り）
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
            value={design.searchTargets}
            onInput={(e) =>
              patchDesign({
                searchTargets: (e.target as HTMLInputElement).value,
              })}
          />
        </label>
      </details>
      {/* Advanced: raw aggregationSpec in disclosure */}
      <details class="mt-2">
        <summary class="cursor-pointer text-xs text-slate-500">
          詳細 / raw 集計仕様
        </summary>
        <label class="mt-1 block text-xs text-slate-500">
          集計仕様（raw — 上の構造化フィールドと独立）
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
            value={design.aggregationSpec}
            onInput={(e) =>
              patchDesign({
                aggregationSpec: (e.target as HTMLInputElement).value,
              })}
          />
        </label>
      </details>

      <div class="mt-3">
        <SamplePreviewPanel
          columns={qualifiedColumns.map((q) => ({
            name: q.key,
            dataType: q.column.dataType,
          }))}
          aggregationKey={design.aggregationKey}
          aggregationMeasures={design.aggregationMeasures}
          displayColumns={design.displayColumns}
          initialDataRows={design.initialDataRows}
        />
      </div>

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
