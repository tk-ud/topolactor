import { JSX } from "preact";
import SuperAuthPanel from "../islands/SuperAuthPanel.tsx";

/**
 * /super_auth — 管理コンソール用デモログイン（admin realm）。
 */
export default function SuperAuthPage(): JSX.Element {
  return (
    <main class="page-main max-w-md font-sans">
      <h1 class="page-title">topolactor — 管理ログイン</h1>

      <div class="alert-warn mb-6">
        <strong>管理コンソール専用です。</strong> 通常の利用は{" "}
        <a href="/auth" class="link font-semibold">ユーザログイン (/auth)</a> から行ってください。
        <details class="mt-2 text-xs">
          <summary class="cursor-pointer text-yellow-800">技術情報（開発者向け）</summary>
          <div class="mt-1 space-y-0.5 text-yellow-900">
            <p>認証正本: <code>auth.users</code> / <code>auth.credentials</code>（realm=admin/system）</p>
            <p>JWT: <code>realm=admin/system</code>, <code>aud=admin_console</code>, <code>role=admin</code></p>
            <p>バックエンド: <code>POST /super_auth/login</code></p>
          </div>
        </details>
      </div>

      <SuperAuthPanel />

      <div class="nav-footer">
        <a href="/" class="link">トップ</a>
        {" · "}
        <a href="/admin" class="link">管理（登録）</a>
        {" · "}
        <a href="/auth" class="link">ユーザログイン</a>
      </div>
    </main>
  );
}
