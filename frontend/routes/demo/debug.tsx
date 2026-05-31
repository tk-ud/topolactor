import { JSX } from "preact";
import OperationPanel from "../../islands/OperationPanel.tsx";

/**
 * /demo/debug — 開発者 / 検証向けデバッグ画面。
 * raw dispatch vector・emission JSON・SQL Attention 投影など内部情報を確認できる。
 * 一般ユーザー向けデモは /demo を利用。
 */
export default function DemoDebug(): JSX.Element {
  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">topolactor — デモ / デバッグ</h1>

      <div class="alert-info mb-6">
        <strong>開発者 / 検証向けページ:</strong> raw dispatch vector・emission JSON・SQL Attention
        投影など内部情報を確認できます。一般ユーザー向けデモは{" "}
        <a href="/demo" class="link">/demo</a> をご利用ください。
      </div>

      <section class="mb-8">
        <OperationPanel mode="demo" initialPresetId="demo_hub_overview" />
      </section>

      <div class="nav-footer">
        <a href="/demo" class="link">← デモ</a>
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
