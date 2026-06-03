import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  type AdminManifestDetail,
  type AdminManifestListItem,
  assignAdminManifestScreenDataShape,
  createAdminManifestDraft,
  getAdminManifest,
  listAdminManifests,
} from "../api/adminApi.ts";
import { AdminSubmitStatus } from "../components/AdminSubmitStatus.tsx";
import ContentsPipelineStepper, {
  type ContentsPipelineStep,
} from "../components/ContentsPipelineStepper.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { buildAssignPayloadForStep } from "../lib/contentsAssign.ts";
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
  type ManifestScreenDesignDraft,
  parseSearchTargets,
  type RelationIntentDraft,
  saveManifestScreenDesignLocal,
  screenDesignFromBackendShape,
} from "../lib/manifestScreenDesign.ts";
import { extractScreenDataShapeFromTopology } from "../lib/manifestTopologyExtensions.ts";
import {
  COLUMN_TYPE_NORMAL_VIEW_OPTIONS,
  UX_COLUMN_TYPE_ADVANCED_LABEL,
  UX_COLUMN_TYPE_LABELS,
  UX_FIELD_AGGREGATION_KEY,
  UX_FIELD_DISPLAY_COLUMNS,
  UX_FIELD_IMPORT_SCHEMA,
  UX_FIELD_INITIAL_DATA,
  UX_FIELD_NULLABLE,
  UX_FIELD_RELATION_INTENT,
  UX_FIELD_OPERATION_ENTITY,
  UX_FIELD_SAMPLE_VIEWING,
  UX_FIELD_SEARCH_KEY,
  UX_FIELD_TABLE_REF,
  UX_HUB_MANIFESTS_PAGE,
  UX_STATUS_LABELS,
} from "../content/adminUxTerms.ts";

type PanelError = { code?: string; message: string };
type DraftSource = "none" | "local" | "backend" | "merged";

