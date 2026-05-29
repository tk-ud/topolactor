import { JSX } from "preact";
import OperationPanel from "../islands/OperationPanel.tsx";

/**
 * /demo — demo topology 向け runtime dispatch（preset 特化）。
 */
export default function Demo(): JSX.Element {
  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">topolactor — Demo runtime</h1>

      <div class="alert-info mb-6">
        <strong>Demo ランタイムページ:</strong> seed 済み demo トポロジへの dispatch 専用です。
        emission は <code class="rounded bg-blue-100 px-1">RuntimeExecutor → DB</code> の実レスポンスです（合成表示なし）。
        先に <a href="/auth" class="link">ログイン</a>。
        汎用シナリオ（default search 等）は <a href="/admin/runtime" class="link">/admin/runtime</a> を利用してください。
        静的構造図のみは <a href="/demo-static" class="link">/demo-static</a>。
      </div>

      <section class="mb-8">
        <h2 class="section-title">Demo シナリオ</h2>
        <p class="mb-4 text-muted">
          カードを選んで実行してください。レコメンド確認は
          <strong> Demo hub + recommendation context</strong> preset が
          <code class="rounded bg-gray-100 px-1">docs/demo-walkthrough.md</code> シナリオ E と同じ seed
          session/token を自動付与します。UUID の手入力は Advanced にあります。
        </p>

        <OperationPanel mode="demo" initialPresetId="demo_hub_overview" />
      </section>

      <div class="nav-footer">
        <a href="/" class="link">トップ</a>
        {" · "}
        <a href="/admin/runtime" class="link">ランタイム検証</a>
        {" · "}
        <a href="/demo-static" class="link">デモ（静的）</a>
        {" · "}
        <a href="/admin" class="link">管理画面</a>
        {" · "}
        <a href="/runtime-status" class="link">ランタイムステータス</a>
      </div>
    </main>
  );
}
