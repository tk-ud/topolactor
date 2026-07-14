/**
 * NormalDashboardHome — authenticated landing surface for /dashboard.
 * Available to any authenticated session (Normal or admin); links out to the surfaces this
 * Bundle implements. role/subject are shown for display only (see hooks/useCurrentSession.ts).
 */
import { useCurrentSession } from "../hooks/useCurrentSession.ts";

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
    </div>
  );
}