/** Sample preview: groups rows by aggregation key when set. */
function SamplePreviewPanel({
  columns,
  aggregationKey,
  displayColumns,
  initialDataRows,
}: {
  columns: { name: string; dataType: string }[];
  aggregationKey: string;
  displayColumns: string[];
  initialDataRows: Record<string, string>[];
}): JSX.Element {
  const activeCols = displayColumns.length > 0
    ? displayColumns
    : columns.map((c) => c.name).filter(Boolean);
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
      {activeCols.length > 0 && (
        <p class="mb-1 text-slate-500">
          {UX_FIELD_DISPLAY_COLUMNS}:{" "}
          <span class="font-mono">{activeCols.join(", ")}</span>
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
  const [nextLink, setNextLink] = useState<{ href: string; label: string } | null>(
    null,
  );
  const [showAdvancedSearch, setShowAdvancedSearch] = useState(false);
  const [showAdvancedAggregation, setShowAdvancedAggregation] = useState(false);
  const { confirm, ConfirmDialogHost } = useConfirm();

  const loadManifests = async () => {
    const m = await listAdminManifests("draft");
    if (m) onManifestsChange(m);
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
      setDesign({
        ...fromBackend,
        ...local,
        screenLabel: local.screenLabel || fromBackend.screenLabel,
      });
      setDraftSource("merged");
    } else {
      setDesign(fromBackend);
      setDraftSource(
        shape.tableRef || shape.importSchemaName ? "backend" : "none",
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
      return;
    }
    loadSelectedManifest(selectedId);
  }, [selectedId]);

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
          setSelectedId(result.manifestId);
          setStoredScreenLabel(result.manifestId, design.screenLabel.trim());
          saveManifestScreenDesignLocal(result.manifestId, design);
          onManifestsReload();
          await loadManifests();
          await loadSelectedManifest(result.manifestId);
          setActiveStep(2);
          setNextLink(null);
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
        successMessage: `Step ${step} を保存しました。`,
        onSuccess: async () => {
          if (design.screenLabel.trim()) {
            setStoredScreenLabel(selectedId, design.screenLabel.trim());
          }
          clearManifestScreenDesignLocal(selectedId);
          onManifestsReload();
          await loadManifests();
          await loadSelectedManifest(selectedId);
          if (step === 2) setActiveStep(2.5);
          else if (step === 2.5) setActiveStep(3);
          else if (step === 3) {
            setNextLink({ href: "/admin/ui-builder", label: "画面づくり (step 4) へ" });
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
        { operationKind: kind, entityTargetColumn: "" },
      ];
    patchDesign({
      operationKinds: next.length > 0 ? next : ["list"],
      operationKind: (next[0] ?? "list") as ScreenOperationKind,
      operationEntityBindings: nextBindings,
    });
  };

  const patchEntityBinding = (
    kind: ScreenOperationKind,
    entityTargetColumn: string,
  ) => {
    const existing = design.operationEntityBindings.find((b) =>
      b.operationKind === kind
    );
    const next = existing
      ? design.operationEntityBindings.map((b) =>
        b.operationKind === kind ? { ...b, entityTargetColumn } : b
      )
      : [...design.operationEntityBindings, { operationKind: kind, entityTargetColumn }];
    patchDesign({ operationEntityBindings: next });
  };

  const draftSourceLabel = {
    none: "未読込",
    local: "この端末の未保存変更",
    backend: "保存済み",
    merged: "保存済み + この端末の未反映変更",
  }[draftSource];

  /** Columns with non-empty names for structured selection UIs. */
  const namedColumns = design.columns.filter((c) => c.name.trim());

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

  const addRelationIntent = () => {
    patchDesign({
      relationIntents: [
        ...design.relationIntents,
        { joinTableRef: "", localKey: "", remoteKey: "" },
      ],
    });
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
    namedColumns.forEach((c) => {
      emptyRow[c.name] = "";
    });
    patchDesign({ initialDataRows: [...design.initialDataRows, emptyRow] });
  };

  const patchInitialDataRow = (
    rowIndex: number,
    colName: string,
    value: string,
  ) => {
    const next = design.initialDataRows.map((row, i) =>
      i === rowIndex ? { ...row, [colName]: value } : row
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
        ページに表示する項目、検索、集計、取り込みルールを設定します。
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
      {nextLink && submitStatus.outcome === "success" && (
        <p class="mb-3 text-sm">
          <a href={nextLink.href} class="link font-semibold">{nextLink.label}</a>
        </p>
      )}

      <ContentsPipelineStepper
        activeStep={activeStep}
        onStepChange={setActiveStep}
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
                下書き {index + 1} [{UX_STATUS_LABELS[m.status] ?? m.status}]
              </option>
            ))}
          </select>
        </label>
      )}

      {activeStep === 3 && (
        <div class="mb-4 grid gap-2 sm:grid-cols-2">
          <label class="text-xs">
            {UX_FIELD_TABLE_REF}
            <input
              class="mt-1 w-full rounded border px-2 py-1 font-mono"
              value={design.tableRef}
              onInput={(e) =>
                patchDesign({ tableRef: (e.target as HTMLInputElement).value })}
            />
          </label>
          <label class="text-xs">
            {UX_FIELD_IMPORT_SCHEMA}
            <input
              class="mt-1 w-full rounded border px-2 py-1 font-mono"
              value={design.importSchemaName}
              onInput={(e) =>
                patchDesign({
                  importSchemaName: (e.target as HTMLInputElement).value,
                })}
            />
          </label>
          <div class="sm:col-span-2 text-xs">
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
          </div>
          <div class="sm:col-span-2 mt-2 text-xs">
            <p class="font-medium mb-1">{UX_FIELD_OPERATION_ENTITY}</p>
            <p class="text-muted-xs mb-2">
              各操作が実行されるときに使う項目を指定します（フォーム送信は追加のみ）。
            </p>
            {design.operationKinds.length === 0
              ? <p class="italic text-slate-400">操作種別を1つ以上選択してください。</p>
              : (
                <div class="space-y-2">
                  {design.operationKinds.map((kind) => {
                    const binding = design.operationEntityBindings.find((b) =>
                      b.operationKind === kind
                    );
                    return (
                      <label key={kind} class="flex flex-wrap items-center gap-2">
                        <span class="w-20 font-medium">
                          {SCREEN_OPERATION_OPTIONS.find((o) => o.kind === kind)?.label ?? kind}
                        </span>
                        <select
                          class="rounded border px-2 py-1 font-mono"
                          value={binding?.entityTargetColumn ?? ""}
                          onChange={(e) =>
                            patchEntityBinding(
                              kind,
                              (e.target as HTMLSelectElement).value,
                            )}
                        >
                          <option value="">— 項目 —</option>
                          {namedColumns.map((c) => (
                            <option key={c.name} value={c.name}>{c.name}</option>
                          ))}
                        </select>
                      </label>
                    );
                  })}
                </div>
              )}
          </div>
        </div>
      )}

      {(activeStep === 2 || activeStep === 3) && (
        <>
      <h3 class="mt-4 text-xs font-semibold">表示項目の設定</h3>
      {design.columns.map((col, index) => (
        <div key={index} class="mt-2 grid gap-2 sm:grid-cols-3">
          <input
            class="rounded border px-2 py-1 text-xs font-mono"
            placeholder="項目名"
            value={col.name}
            onInput={(e) => {
              const columns = [...design.columns];
              columns[index] = {
                ...columns[index],
                name: (e.target as HTMLInputElement).value,
              };
              patchDesign({ columns });
            }}
          />
          <div>
            <select
              class="w-full rounded border px-2 py-1 text-xs font-mono"
              value={COLUMN_TYPE_NORMAL_VIEW_OPTIONS.includes(col.dataType)
                ? col.dataType
                : "__advanced__"}
              onChange={(e) => {
                const val = (e.target as HTMLSelectElement).value;
                const columns = [...design.columns];
                if (val === "__advanced__") {
                  columns[index] = { ...columns[index], dataType: "" };
                } else {
                  columns[index] = { ...columns[index], dataType: val };
                }
                patchDesign({ columns });
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
                  const columns = [...design.columns];
                  columns[index] = {
                    ...columns[index],
                    dataType: (e.target as HTMLInputElement).value,
                  };
                  patchDesign({ columns });
                }}
              />
            )}
          </div>
          <label class="flex items-center gap-2 text-xs">
            <input
              type="checkbox"
              checked={col.nullable}
              onChange={(e) => {
                const columns = [...design.columns];
                columns[index] = {
                  ...columns[index],
                  nullable: (e.target as HTMLInputElement).checked,
                };
                patchDesign({ columns });
              }}
            />
            {UX_FIELD_NULLABLE}
          </label>
        </div>
      ))}
      <button
        type="button"
        class="btn-secondary mt-2 text-xs"
        onClick={() =>
          patchDesign({
            columns: [...design.columns, {
              name: "",
              dataType: "text",
              nullable: true,
            }],
          })}
      >
        項目を追加
      </button>

        </>
      )}

      {activeStep === 2.5 && (
        <>
      <h3 class="mt-4 text-xs font-semibold">{UX_FIELD_RELATION_INTENT}</h3>
      <p class="mb-2 text-xs text-muted-xs">
        テーブル間の relationship を設定します（独立 submit）。
      </p>
      {design.relationIntents.map((rel, ri) => (
        <div
          key={ri}
          class="mb-2 grid gap-2 rounded border border-slate-200 p-2 sm:grid-cols-3"
        >
          <label class="text-xs">
            参照先
            <input
              class="mt-1 w-full rounded border px-2 py-1 text-xs font-mono"
              placeholder="参照テーブル名"
              value={rel.joinTableRef}
              onInput={(e) =>
                patchRelationIntent(ri, {
                  joinTableRef: (e.target as HTMLInputElement).value,
                })}
            />
          </label>
          <label class="text-xs">
            このページの照合項目
            <input
              class="mt-1 w-full rounded border px-2 py-1 text-xs font-mono"
              placeholder="照合する項目名"
              value={rel.localKey}
              onInput={(e) =>
                patchRelationIntent(ri, {
                  localKey: (e.target as HTMLInputElement).value,
                })}
            />
          </label>
          <label class="text-xs">
            参照先の照合項目
            <input
              class="mt-1 w-full rounded border px-2 py-1 text-xs font-mono"
              placeholder="参照先の項目名"
              value={rel.remoteKey}
              onInput={(e) =>
                patchRelationIntent(ri, {
                  remoteKey: (e.target as HTMLInputElement).value,
                })}
            />
          </label>
          <div class="sm:col-span-3 flex justify-end">
            <button
              type="button"
              class="text-xs text-red-500 hover:text-red-700"
              onClick={() => removeRelationIntent(ri)}
            >
              削除
            </button>
          </div>
        </div>
      ))}
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
      <h3 class="mt-5 text-xs font-semibold">{UX_FIELD_INITIAL_DATA}</h3>
      <p class="mb-2 text-xs text-muted-xs">
        初期表示のデータ候補（add のみの既定セマンティクス）。
      </p>
      {namedColumns.length === 0
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
                      {namedColumns.map((c) => (
                        <th
                          key={c.name}
                          class="border-b px-2 py-1 font-semibold text-slate-600 bg-slate-50"
                        >
                          {c.name}
                        </th>
                      ))}
                      <th class="border-b px-2 py-1 bg-slate-50" />
                    </tr>
                  </thead>
                  <tbody>
                    {design.initialDataRows.map((row, ri) => (
                      <tr key={ri} class="border-b last:border-0">
                        {namedColumns.map((c) => (
                          <td key={c.name} class="px-1 py-1">
                            <input
                              class="w-full rounded border px-1 py-0.5 text-xs font-mono"
                              value={row[c.name] ?? ""}
                              onInput={(e) =>
                                patchInitialDataRow(
                                  ri,
                                  c.name,
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
        )}

      <h3 class="mt-5 text-xs font-semibold">{UX_FIELD_SEARCH_KEY}</h3>
      <p class="mb-2 text-xs text-muted-xs">
        検索に使う項目を選択してください（複数選択可）。
      </p>
      {namedColumns.length === 0
        ? (
          <p class="text-xs text-slate-400 italic">
            表示項目を設定してから検索キーを選択してください。
          </p>
        )
        : (
          <div class="flex flex-wrap gap-2">
            {namedColumns.map((c) => (
              <label key={c.name} class="flex items-center gap-1 text-xs">
                <input
                  type="checkbox"
                  checked={design.searchKeyColumns.includes(c.name)}
                  onChange={() =>
                    toggleSearchKey(c.name)}
                />
                {c.name}
              </label>
            ))}
          </div>
        )}
      {/* Advanced: raw comma-separated input in disclosure */}
      <details class="mt-2">
        <summary class="cursor-pointer text-xs text-slate-500">
          詳細 / raw 入力
        </summary>
        <label class="mt-1 block text-xs text-slate-500">
          検索対象（カンマ区切り、上のチェックと独立）
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

      <h3 class="mt-5 text-xs font-semibold">
        {UX_FIELD_AGGREGATION_KEY} / {UX_FIELD_DISPLAY_COLUMNS}
      </h3>
      <p class="mb-2 text-xs text-muted-xs">
        集計・表示の設定をします。サンプル表示で確認してから保存してください。
      </p>
      <div class="grid gap-2 sm:grid-cols-2">
        <label class="text-xs">
          {UX_FIELD_AGGREGATION_KEY}
          <select
            class="mt-1 w-full rounded border px-2 py-1 text-xs"
            value={design.aggregationKey}
            onChange={(e) =>
              patchDesign({
                aggregationKey: (e.target as HTMLSelectElement).value,
              })}
          >
            <option value="">— なし —</option>
            {namedColumns.map((c) => (
              <option key={c.name} value={c.name}>{c.name}</option>
            ))}
          </select>
        </label>
        <div class="text-xs">
          <p class="font-medium mb-1">{UX_FIELD_DISPLAY_COLUMNS}</p>
          {namedColumns.length === 0
            ? <p class="text-slate-400 italic">表示項目を設定してください。</p>
            : (
              <div class="flex flex-wrap gap-2">
                {namedColumns.map((c) => (
                  <label key={c.name} class="flex items-center gap-1">
                    <input
                      type="checkbox"
                      checked={design.displayColumns.includes(c.name)}
                      onChange={() =>
                        toggleDisplayColumn(c.name)}
                    />
                    {c.name}
                  </label>
                ))}
              </div>
            )}
        </div>
      </div>
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
          columns={namedColumns}
          aggregationKey={design.aggregationKey}
          displayColumns={design.displayColumns}
          initialDataRows={design.initialDataRows}
        />
      </div>
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
