import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import HubNavigationAdmin from "./HubNavigationAdmin.tsx";
import {
  type HubNavigationManifestItem,
  listHubNavigationManifests,
} from "../api/adminApi.ts";
import AdminHowTo from "../components/AdminHowTo.tsx";
import AdminHelpPanel from "../components/AdminHelpPanel.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { ADMIN_MANIFESTS_GUIDE } from "../content/adminGuides.ts";
import { UX_CONTENTS_PAGE, UX_HUB_MANIFESTS } from "../content/adminUxTerms.ts";

type PanelError = { code?: string; message: string };

export default function ManifestsAdmin(): JSX.Element {
  const [topologyManifests, setTopologyManifests] = useState<
    HubNavigationManifestItem[]
  >([]);
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
      setStatus(`作成済みページ ${manifests.length} 件`);
    } catch (e) {
      console.error("PAGE_CONNECTIONS_LOAD_FAILED", e);
      setErrors([{
        message:
          "作成済みページを読み込めませんでした。接続状態を確認して再度お試しください。",
      }]);
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
      <p class="mb-4">
        <a href="/admin" class="link">&larr; 管理インデックス</a>
      </p>

      <AdminHowTo steps={ADMIN_MANIFESTS_GUIDE.howToSteps} />
      <AdminHelpPanel {...ADMIN_MANIFESTS_GUIDE} />

      <section class="mb-6 rounded-lg border border-blue-200 bg-blue-50 p-4 text-sm text-blue-900">
        <p class="font-semibold">
          この画面でできること: 作成済みページをつなぐ
        </p>
        <ul class="mt-2 list-inside list-disc text-xs">
          <li>
            新しいページの作成 →{" "}
            <a href="/admin/contents" class="link font-semibold">
              {UX_CONTENTS_PAGE}
            </a>
          </li>
          <li>作成済みページの所属先を管理します。</li>
          <li>ページ間のつながりとナビゲーションの表示順を管理します。</li>
        </ul>
      </section>

      {backendUnavailable && (
        <p class="alert-warning mb-4 text-sm" role="status">
          サーバーに接続できません。環境設定を確認し、再読み込みしてください。
        </p>
      )}

      <ValidationErrorPanel errors={errors} title="エラー" />
      {status && <p class="mb-4 text-sm text-muted-xs">{status}</p>}

      <section class="mb-8">
        <div class="mb-3 flex flex-wrap gap-2">
          <button
            type="button"
            class="btn-secondary"
            disabled={loading}
            onClick={loadAll}
          >
            再読み込み
          </button>
        </div>

        <h2 class="section-title">1. 作成済みページ</h2>
        {topologyManifests.length === 0
          ? (
            <p class="text-sm text-muted-xs">
              まだページがありません。{UX_CONTENTS_PAGE}で下書きを作成し、有効化してください。
            </p>
          )
          : (
            <div class="overflow-x-auto">
              <table class="w-full border-collapse text-sm">
                <thead>
                  <tr class="border-b bg-slate-50 text-left">
                    {["ページ設定", "ナビ設定数", "技術情報"].map((h) => (
                      <th key={h} class="px-2 py-1 font-semibold">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {topologyManifests.map((manifest) => (
                    <tr key={manifest.topologyManifestId} class="border-b">
                      <td class="px-2 py-1">{manifest.manifestKey}</td>
                      <td class="px-2 py-1">{manifest.hubRelationCount}</td>
                      <td class="px-2 py-1">
                        <details class="text-muted-xs">
                          <summary class="cursor-pointer">
                            内部 ID を表示
                          </summary>
                          <dl class="mt-1 grid grid-cols-[auto_1fr] gap-x-2 font-mono">
                            <dt>topology_manifest_id</dt>
                            <dd>{manifest.topologyManifestId}</dd>
                            <dt>hub_id</dt>
                            <dd>{manifest.hubId}</dd>
                          </dl>
                        </details>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
      </section>

      <section class="mb-8 rounded border border-slate-200 bg-slate-50 p-4">
        <h2 class="section-title">2. 所属先 / ページ間のつながり / 表示順</h2>
        <p class="mb-4 text-xs text-muted-xs">
          作成済みページの所属先、ページ間のつながり、ナビゲーションの表示順をまとめて管理します。
        </p>
        <HubNavigationAdmin />
      </section>
    </main>
  );
}
