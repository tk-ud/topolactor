import { JSX } from "preact";

/**
 * /demo — draft preview surface (not yet implemented).
 * Draft preview requires: layout selector (admin-authored layouts) + draft content selector
 * (content_entity_drafts). Both require API endpoints not yet available here.
 * Use /demo/debug for runtime inspection.
 */
export default function Demo(): JSX.Element {
  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">topolactor — ドラフトプレビュー</h1>

      <div class="alert-info mb-6">
        <p class="font-semibold">ドラフトプレビューは未実装です</p>
        <p class="mt-2 text-sm">
          admin-authored layout とドラフトコンテンツを選択して投影する機能は今後実装されます。
          runtime の検証は
          {" "}<a href="/demo/debug" class="link">開発者向け検証</a>{" "}
          を利用してください。
          layout・コンテンツの構築は
          {" "}<a href="/admin" class="link">管理画面</a>{" "}
          で行います。
        </p>
      </div>

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
