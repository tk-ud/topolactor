import { JSX } from "preact";
import DraftPreviewShell from "../islands/DraftPreviewShell.tsx";

/**
 * /demo — draft preview surface.
 * Renders layout selector (admin-authored) + draft content selector + preview projection.
 */
export default function Demo(): JSX.Element {
  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">topolactor — ドラフトプレビュー</h1>
      <p class="mb-6 text-sm text-gray-500">
        admin-authored layout とドラフトコンテンツを選択して投影を確認できます。
      </p>

      <DraftPreviewShell />

      <div class="nav-footer mt-8">
        <a href="/" class="link">トップ</a>
        {" · "}
        <a href="/demo/debug" class="link">開発者向け検証</a>
        {" · "}
        <a href="/admin" class="link">管理画面</a>
      </div>
    </main>
  );
}
