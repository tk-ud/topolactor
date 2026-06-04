import { JSX } from "preact";
import LoginManifestPanel from "../islands/LoginManifestPanel.tsx";

/**
 * /auth — 通常ユーザ向けログイン（seed manifest 駆動 UI）。
 */
export default function AuthPage(): JSX.Element {
  return (
    <main class="page-main max-w-md font-sans">
      <h1 class="page-title">topolactor — ログイン</h1>

      <div class="alert-info mb-6 text-sm">
        画面定義は DB seed manifest から読み込みます。認証処理は auth 専用 DB 正本へ委譲されます。
      </div>

      <LoginManifestPanel />

      <div class="nav-footer">
        <a href="/" class="link">トップ</a>
        {" · "}
        <a href="/admin" class="link">管理（登録）</a>
        {" · "}
        <a href="/super_auth" class="link">管理ログイン</a>
        {" · "}
        <a href="/demo" class="link">デモ</a>
      </div>
    </main>
  );
}
