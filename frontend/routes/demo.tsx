import { JSX } from "preact";
import DraftPreviewShell from "../islands/DraftPreviewShell.tsx";

/**
 * /demo — pre-publish projection preview (read-only).
 * Layout from UI Builder Apply; content from manifest screen_data_shape.initialDataRows.
 */
export default function Demo(): JSX.Element {
  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">topolactor — 公開前プレビュー</h1>
      <p class="mb-6 text-sm text-gray-500">
        UIビルダーで Apply した配置と、Contents で保存した manifest のサンプルデータ（topology intent）を投影します。
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
