import { JSX } from "preact";
import AuthPanel from "../islands/AuthPanel.tsx";

/**
 * /auth — デモログイン画面。
 */
export default function AuthPage(): JSX.Element {
  return (
    <main class="page-main max-w-md font-sans">
      <h1 class="page-title">topolactor — デモログイン</h1>

      <div class="alert-warn mb-6">
        <strong>デモ用スキャフォールドのみ。</strong> 本番利用不可。
        認証情報は <code class="rounded bg-yellow-100 px-1">function_parameters</code>（
        <code class="rounded bg-yellow-100 px-1">demo_auth / demo_users</code>）に bcrypt ハッシュで保存されます。
        JWT 設定は <code class="rounded bg-yellow-100 px-1">DEMO_JWT_SECRET</code> /{" "}
        <code class="rounded bg-yellow-100 px-1">DEMO_JWT_ISSUER</code> 環境変数から読み込みます。
        バックエンド認証には <code class="rounded bg-yellow-100 px-1">DEMO_BACKEND_URL</code> が必要です。
      </div>

      <AuthPanel />

      <div class="nav-footer">
        <a href="/" class="link">トップ</a>
        {" · "}
        <a href="/admin" class="link">管理（登録）</a>
        {" · "}
        <a href="/admin/runtime" class="link">ランタイム検証</a>
        {" · "}
        <a href="/demo" class="link">デモ</a>
      </div>
    </main>
  );
}
