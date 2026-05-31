import { JSX } from "preact";
import UserDemoStepper from "../islands/UserDemoStepper.tsx";

/**
 * /demo — admin構築済み demo project projection の体験監査 preview 面。
 * UX構築 authority は持たない。構築・編集は /admin で行う。
 * raw runtime 検証は /demo/debug または /admin/runtime を利用。
 */
export default function Demo(): JSX.Element {
  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">topolactor — Demo preview</h1>

      <div class="alert-info mb-6">
        <strong>Demo preview:</strong> admin で構築した demo project projection を確認できます。
        先に <a href="/auth" class="link">ログイン</a> してください。
        raw runtime 検証は{" "}
        <a href="/demo/debug" class="link">/demo/debug</a> または{" "}
        <a href="/admin/runtime" class="link">/admin/runtime</a>、
        構築・編集は <a href="/admin" class="link">/admin</a> をご利用ください。
      </div>

      <section class="mb-8">
        <UserDemoStepper />
      </section>

      <div class="nav-footer">
        <a href="/" class="link">トップ</a>
        {" · "}
        <a href="/demo/debug" class="link">raw 検証</a>
        {" · "}
        <a href="/admin/runtime" class="link">admin runtime</a>
        {" · "}
        <a href="/demo-static" class="link">デモ（静的）</a>
        {" · "}
        <a href="/admin" class="link">admin</a>
      </div>
    </main>
  );
}
