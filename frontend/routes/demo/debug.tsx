import { JSX } from "preact";
import OperationPanel from "../../islands/OperationPanel.tsx";

/**
 * /demo/debug — demo project の raw runtime 検証面。
 * raw dispatch vector・emission JSON・SQL Attention 投影など内部情報を確認できる。
 * project construction authority はなく、admin構築済み projection の raw 検証が目的。
 * 通常の preview 監査は /demo を利用。
 */
export default function DemoDebug(): JSX.Element {
  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">topolactor — Demo / raw runtime 検証</h1>

      <div class="alert-info mb-6">
        <strong>raw runtime 検証面:</strong> admin構築済み demo project projection の
        raw dispatch vector・emission JSON・SQL Attention 投影を確認します。
        project construction authority はありません。構築・編集は{" "}
        <a href="/admin" class="link">/admin</a> で行ってください。
        通常の preview 監査は <a href="/demo" class="link">/demo</a> をご利用ください。
      </div>

      <section class="mb-8">
        <OperationPanel mode="demo" initialPresetId="demo_hub_overview" />
      </section>

      <div class="nav-footer">
        <a href="/demo" class="link">← demo preview</a>
        {" · "}
        <a href="/admin/runtime" class="link">admin runtime 検証</a>
        {" · "}
        <a href="/demo-static" class="link">デモ（静的）</a>
        {" · "}
        <a href="/admin" class="link">admin</a>
      </div>
    </main>
  );
}
