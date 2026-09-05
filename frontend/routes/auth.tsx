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
        アカウントでログインしてください。
        <details class="mt-2 text-xs">
          <summary class="cursor-pointer text-slate-500">技術情報（開発者向け）</summary>
          <div class="mt-1 space-y-0.5 text-slate-600">
            <p>画面定義: DB seed manifest 駆動</p>
            <p>認証正本: auth 専用 DB 正本</p>
          </div>
        </details>
      </div>

      <LoginManifestPanel />

      <div class="nav-footer">
        <a href="/" class="link">トップ</a>
        {" · "}
        <a href="/auth#register" class="link">新規登録</a>
        {" · "}
        <a href="/super_auth" class="link">管理ログイン</a>
      </div>
    </main>
  );
}
