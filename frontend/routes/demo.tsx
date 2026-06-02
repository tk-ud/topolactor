import { JSX } from "preact";
import UserDemoStepper from "../islands/UserDemoStepper.tsx";

/** /demo — user-facing preview. Configuration and editing stay under /admin. */
export default function Demo(): JSX.Element {
  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">topolactor — デモ</h1>

      <div class="alert-info mb-6">
        <strong>デモ:</strong> 設定済みページの表示と操作を確認できます。 先に
        {" "}
        <a href="/auth" class="link">ログイン</a> してください。 ページの設定は
        {" "}
        <a href="/admin" class="link">管理画面</a> から行えます。
      </div>

      <section class="mb-8">
        <UserDemoStepper />
      </section>

      <div class="nav-footer">
        <a href="/" class="link">トップ</a>
        {" · "}
        <a href="/demo/debug" class="link">開発者向け検証</a>
        {" · "}
        <a href="/demo-static" class="link">デモ（静的）</a>
        {" · "}
        <a href="/admin" class="link">管理画面</a>
      </div>
    </main>
  );
}
