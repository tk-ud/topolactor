import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import HubNavigationAdmin from "./HubNavigationAdmin.tsx";
import {
  listHubNavigationManifests,
  type HubNavigationManifestItem,
} from "../api/adminApi.ts";
import AdminHowTo from "../components/AdminHowTo.tsx";
import AdminHelpPanel from "../components/AdminHelpPanel.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { ADMIN_MANIFESTS_GUIDE } from "../content/adminGuides.ts";
import { UX_CONTENTS_PAGE, UX_HUB_MANIFESTS } from "../content/adminUxTerms.ts";

type PanelError = { code?: string; message: string };

export default function ManifestsAdmin(): JSX.Element {
  const [topologyManifests, setTopologyManifests] = useState<HubNavigationManifestItem[]>([]);
  const [errors, setErrors] = useState<PanelError[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [backendUnavailable, setBackendUnavailable] = useState(false);
  const [loading, setLoading] = useState(false);

  const loadAll = async () => {
    setLoading(true);
    setErrors([]);
    setBackendUnavailable(false);
    try {
      const manifests = await listHubNavigationManifests();
      if (manifests === null) {
        setBackendUnavailable(true);
        setTopologyManifests([]);
        return;
      }
      setTopologyManifests(manifests);
      setStatus(`登録済み topology_manifest ${manifests.length} 件`);
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAll();
  }, []);

  return (
    <main class="page-main font-mono">
      <h1 class="page-title">topolactor — 管理 / {UX_HUB_MANIFESTS}</h1>
      <p class="mb-4"><a href="/admin" class="link">&larr; 管理インデックス</a></p>

      <AdminHowTo steps={ADMIN_MANIFESTS_GUIDE.howToSteps} />
      <AdminHelpPanel {...ADMIN_MANIFESTS_GUIDE} />

      <section class="mb-6 rounded-lg border border-blue-200 bg-blue-50 p-4 text-sm text-blue-900">
        <p class="font-semibold">この画面の責務: 既存 manifest の relation / hub 操作</p>
        <ul class="mt-2 list-inside list-disc text-xs">
          <li>新規 manifest 作成 → <a href="/admin/contents" class="link font-semibold">{UX_CONTENTS_PAGE}</a></li>
          <li>promote 前の draft hub 割当はこの画面では扱いません。</li>
          <li>登録済み topology_manifest の relation 追加・編集・並び替えを扱います。</li>
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
        </div>

        <h2 class="section-title">1. 登録済み topology_manifest（canonical）</h2>
        {topologyManifests.length === 0 ? (
          <p class="text-sm text-muted-xs">
            まだ topology_manifest がありません。{UX_CONTENTS_PAGE} で新規 manifest を作成し promote してください。
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
                {topologyManifests.map((manifest) => (
                  <tr key={manifest.topologyManifestId} class="border-b">
                    <td class="px-2 py-1"><code class="text-xs">{manifest.topologyManifestId.slice(0, 8)}…</code></td>
                    <td class="px-2 py-1">{manifest.manifestKey}</td>
                    <td class="px-2 py-1"><code class="text-xs">{manifest.hubId.slice(0, 8)}…</code></td>
                    <td class="px-2 py-1">{manifest.hubRelationCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section class="mb-8 rounded border border-slate-200 bg-slate-50 p-4">
        <h2 class="section-title">2. 既存 manifest の relation / hub 操作</h2>
        <p class="mb-4 text-xs text-muted-xs">
          登録済み topology_manifest の画面間 relation を追加・編集・並び替えします。
        </p>
        <HubNavigationAdmin />
      </section>
    </main>
  );
}
