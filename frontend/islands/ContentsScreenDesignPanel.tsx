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
  MANIFEST_SCREEN_DB_SHAPE_TODO_NOTE,
  type ManifestScreenDesignDraft,
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
} from "../content/adminUxTerms.ts";

type PanelError = { code?: string; message: string };
type DraftSource = "none" | "local" | "backend" | "merged";

export default function ContentsScreenDesignPanel(): JSX.Element {
  const [manifests, setManifests] = useState<AdminManifestListItem[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [backendDetail, setBackendDetail] = useState<AdminManifestDetail | null>(null);
  const [design, setDesign] = useState<ManifestScreenDesignDraft>(emptyManifestScreenDesign());
  const [draftSource, setDraftSource] = useState<DraftSource>("none");
  const [errors, setErrors] = useState<PanelError[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

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
    const summaryAction = detail.summary?.dispatcherMapping?.action ?? "";
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
        searchTargets: parseSearchTargets(design.searchTargets),
        aggregationSpec: design.aggregationSpec || undefined,
        columns: design.columns.filter((c) => c.name.trim()),
        screenOperationKind: design.operationKind,
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

  return (
    <section class="mb-8 rounded border p-4">
      <h2 class="section-title">画面設計（manifest 単体）</h2>
      <p class="mb-3 text-xs text-muted-xs">
        DB table/column・検索・集計・import schema を定義します。ハブ割当・manifest_key は
        <a href="/admin/manifests" class="link font-semibold"> {UX_HUB_MANIFESTS_PAGE}</a>
        で確定してください（contents は grouping intent を確定しません）。
      </p>
      <p class="mb-3 rounded border border-amber-200 bg-amber-50 p-2 text-xs text-amber-900">
        {MANIFEST_SCREEN_DB_SHAPE_TODO_NOTE}
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
        <p class="mb-2 font-semibold text-blue-800">作成ステップ（前半）</p>
        <ol class="space-y-1 text-blue-900">
          <li>① 下書き作成</li>
          <li>② 参照テーブル設定</li>
          <li>③ カラム定義</li>
          <li class="text-slate-400">④ 初期データ登録 — 未実装（次工程）</li>
          <li class="text-slate-400">⑤ テーブル結合意図（任意）— 未実装（後続工程）</li>
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
        ③ 有効化（内容確認 → 有効化）は下の「公開・案内」パネルで実行してください。
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
        <label class="text-xs sm:col-span-2">
          検索対象（カンマ区切り）
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            value={design.searchTargets}
            onInput={(e) => patchDesign({ searchTargets: (e.target as HTMLInputElement).value })}
          />
        </label>
        <label class="text-xs sm:col-span-2">
          集計仕様（viewing key / display columns の構造化は未実装）
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            value={design.aggregationSpec}
            onInput={(e) => patchDesign({ aggregationSpec: (e.target as HTMLInputElement).value })}
          />
        </label>
      </div>

      <h3 class="mt-4 text-xs font-semibold">カラム定義</h3>
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
    </section>
  );
}
