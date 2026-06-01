import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  listAdminManifests,
  createAdminManifestDraft,
  validateAdminManifest,
  promoteAdminManifest,
  assignAdminManifestHubGrouping,
  assignAdminManifestScreenDataShape,
  type AdminManifestListItem,
} from "../api/adminApi.ts";
import { listContentHubs } from "../api/adminApi.ts";
import {
  SCREEN_OPERATION_OPTIONS,
  buildDraftInputFromScreenIntent,
  setStoredScreenLabel,
  type ScreenOperationKind,
} from "../runtime/screenAuthoringIntent.ts";
import {
  emptyManifestScreenDesign,
  loadManifestScreenDesign,
  parseSearchTargets,
  saveManifestScreenDesign,
  type ManifestScreenDesignDraft,
} from "../lib/manifestScreenDesign.ts";
import { UX_HUB_MANIFESTS_PAGE, UX_STATUS_LABELS } from "../content/adminUxTerms.ts";

type PanelError = { code?: string; message: string };

export default function ContentsScreenDesignPanel(): JSX.Element {
  const [manifests, setManifests] = useState<AdminManifestListItem[]>([]);
  const [hubs, setHubs] = useState<{ id: string; label: string }[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [design, setDesign] = useState<ManifestScreenDesignDraft>(emptyManifestScreenDesign());
  const [errors, setErrors] = useState<PanelError[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const loadManifests = async () => {
    const [m, h] = await Promise.all([listAdminManifests(), listContentHubs()]);
    if (m) setManifests(m);
    if (h) setHubs(h.map((x) => ({ id: x.id, label: x.label || x.id })));
  };

  useEffect(() => {
    loadManifests();
  }, []);

  useEffect(() => {
    if (!selectedId) {
      setDesign(emptyManifestScreenDesign());
      return;
    }
    const stored = loadManifestScreenDesign(selectedId);
    if (stored) setDesign(stored);
    else setDesign({ ...emptyManifestScreenDesign(), hubId: hubs[0]?.id ?? "" });
  }, [selectedId, hubs]);

  const patchDesign = (patch: Partial<ManifestScreenDesignDraft>) => {
    setDesign((prev) => {
      const next = { ...prev, ...patch };
      if (selectedId) saveManifestScreenDesign(selectedId, next);
      return next;
    });
  };

  const handleCreateDraft = async () => {
    setLoading(true);
    setErrors([]);
    try {
      const draftInput = buildDraftInputFromScreenIntent({
        operationKind: design.operationKind,
      });
      const created = await createAdminManifestDraft({
        ...draftInput,
        screenOperationKind: design.operationKind,
      });
      setSelectedId(created.manifestId);
      if (design.screenLabel.trim()) {
        setStoredScreenLabel(created.manifestId, design.screenLabel.trim());
      }
      saveManifestScreenDesign(created.manifestId, { ...design, hubId: design.hubId || hubs[0]?.id || "" });
      setStatus(`下書き manifest を作成: ${created.manifestId}`);
      await loadManifests();
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
    if (!design.hubId || !design.manifestKey.trim()) {
      setErrors([{ message: "親 hub と manifest_key は必須です（promote 前に hubs.topology_manifests 投影に使用）。" }]);
      return;
    }
    setLoading(true);
    setErrors([]);
    try {
      if (design.screenLabel.trim()) setStoredScreenLabel(selectedId, design.screenLabel.trim());
      saveManifestScreenDesign(selectedId, design);
      await assignAdminManifestScreenDataShape({
        manifestId: selectedId,
        dbTableName: design.dbTableName || undefined,
        importSchemaName: design.importSchemaName || undefined,
        searchTargets: parseSearchTargets(design.searchTargets),
        aggregationSpec: design.aggregationSpec || undefined,
        columns: design.columns.filter((c) => c.name.trim()),
      });
      await assignAdminManifestHubGrouping(selectedId, design.hubId, design.manifestKey.trim());
      setStatus("画面設計とハブ割当を下書きに保存しました。");
      await loadManifests();
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handlePromote = async () => {
    if (!selectedId) return;
    setLoading(true);
    setErrors([]);
    try {
      const validation = await validateAdminManifest(selectedId);
      if (validation && !validation.valid) {
        setErrors(validation.issues.map((i) => ({ code: i.code, message: i.message })));
        setStatus("promote 前に内容確認で問題を解消してください。");
        return;
      }
      const result = await promoteAdminManifest(selectedId);
      if (!result?.ok) {
        setErrors([{ code: result?.errorCode, message: result?.message ?? "promote failed" }]);
        return;
      }
      setStatus(`有効化完了 — topology_manifests へ投影済み。次: ${UX_HUB_MANIFESTS_PAGE}`);
      await loadManifests();
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  return (
    <section class="mb-8 rounded border p-4">
      <h2 class="section-title">画面設計（manifest 単体）</h2>
      <p class="mb-3 text-xs text-muted-xs">
        DB table/column、検索対象、集計、import schema を定義し、promote で hubs.topology_manifests に投影します。
      </p>

      {errors.length > 0 && (
        <ul class="mb-3 list-inside list-disc text-sm text-red-700">
          {errors.map((e) => <li key={e.code ?? e.message}>[{e.code}] {e.message}</li>)}
        </ul>
      )}
      {status && <p class="mb-3 text-sm text-muted-xs">{status}</p>}

      <div class="mb-3 flex flex-wrap gap-2">
        <button type="button" class="btn-secondary" disabled={loading} onClick={handleCreateDraft}>
          新規下書き（操作種別から軸を導出）
        </button>
        <button type="button" class="btn-primary" disabled={loading || !selectedId} onClick={handleSaveAuthoring}>
          設計を下書きに保存
        </button>
        <button type="button" class="btn-secondary" disabled={loading || !selectedId} onClick={handlePromote}>
          有効化（canonical 投影）
        </button>
      </div>

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
          画面ラベル
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
          DB テーブル名
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            value={design.dbTableName}
            onInput={(e) => patchDesign({ dbTableName: (e.target as HTMLInputElement).value })}
          />
        </label>
        <label class="text-xs">
          import schema 名
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
          集計仕様
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            value={design.aggregationSpec}
            onInput={(e) => patchDesign({ aggregationSpec: (e.target as HTMLInputElement).value })}
          />
        </label>
        <label class="text-xs">
          親 hub（promote 投影用）
          <select
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            value={design.hubId}
            onChange={(e) => patchDesign({ hubId: (e.target as HTMLSelectElement).value })}
          >
            {hubs.map((h) => <option key={h.id} value={h.id}>{h.label}</option>)}
          </select>
        </label>
        <label class="text-xs">
          manifest_key
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            value={design.manifestKey}
            onInput={(e) => patchDesign({ manifestKey: (e.target as HTMLInputElement).value })}
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
          <input
            class="rounded border px-2 py-1 text-xs font-mono"
            placeholder="type"
            value={col.dataType}
            onInput={(e) => {
              const columns = [...design.columns];
              columns[index] = { ...columns[index], dataType: (e.target as HTMLInputElement).value };
              patchDesign({ columns });
            }}
          />
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
            nullable
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
