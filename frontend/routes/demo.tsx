import { JSX } from "preact";
import UserDemoStepper from "../islands/UserDemoStepper.tsx";

/**
 * /demo — draft preview surface.
 * Selects an admin-authored projection and projects draft content through it.
 * Layout identity (emission.layoutId) is surfaced in the result when the
 * resolved structure map has a bound layout. Configuration stays under /admin.
 */
export default function Demo(): JSX.Element {
  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">topolactor — ドラフトプレビュー</h1>

      <div class="alert-info mb-6">
        <strong>ドラフトプレビュー:</strong> admin で構築したレイアウト・コンテンツの投影を確認できます。
        先に <a href="/auth" class="link">ログイン</a> してください。
        ページの構築は <a href="/admin" class="link">管理画面</a> から行います。
      </div>

      <section class="mb-8">
        <UserDemoStepper />
      </section>

      <div class="nav-footer">
        <a href="/" class="link">トップ</a>
        {" · "}
        <a href="/demo/debug" class="link">開発者向け検証</a>
        {" · "}
        <a href="/admin" class="link">管理画面</a>
      </div>
    </main>
  );
}
