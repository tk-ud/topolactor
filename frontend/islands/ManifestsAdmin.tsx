import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  listAdminManifests,
  assignAdminManifestHubGrouping,
  type AdminManifestListItem,
} from "../api/adminApi.ts";
import { listContentHubs, listHubNavigationManifests, type HubNavigationManifestItem } from "../api/adminApi.ts";
import AdminHowTo from "../components/AdminHowTo.tsx";
import AdminHelpPanel from "../components/AdminHelpPanel.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { ADMIN_MANIFESTS_GUIDE } from "../content/adminGuides.ts";
import {
  UX_CONTENTS,
  UX_CONTENTS_PAGE,
  UX_HUB_MANIFESTS,
  UX_STATUS_LABELS,
} from "../content/adminUxTerms.ts";

type PanelError = { code?: string; message: string };

export default function ManifestsAdmin(): JSX.Element {
  const [topologyManifests, setTopologyManifests] = useState<HubNavigationManifestItem[]>([]);
  const [draftManifests, setDraftManifests] = useState<AdminManifestListItem[]>([]);
  const [hubOptions, setHubOptions] = useState<{ id: string; label: string }[]>([]);
  const [assignManifestId, setAssignManifestId] = useState("");
  const [assignHubId, setAssignHubId] = useState("");
  const [assignManifestKey, setAssignManifestKey] = useState("");
  const [errors, setErrors] = useState<PanelError[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [backendUnavailable, setBackendUnavailable] = useState(false);
  const [loading, setLoading] = useState(false);

  const loadAll = async () => {
    setLoading(true);
    setErrors([]);
    setBackendUnavailable(false);
    try {
      const [tm, drafts, hubs] = await Promise.all([
        listHubNavigationManifests(),
        listAdminManifests("draft"),
        listContentHubs(),
      ]);
      if (tm === null || drafts === null || hubs === null) {
        setBackendUnavailable(true);
        setTopologyManifests([]);
        setDraftManifests([]);
        return;
      }
      setTopologyManifests(tm);
      setDraftManifests(drafts);
      setHubOptions(hubs.map((h) => ({ id: h.id, label: h.label || h.id })));
      if (!assignHubId && hubs.length > 0) setAssignHubId(hubs[0].id);
      setStatus(`topology_manifest ${tm.length} 件 / 下書き manifest ${drafts.length} 件`);
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAll();
  }, []);

  const handleAssignGrouping = async () => {
    if (!assignManifestId || !assignHubId || !assignManifestKey.trim()) {
      setErrors([{ message: "下書き画面・親ハブ・manifest キーは必須です。" }]);
      return;
    }
    setLoading(true);
    setErrors([]);
    try {
      await assignAdminManifestHubGrouping(
        assignManifestId,
        assignHubId,
        assignManifestKey.trim(),
      );
      setStatus("ハブへの割当を下書きに保存しました。有効化はコンテンツ画面から promote してください。");
      await loadAll();
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  return (
    <main class="page-main font-mono">
      <h1 class="page-title">topolactor — 管理 / {UX_HUB_MANIFESTS}</h1>
      <p class="mb-4"><a href="/admin" class="link">&larr; 管理インデックス</a></p>

      <AdminHowTo steps={ADMIN_MANIFESTS_GUIDE.howToSteps} />
      <AdminHelpPanel {...ADMIN_MANIFESTS_GUIDE} />

      <section class="mb-6 rounded-lg border border-blue-200 bg-blue-50 p-4 text-sm text-blue-900">
        <p class="font-semibold">この画面の責務: ハブ設計・画面群（topology_manifest grouping）</p>
        <ul class="mt-2 list-inside list-disc text-xs">
          <li>単一画面の DB 設計・data shape → <a href="/admin/contents" class="link font-semibold">{UX_CONTENTS_PAGE}</a></li>
          <li>画面間の遷移順序 → <a href="/admin/hub-navigation" class="link font-semibold">遷移順序設定</a></li>
        </ul>
      </section>

      {backendUnavailable && (
        <p class="alert-warning mb-4 text-sm" role="status">
          バックエンド未接続 — hub / manifest API は DB + dispatch 経由でのみ利用できます。
        </p>
      )}

      <ValidationErrorPanel errors={errors} title="エラー" />
      {status && <p class="mb-4 text-sm text-muted-xs">{status}</p>}

      <section class="mb-8">
        <div class="mb-3 flex flex-wrap gap-2">
          <button type="button" class="btn-secondary" disabled={loading} onClick={loadAll}>
            再読み込み
          </button>
          <a href="/admin/hub-navigation" class="btn-primary inline-block">遷移順序を設定</a>
        </div>

        <h2 class="section-title">1. 登録済み topology_manifest（canonical）</h2>
        {topologyManifests.length === 0 ? (
          <p class="text-sm text-muted-xs">
            まだ topology_manifest がありません。{UX_CONTENTS_PAGE} で画面を定義し promote するとここに投影されます。
          </p>
        ) : (
          <div class="overflow-x-auto">
            <table class="w-full border-collapse text-sm">
              <thead>
                <tr class="border-b bg-slate-50 text-left">
                  {["topology_manifest_id", "manifest_key", "hub_id", "hub_relation 数"].map((h) => (
                    <th key={h} class="px-2 py-1 font-semibold">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {topologyManifests.map((m) => (
                  <tr key={m.topologyManifestId} class="border-b">
                    <td class="px-2 py-1"><code class="text-xs">{m.topologyManifestId.slice(0, 8)}…</code></td>
                    <td class="px-2 py-1">{m.manifestKey}</td>
                    <td class="px-2 py-1"><code class="text-xs">{m.hubId.slice(0, 8)}…</code></td>
                    <td class="px-2 py-1">{m.hubRelationCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section class="mb-8 rounded border p-4">
        <h2 class="section-title">2. 下書き画面のハブ割当（promote 前）</h2>
        <p class="mb-3 text-xs text-muted-xs">
          {UX_CONTENTS} で作成した下書き manifest に親 hub と manifest_key を付与します。promote 時に hubs.topology_manifests へ投影されます。
        </p>
        <div class="grid gap-3 sm:grid-cols-2">
          <label class="text-xs">
            下書き manifest
            <select
              class="mt-1 w-full rounded border px-2 py-1 font-mono"
              value={assignManifestId}
              onChange={(e) => setAssignManifestId((e.target as HTMLSelectElement).value)}
            >
              <option value="">— 選択 —</option>
              {draftManifests.map((m) => (
                <option key={m.manifestId} value={m.manifestId}>
                  {m.manifestId.slice(0, 8)}… [{UX_STATUS_LABELS[m.status] ?? m.status}]
                </option>
              ))}
            </select>
          </label>
          <label class="text-xs">
            親 hub
            <select
              class="mt-1 w-full rounded border px-2 py-1 font-mono"
              value={assignHubId}
              onChange={(e) => setAssignHubId((e.target as HTMLSelectElement).value)}
            >
              {hubOptions.map((h) => (
                <option key={h.id} value={h.id}>{h.label}</option>
              ))}
            </select>
          </label>
          <label class="text-xs sm:col-span-2">
            manifest_key（画面群内の識別子）
            <input
              class="mt-1 w-full rounded border px-2 py-1 font-mono"
              value={assignManifestKey}
              onInput={(e) => setAssignManifestKey((e.target as HTMLInputElement).value)}
              placeholder="例: entity_list_primary"
            />
          </label>
        </div>
        <button
          type="button"
          class="btn-primary mt-4"
          disabled={loading || !assignManifestId}
          onClick={handleAssignGrouping}
        >
          ハブ割当を保存
        </button>
      </section>
    </main>
  );
}
