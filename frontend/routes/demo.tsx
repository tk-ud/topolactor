import { JSX } from "preact";
import UserDemoStepper from "../islands/UserDemoStepper.tsx";

/**
 * /demo — 一般ユーザー向け体験面。Stepper 型で目的選択 → 結果確認 → 次アクションを提供する。
 * 開発者向け runtime 検証は /demo/debug または /admin/runtime を利用。
 */
export default function Demo(): JSX.Element {
  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">topolactor — デモ</h1>

      <div class="alert-info mb-6">
        <strong>デモページ:</strong> seed 済み demo トポロジを体験できます。
        先に <a href="/auth" class="link">ログイン</a> してください。
        開発者向け runtime 検証は{" "}
        <a href="/demo/debug" class="link">/demo/debug</a> または{" "}
        <a href="/admin/runtime" class="link">/admin/runtime</a> をご利用ください。
      </div>

      <section class="mb-8">
        <UserDemoStepper />
      </section>

      <div class="nav-footer">
        <a href="/" class="link">トップ</a>
        {" · "}
        <a href="/demo/debug" class="link">デバッグ</a>
        {" · "}
        <a href="/admin/runtime" class="link">ランタイム検証</a>
        {" · "}
        <a href="/demo-static" class="link">デモ（静的）</a>
        {" · "}
        <a href="/admin" class="link">管理画面</a>
      </div>
    </main>
  );
}
