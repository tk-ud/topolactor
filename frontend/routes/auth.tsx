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
        <strong>デモ環境専用です。</strong> 本番での利用はできません。
        <details class="mt-2 text-xs">
          <summary class="cursor-pointer text-yellow-800">技術情報（開発者向け）</summary>
          <div class="mt-1 space-y-0.5 text-yellow-900">
            <p>認証情報は <code>function_parameters</code>（<code>demo_auth / demo_users</code>）に bcrypt ハッシュで保存されます。</p>
            <p>JWT 設定: <code>DEMO_JWT_SECRET</code> / <code>DEMO_JWT_ISSUER</code> 環境変数</p>
            <p>バックエンド接続: <code>DEMO_BACKEND_URL</code></p>
          </div>
        </details>
      </div>

      <AuthPanel />

      <div class="nav-footer">
        <a href="/" class="link">トップ</a>
        {" · "}
        <a href="/admin" class="link">管理（登録）</a>
        {" · "}
        <a href="/demo/debug" class="link">ランタイム検証</a>
        {" · "}
        <a href="/demo" class="link">デモ</a>
      </div>
    </main>
  );
}
