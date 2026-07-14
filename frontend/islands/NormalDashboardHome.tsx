/**
 * NormalDashboardHome — authenticated landing surface for /dashboard.
 * Available to any authenticated session (Normal or admin); links out to the surfaces this
 * Bundle implements. role/subject are shown for display only (see hooks/useCurrentSession.ts).
 *
 * This surface has no business projection of its own (see catalog.ts normal_dashboard_home.projection
 * notes). Beyond the fixed feature links above, RelatedHubLinksPanel renders a read-only navigation
 * link list the BACKEND derives from the existing hub_relation authority (GET
 * /api/hub-navigation/relations -> HubNavigationResolver) — never a frontend-hardcoded list, and an
 * explicit empty state when nothing resolves.
 */
import { useEffect, useState } from "preact/hooks";
import { useCurrentSession } from "../hooks/useCurrentSession.ts";
import { fetchHubRelationNavigationLinks, type HubRelationNavigationLink } from "../api/hubNavigationApi.ts";

export function RelatedHubLinksPanel({ token }: { token: string }) {
  const [links, setLinks] = useState<HubRelationNavigationLink[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const result = await fetchHubRelationNavigationLinks(token);
        if (!result.success) {
          setError(result.errors?.[0]?.message ?? "関連ハブの取得に失敗しました。");
          return;
        }
        setLinks(result.links);
      } catch (err) {
        setError(err instanceof Error ? err.message : "関連ハブの取得に失敗しました。");
      }
    })();
  }, [token]);

  return (
    <section class="page-section mt-4" aria-label="Related hub navigation">
      <h2 class="text-lg font-semibold text-gray-900">関連ハブ</h2>
      {error && <div class="md-dashboard-error" role="alert">{error}</div>}
      {links === null && !error && <p class="text-muted">読み込み中…</p>}
      {links !== null && links.length === 0 && (
        <p class="text-muted">現在、遷移可能な関連ハブはありません。</p>
      )}
      {links !== null && links.length > 0 && (
        <ul class="mt-2 space-y-1">
          {links.map((l) => (
            <li key={l.relatedHubId} class="text-sm text-gray-700">
              {l.relatedHubLabel}
              {!l.targetManifestId && (
                <span class="ml-2 text-xs text-gray-500">（直接遷移不可）</span>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

export default function NormalDashboardHome() {
  const session = useCurrentSession();

  if (session.status !== "present") {
    return <p class="text-muted">ログイン状態を確認しています...</p>;
  }

  return (
    <div class="page-main-wide">
      <h1 class="text-xl font-bold text-gray-900">ダッシュボード</h1>
      <p class="mt-2 mb-4 text-sm text-gray-600">
        ログイン中: <strong>{session.subject ?? "unknown"}</strong>{" "}
        （role: <code class="rounded bg-gray-100 px-1">{session.role ?? "unknown"}</code>）
      </p>
      <ul class="mt-4 space-y-2">
        <li>
          <a href="/dashboard/team" class="link">Team Dashboard を開く</a>
          <span class="ml-2 text-xs text-gray-500">閲覧・検索は全ユーザー、編集は管理者のみ</span>
        </li>
        <li>
          <a href="/account" class="link">個人ページ（アカウント情報 / パスワード変更 / セッション管理）</a>
        </li>
        {session.role === "admin" && (
          <li>
            <a href="/admin" class="link">管理コンソールを開く</a>
          </li>
        )}
      </ul>
      <RelatedHubLinksPanel token={session.token} />
    </div>
  );
}
