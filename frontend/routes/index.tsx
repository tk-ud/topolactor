import { JSX } from "preact";

/**
 * トップページ — プロジェクト入口（概要・導線のみ）。
 * ランタイム dispatch は /admin/runtime（登録は /admin/* の各画面）。
 */
export default function Index(): JSX.Element {
  return (
    <main class="page-main max-w-3xl font-sans">
      <h1 class="page-title">topolactor</h1>

      <p class="mb-4 leading-relaxed text-gray-700">
        データ駆動トポロジー runtime のデモ用スキャフォールドです。
        UI は<strong>投影面</strong>、正本は<strong>DB 上の promoted identity</strong>、
        操作は<strong>canonical dispatch</strong> 経由で backend が解決します。
      </p>

      <section class="card mb-6">
        <h2 class="section-title">はじめ方</h2>
        <ol class="list-decimal space-y-2 pl-5 text-sm leading-relaxed">
          <li>
            <a href="/auth" class="link font-semibold">ログイン</a>
            {" "}— JWT を取得（DB 接続・dispatch に必要）
          </li>
          <li>
            <a href="/admin" class="link font-semibold">管理（/admin）</a>
            {" "}— トポロジー登録（import / seed / UI builder 等）。direct DB エディタではありません
          </li>
          <li>
            登録後、<a href="/admin/runtime" class="link font-semibold">ランタイム検証（/admin/runtime）</a>
            {" "}— promoted 状態が dispatch で期待どおり動くか確認
          </li>
          <li>
            <a href="/demo" class="link font-semibold">デモ（/demo）</a>
            {" "}— seed 済み demo トポロジ専用の preset dispatch
          </li>
        </ol>
      </section>

      <section class="mb-6 grid gap-3 sm:grid-cols-2">
        <a href="/admin" class="card block hover:border-blue-300">
          <strong class="link">管理 — 登録境界</strong>
          <p class="text-muted-xs mt-1">CSV/JSON・seed・UI topology・token 辞書</p>
        </a>
        <a href="/admin/runtime" class="card block hover:border-blue-300">
          <strong class="link">管理 — ランタイム検証</strong>
          <p class="text-muted-xs mt-1">operationType=user の dispatch・emission 投影</p>
        </a>
        <a href="/demo" class="card block hover:border-blue-300">
          <strong class="link">Demo runtime</strong>
          <p class="text-muted-xs mt-1">demo hub / recommendation 等の preset</p>
        </a>
        <a href="/runtime-status" class="card block hover:border-blue-300">
          <strong class="link">Runtime status</strong>
          <p class="text-muted-xs mt-1">パイプライン・環境の接続確認</p>
        </a>
      </section>

      <p class="text-muted text-sm">
        静的構造図: <a href="/demo-static" class="link">/demo-static</a>
        {" · "}
        仕様: <code class="rounded bg-gray-100 px-1 text-xs">docs/registrar-admin-ui-specification.md</code>
      </p>

      <div class="nav-footer">
        <a href="/auth" class="link">ログイン</a>
        {" · "}
        <a href="/admin" class="link">管理</a>
        {" · "}
        <a href="/demo" class="link">デモ</a>
      </div>
    </main>
  );
}
