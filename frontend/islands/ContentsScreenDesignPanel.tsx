import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  listAdminManifests,
  getAdminManifest,
  createAdminManifestDraft,
  assignAdminManifestScreenDataShape,
  type AdminManifestListItem,
  type AdminManifestDetail,
} from "../api/adminApi.ts";
import {
  SCREEN_OPERATION_OPTIONS,
  buildDraftInputFromScreenIntent,
  setStoredScreenLabel,
  getStoredScreenLabel,
  type ScreenOperationKind,
} from "../runtime/screenAuthoringIntent.ts";
import {
  emptyManifestScreenDesign,
  loadManifestScreenDesignLocal,
  saveManifestScreenDesignLocal,
  clearManifestScreenDesignLocal,
  screenDesignFromBackendShape,
  parseSearchTargets,
  MANIFEST_SCREEN_DESIGN_LOCAL_CACHE_NOTE,
  type ManifestScreenDesignDraft,
  type RelationIntentDraft,
} from "../lib/manifestScreenDesign.ts";
import { extractScreenDataShapeFromTopology } from "../lib/manifestTopologyExtensions.ts";
import {
  UX_HUB_MANIFESTS_PAGE,
  UX_STATUS_LABELS,
  UX_FIELD_TABLE_REF,
  UX_FIELD_IMPORT_SCHEMA,
  UX_FIELD_NULLABLE,
  COLUMN_TYPE_NORMAL_VIEW_OPTIONS,
  UX_COLUMN_TYPE_ADVANCED_LABEL,
  UX_FIELD_SEARCH_KEY,
  UX_FIELD_AGGREGATION_KEY,
  UX_FIELD_DISPLAY_COLUMNS,
  UX_FIELD_SAMPLE_VIEWING,
  UX_FIELD_INITIAL_DATA,
  UX_FIELD_RELATION_INTENT,
} from "../content/adminUxTerms.ts";

type PanelError = { code?: string; message: string };
type DraftSource = "none" | "local" | "backend" | "merged";

