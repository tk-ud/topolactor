import { JSX } from "preact";
import {
  UX_IMPORT_SETTINGS,
  UX_RUNTIME_CHECK,
  UX_UI_BUILDER,
} from "../content/adminUxTerms.ts";

/**
 * トップページ — プロジェクト入口（概要・導線のみ）。
 * 管理作業は /admin、動作確認は /admin/runtime。
 */
export default function Index(): JSX.Element {
  return (
    <main class="page-main max-w-3xl font-sans">
      <h1 class="page-title">topolactor</h1>

      <p class="mb-4 leading-relaxed text-gray-700">
        topolactor は、<strong>{UX_IMPORT_SETTINGS}を作り</strong>、CSV/JSON を取り込み、
        画面を準備し、<strong>{UX_RUNTIME_CHECK}</strong>まで進める管理デモです。
        管理画面を使うには先に<strong>ログイン</strong>が必要です。
      </p>

      <section class="card mb-6">
        <h2 class="section-title">はじめ方（推奨順）</h2>
        <ol class="list-decimal space-y-2 pl-5 text-sm leading-relaxed">
          <li>
            <a href="/auth" class="link font-semibold">ログイン</a>
            {" "}— 管理画面を使う前に認証します
          </li>
          <li>
            <a href="/admin/manifests" class="link font-semibold">{UX_IMPORT_SETTINGS}を作る</a>
            {" "}— 何をどこへ取り込むか、どう表示するか（インポートの前提）
          </li>
          <li>
            <a href="/admin/import" class="link font-semibold">CSV/JSON を取り込む</a>
            {" "}— プレビューで内容を確認してから反映
          </li>
          <li>
            <a href="/admin/ui-builder" class="link font-semibold">{UX_UI_BUILDER}で画面を準備する</a>
            {" "}— 部品の登録とレイアウト
          </li>
          <li>
            <a href="/admin/runtime" class="link font-semibold">{UX_RUNTIME_CHECK}</a>
            {" "}— 登録した設定が期待どおり動くか確認
          </li>
        </ol>
        <p class="text-muted-xs mt-3">
          全体の流れは <a href="/admin" class="link">管理トップ（/admin）</a> にも同じ順序で表示しています。
        </p>
      </section>

      <section class="mb-6 grid gap-3 sm:grid-cols-2">
        <a href="/auth" class="card block hover:border-blue-300">
          <strong class="link">ログイン</strong>
          <p class="text-muted-xs mt-1">管理画面利用の前提</p>
        </a>
        <a href="/admin" class="card block hover:border-blue-300">
          <strong class="link">管理 — 作業の流れ</strong>
          <p class="text-muted-xs mt-1">
            {UX_IMPORT_SETTINGS} → インポート → {UX_UI_BUILDER} → {UX_RUNTIME_CHECK}
          </p>
        </a>
        <a href="/admin/manifests" class="card block hover:border-blue-300">
          <strong class="link">{UX_IMPORT_SETTINGS}</strong>
          <p class="text-muted-xs mt-1">取り込みルールと表示・実行先</p>
        </a>
        <a href="/admin/import" class="card block hover:border-blue-300">
          <strong class="link">インポート</strong>
          <p class="text-muted-xs mt-1">CSV/JSON のプレビューと反映</p>
        </a>
        <a href="/admin/ui-builder" class="card block hover:border-blue-300">
          <strong class="link">{UX_UI_BUILDER}</strong>
          <p class="text-muted-xs mt-1">画面部品とレイアウトの準備</p>
        </a>
        <a href="/admin/runtime" class="card block hover:border-blue-300">
          <strong class="link">{UX_RUNTIME_CHECK}</strong>
          <p class="text-muted-xs mt-1">登録済み設定の動作確認</p>
        </a>
        <a href="/demo" class="card block hover:border-blue-300 sm:col-span-2">
          <strong class="link">デモ（/demo）</strong>
          <p class="text-muted-xs mt-1">サンプルデータ済みの体験用画面（管理フローとは別経路）</p>
        </a>
      </section>

      <details class="mb-6 rounded border border-gray-200 bg-gray-50 p-4 text-sm">
        <summary class="cursor-pointer font-semibold text-gray-800">技術情報（開発者向け）</summary>
        <ul class="mt-3 list-disc space-y-1 pl-5 text-gray-600">
          <li>画面上の「取り込み設定」は内部では manifest（/admin/manifests）</li>
          <li>UI は projection 面、topology 正本は DB + backend runtime</li>
          <li>操作は canonical dispatch（POST /api/dispatch）経由</li>
          <li>
            仕様:{" "}
            <code class="rounded bg-gray-100 px-1 text-xs">docs/registrar-admin-ui-specification.md</code>
          </li>
          <li>
            静的構造図: <a href="/demo-static" class="link">/demo-static</a>
            {" · "}
            接続確認: <a href="/runtime-status" class="link">/runtime-status</a>
          </li>
        </ul>
      </details>

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