/** Sample preview row: renders initial data rows or column-based preview. */
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
  return (
    <div class="rounded border border-slate-200 bg-slate-50 p-3 text-xs">
      <p class="mb-2 font-semibold text-slate-700">{UX_FIELD_SAMPLE_VIEWING}</p>
      {aggregationKey && (
        <p class="mb-1 text-slate-500">
          {UX_FIELD_AGGREGATION_KEY}: <span class="font-mono">{aggregationKey}</span>
        </p>
      )}
      {activeCols.length > 0 && (
        <p class="mb-1 text-slate-500">
          {UX_FIELD_DISPLAY_COLUMNS}: <span class="font-mono">{activeCols.join(", ")}</span>
        </p>
      )}
      {hasRows ? (
        <div class="mt-2 overflow-x-auto">
          <table class="min-w-full text-left text-xs">
            <thead>
              <tr>
                {activeCols.map((c) => (
                  <th key={c} class="border-b px-2 py-1 font-semibold text-slate-600">{c}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {initialDataRows.map((row, i) => (
                <tr key={i} class="border-b last:border-0">
                  {activeCols.map((c) => (
                    <td key={c} class="px-2 py-1 font-mono text-slate-700">{row[c] ?? ""}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p class="mt-1 italic text-slate-400">
          初期データ行がありません（④ 初期データで追加してください）
        </p>
      )}
    </div>
  );
}

export default function ContentsScreenDesignPanel(): JSX.Element {
  const [manifests, setManifests] = useState<AdminManifestListItem[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [backendDetail, setBackendDetail] = useState<AdminManifestDetail | null>(null);
  const [design, setDesign] = useState<ManifestScreenDesignDraft>(emptyManifestScreenDesign());
  const [draftSource, setDraftSource] = useState<DraftSource>("none");
  const [errors, setErrors] = useState<PanelError[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [showAdvancedSearch, setShowAdvancedSearch] = useState(false);
  const [showAdvancedAggregation, setShowAdvancedAggregation] = useState(false);

  const loadManifests = async () => {
    const m = await listAdminManifests();
    if (m) setManifests(m);
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
    const opFromBackend = shape.screenOperationKind as ScreenOperationKind | null;
    const operationKind: ScreenOperationKind = opFromBackend ??
      (summaryLayer.includes("detail") ? "detail" : "list");

    const fromBackend = screenDesignFromBackendShape(shape, operationKind);
    fromBackend.screenLabel = getStoredScreenLabel(manifestId) ?? "";

    const local = loadManifestScreenDesignLocal(manifestId);
    if (local) {
      setDesign({ ...fromBackend, ...local, screenLabel: local.screenLabel || fromBackend.screenLabel });
      setDraftSource("merged");
    } else {
      setDesign(fromBackend);
      setDraftSource(shape.tableRef || shape.importSchemaName ? "backend" : "none");
    }
  };

  useEffect(() => {
    loadManifests();
  }, []);

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

  const handleCreateDraft = async () => {
    setLoading(true);
    setErrors([]);
    try {
      const draftInput = buildDraftInputFromScreenIntent({
        operationKind: design.operationKind,
        manifestId: null,
      });
      const created = await createAdminManifestDraft({
        ...draftInput,
        screenOperationKind: design.operationKind,
      });
      setSelectedId(created.manifestId);
      if (design.screenLabel.trim()) {
        setStoredScreenLabel(created.manifestId, design.screenLabel.trim());
      }
      saveManifestScreenDesignLocal(created.manifestId, design);
      setStatus(`下書き manifest を作成: ${created.manifestId}`);
      await loadManifests();
      await loadSelectedManifest(created.manifestId);
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handleSaveAuthoring = async () => {
    if (!selectedId) {
      setErrors([{ message: "対象 manifest を選択するか、新規下書きを作成してください。" }]);
      return;
    }
    setLoading(true);
    setErrors([]);
    try {
      if (design.screenLabel.trim()) setStoredScreenLabel(selectedId, design.screenLabel.trim());
      await assignAdminManifestScreenDataShape({
        manifestId: selectedId,
        tableRef: design.tableRef || undefined,
        dbTableName: design.tableRef || undefined,
        importSchemaName: design.importSchemaName || undefined,
        searchTargets: design.searchKeyColumns.length > 0
          ? design.searchKeyColumns
          : parseSearchTargets(design.searchTargets),
        searchKeyColumns: design.searchKeyColumns.length > 0 ? design.searchKeyColumns : undefined,
        aggregationSpec: design.aggregationSpec || undefined,
        aggregationKey: design.aggregationKey || undefined,
        displayColumns: design.displayColumns.length > 0 ? design.displayColumns : undefined,
        columns: design.columns.filter((c) => c.name.trim()),
        screenOperationKind: design.operationKind,
        relationIntents: design.relationIntents.filter((r) => r.joinTableRef.trim()).length > 0
          ? design.relationIntents.filter((r) => r.joinTableRef.trim())
          : undefined,
        initialDataRows: design.initialDataRows.length > 0 ? design.initialDataRows : undefined,
      });
      clearManifestScreenDesignLocal(selectedId);
      setStatus("画面設計を backend 下書きに保存しました（canonical は promote 後の topology 投影）。");
      await loadManifests();
      await loadSelectedManifest(selectedId);
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const draftSourceLabel = {
    none: "未読込",
    local: "ローカル下書きキャッシュ",
    backend: "backend 保存済み",
    merged: "backend + 未保存のローカル差分",
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

  const patchRelationIntent = (index: number, patch: Partial<RelationIntentDraft>) => {
    const next = design.relationIntents.map((r, i) => i === index ? { ...r, ...patch } : r);
    patchDesign({ relationIntents: next });
  };

  const removeRelationIntent = (index: number) => {
    patchDesign({ relationIntents: design.relationIntents.filter((_, i) => i !== index) });
  };

  const addInitialDataRow = () => {
    const emptyRow: Record<string, string> = {};
    namedColumns.forEach((c) => { emptyRow[c.name] = ""; });
    patchDesign({ initialDataRows: [...design.initialDataRows, emptyRow] });
  };

  const patchInitialDataRow = (rowIndex: number, colName: string, value: string) => {
    const next = design.initialDataRows.map((row, i) =>
      i === rowIndex ? { ...row, [colName]: value } : row
    );
    patchDesign({ initialDataRows: next });
  };

  const removeInitialDataRow = (rowIndex: number) => {
    patchDesign({ initialDataRows: design.initialDataRows.filter((_, i) => i !== rowIndex) });
  };

  return (
    <section class="mb-8 rounded border p-4">
      <h2 class="section-title">画面設計（manifest 単体）</h2>
      <p class="mb-3 text-xs text-muted-xs">
        DB table/column・検索・集計・import schema を定義します。ハブ割当・manifest_key は
        <a href="/admin/manifests" class="link font-semibold"> {UX_HUB_MANIFESTS_PAGE}</a>
        で確定してください（contents は grouping intent を確定しません）。
      </p>

      {errors.length > 0 && (
        <ul class="mb-3 list-inside list-disc text-sm text-red-700">
          {errors.map((e) => <li key={e.code ?? e.message}>[{e.code}] {e.message}</li>)}
        </ul>
      )}
      {status && <p class="mb-3 text-sm text-muted-xs">{status}</p>}

      <p class="mb-2 text-xs text-muted-xs">
        データ出所: <strong>{draftSourceLabel}</strong>
        {draftSource === "local" || draftSource === "merged" ? ` — ${MANIFEST_SCREEN_DESIGN_LOCAL_CACHE_NOTE}` : ""}
      </p>

      <div class="mb-4 rounded border border-slate-200 bg-slate-50 p-3 text-xs">
        <p class="font-semibold">単体ページ runtime 投影</p>
        {backendDetail?.summary?.dispatcherMapping && (
          <p class="mt-2 font-mono text-[10px] text-muted-xs">
            dispatcher: {backendDetail.summary.dispatcherMapping.role}/
            {backendDetail.summary.dispatcherMapping.target}/
            {backendDetail.summary.dispatcherMapping.layer}/
            {backendDetail.summary.dispatcherMapping.action}
          </p>
        )}
      </div>

      <div class="mb-4 rounded border border-blue-100 bg-blue-50 p-3 text-xs">
        <p class="mb-2 font-semibold text-blue-800">作成ステップ</p>
        <ol class="space-y-1 text-blue-900">
          <li>① 下書き作成</li>
          <li>② 参照テーブル設定</li>
          <li>③ カラム定義</li>
          <li>④ 初期データ登録</li>
          <li>⑤ テーブル結合意図（任意）</li>
          <li>⑥ 検索キー選択</li>
          <li>⑦ 集計・表示グループ + サンプル表示</li>
          <li>⑧ 内容確認 → 有効化（下の「公開・案内」パネル）</li>
        </ol>
      </div>

      <div class="mb-3 flex flex-wrap gap-2">
        <button type="button" class="btn-secondary" disabled={loading} onClick={handleCreateDraft}>
          ① 下書き作成
        </button>
        <button type="button" class="btn-primary" disabled={loading || !selectedId} onClick={handleSaveAuthoring}>
          ② 設計を保存
        </button>
      </div>
      <p class="mb-2 text-xs text-muted-xs">
        次: 内容確認 → 有効化は下の「公開・案内」パネルで実行してください。
      </p>

      <label class="mb-3 block text-xs">
        既存 manifest
        <select
          class="mt-1 w-full rounded border px-2 py-1 font-mono"
          value={selectedId}
          onChange={(e) => setSelectedId((e.target as HTMLSelectElement).value)}
        >
          <option value="">— 選択 —</option>
          {manifests.map((m) => (
            <option key={m.manifestId} value={m.manifestId}>
              {m.manifestId.slice(0, 8)}… [{UX_STATUS_LABELS[m.status] ?? m.status}]
            </option>
          ))}
        </select>
      </label>

      <div class="grid gap-2 sm:grid-cols-2">
        <label class="text-xs">
          画面ラベル（ローカル表示用）
          <input
            class="mt-1 w-full rounded border px-2 py-1"
            value={design.screenLabel}
            onInput={(e) => patchDesign({ screenLabel: (e.target as HTMLInputElement).value })}
          />
        </label>
        <label class="text-xs">
          操作種別
          <select
            class="mt-1 w-full rounded border px-2 py-1"
            value={design.operationKind}
            onChange={(e) =>
              patchDesign({ operationKind: (e.target as HTMLSelectElement).value as ScreenOperationKind })}
          >
            {SCREEN_OPERATION_OPTIONS.map((o) => (
              <option key={o.kind} value={o.kind}>{o.label}</option>
            ))}
          </select>
        </label>
        <label class="text-xs">
          {UX_FIELD_TABLE_REF}
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            value={design.tableRef}
            onInput={(e) => patchDesign({ tableRef: (e.target as HTMLInputElement).value })}
          />
        </label>
        <label class="text-xs">
          {UX_FIELD_IMPORT_SCHEMA}
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            value={design.importSchemaName}
            onInput={(e) => patchDesign({ importSchemaName: (e.target as HTMLInputElement).value })}
          />
        </label>
      </div>

      {/* ③ カラム定義 */}
      <h3 class="mt-4 text-xs font-semibold">③ カラム定義</h3>
      {design.columns.map((col, index) => (
        <div key={index} class="mt-2 grid gap-2 sm:grid-cols-3">
          <input
            class="rounded border px-2 py-1 text-xs font-mono"
            placeholder="name"
            value={col.name}
            onInput={(e) => {
              const columns = [...design.columns];
              columns[index] = { ...columns[index], name: (e.target as HTMLInputElement).value };
              patchDesign({ columns });
            }}
          />
          <div>
            <select
              class="w-full rounded border px-2 py-1 text-xs font-mono"
              value={COLUMN_TYPE_NORMAL_VIEW_OPTIONS.includes(col.dataType) ? col.dataType : "__advanced__"}
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
                <option key={t} value={t}>{t}</option>
              ))}
              <option value="__advanced__">{UX_COLUMN_TYPE_ADVANCED_LABEL}</option>
            </select>
            {!COLUMN_TYPE_NORMAL_VIEW_OPTIONS.includes(col.dataType) && (
              <input
                class="mt-1 w-full rounded border px-2 py-1 text-xs font-mono"
                placeholder="カスタム型"
                value={col.dataType}
                onInput={(e) => {
                  const columns = [...design.columns];
                  columns[index] = { ...columns[index], dataType: (e.target as HTMLInputElement).value };
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
                columns[index] = { ...columns[index], nullable: (e.target as HTMLInputElement).checked };
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
        onClick={() => patchDesign({ columns: [...design.columns, { name: "", dataType: "text", nullable: true }] })}
      >
        カラムを追加
      </button>

      {/* ④ 初期データ登録 */}
      <h3 class="mt-5 text-xs font-semibold">④ {UX_FIELD_INITIAL_DATA}</h3>
      <p class="mb-2 text-xs text-muted-xs">
        初期データ候補を topology intent として保存します。下のサンプル表示で確認し、内容確認後に manifest を有効化してください。
        この画面から実データを直接登録しません。実データ登録は別のコンテンツ登録フローで行います。
      </p>
      {namedColumns.length === 0 ? (
        <p class="text-xs text-slate-400 italic">カラムを定義してから初期データを追加してください。</p>
      ) : (
        <>
          {design.initialDataRows.length > 0 && (
            <div class="mb-2 overflow-x-auto rounded border border-slate-200">
              <table class="min-w-full text-left text-xs">
                <thead>
                  <tr>
                    {namedColumns.map((c) => (
                      <th key={c.name} class="border-b px-2 py-1 font-semibold text-slate-600 bg-slate-50">{c.name}</th>
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
                            onInput={(e) => patchInitialDataRow(ri, c.name, (e.target as HTMLInputElement).value)}
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

      {/* ⑤ テーブル結合意図（任意） */}
      <h3 class="mt-5 text-xs font-semibold">⑤ {UX_FIELD_RELATION_INTENT}</h3>
      <p class="mb-2 text-xs text-muted-xs">
        このページの data shape に必要な結合のみ指定します。
        作成済み manifest 間の hub 所属・関係設定は
        <a href="/admin/manifests" class="link font-semibold"> {UX_HUB_MANIFESTS_PAGE}</a>
        で管理します。
      </p>
      {design.relationIntents.map((rel, ri) => (
        <div key={ri} class="mb-2 grid gap-2 rounded border border-slate-200 p-2 sm:grid-cols-3">
          <label class="text-xs">
            結合先テーブル
            <input
              class="mt-1 w-full rounded border px-2 py-1 text-xs font-mono"
              placeholder="参照テーブル名"
              value={rel.joinTableRef}
              onInput={(e) => patchRelationIntent(ri, { joinTableRef: (e.target as HTMLInputElement).value })}
            />
          </label>
          <label class="text-xs">
            自テーブルキー
            <input
              class="mt-1 w-full rounded border px-2 py-1 text-xs font-mono"
              placeholder="local key"
              value={rel.localKey}
              onInput={(e) => patchRelationIntent(ri, { localKey: (e.target as HTMLInputElement).value })}
            />
          </label>
          <label class="text-xs">
            結合先キー
            <input
              class="mt-1 w-full rounded border px-2 py-1 text-xs font-mono"
              placeholder="remote key"
              value={rel.remoteKey}
              onInput={(e) => patchRelationIntent(ri, { remoteKey: (e.target as HTMLInputElement).value })}
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
        結合を追加
      </button>

      {/* ⑥ 検索キー選択 */}
      <h3 class="mt-5 text-xs font-semibold">⑥ {UX_FIELD_SEARCH_KEY}</h3>
      <p class="mb-2 text-xs text-muted-xs">
        検索に使うカラムを選択してください（複数選択可）。
      </p>
      {namedColumns.length === 0 ? (
        <p class="text-xs text-slate-400 italic">カラムを定義してから検索キーを選択してください。</p>
      ) : (
        <div class="flex flex-wrap gap-2">
          {namedColumns.map((c) => (
            <label key={c.name} class="flex items-center gap-1 text-xs">
              <input
                type="checkbox"
                checked={design.searchKeyColumns.includes(c.name)}
                onChange={() => toggleSearchKey(c.name)}
              />
              {c.name}
            </label>
          ))}
        </div>
      )}
      {/* Advanced: raw comma-separated input in disclosure */}
      <details class="mt-2">
        <summary class="cursor-pointer text-xs text-slate-500">詳細 / raw 入力</summary>
        <label class="mt-1 block text-xs text-slate-500">
          検索対象（カンマ区切り、上のチェックと独立）
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
            value={design.searchTargets}
            onInput={(e) => patchDesign({ searchTargets: (e.target as HTMLInputElement).value })}
          />
        </label>
      </details>

      {/* ⑦ 集計・表示グループ + サンプル表示 */}
      <h3 class="mt-5 text-xs font-semibold">⑦ {UX_FIELD_AGGREGATION_KEY} / {UX_FIELD_DISPLAY_COLUMNS}</h3>
      <p class="mb-2 text-xs text-muted-xs">
        集計・表示の設定をします。サンプル表示で確認してから保存してください。
      </p>
      <div class="grid gap-2 sm:grid-cols-2">
        <label class="text-xs">
          {UX_FIELD_AGGREGATION_KEY}
          <select
            class="mt-1 w-full rounded border px-2 py-1 text-xs"
            value={design.aggregationKey}
            onChange={(e) => patchDesign({ aggregationKey: (e.target as HTMLSelectElement).value })}
          >
            <option value="">— なし —</option>
            {namedColumns.map((c) => (
              <option key={c.name} value={c.name}>{c.name}</option>
            ))}
          </select>
        </label>
        <div class="text-xs">
          <p class="font-medium mb-1">{UX_FIELD_DISPLAY_COLUMNS}</p>
          {namedColumns.length === 0 ? (
            <p class="text-slate-400 italic">カラムを定義してください。</p>
          ) : (
            <div class="flex flex-wrap gap-2">
              {namedColumns.map((c) => (
                <label key={c.name} class="flex items-center gap-1">
                  <input
                    type="checkbox"
                    checked={design.displayColumns.includes(c.name)}
                    onChange={() => toggleDisplayColumn(c.name)}
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
        <summary class="cursor-pointer text-xs text-slate-500">詳細 / raw 集計仕様</summary>
        <label class="mt-1 block text-xs text-slate-500">
          集計仕様（raw — 上の構造化フィールドと独立）
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
            value={design.aggregationSpec}
            onInput={(e) => patchDesign({ aggregationSpec: (e.target as HTMLInputElement).value })}
          />
        </label>
      </details>

      {/* サンプル表示 */}
      <div class="mt-3">
        <SamplePreviewPanel
          columns={namedColumns}
          aggregationKey={design.aggregationKey}
          displayColumns={design.displayColumns}
          initialDataRows={design.initialDataRows}
        />
      </div>
    </section>
  );
}
