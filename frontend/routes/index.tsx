import { JSX } from "preact";
import { UX_HUB_MANIFESTS, UX_UI_BUILDER } from "../content/adminUxTerms.ts";

/** Top page: canonical admin workflow entry and demo links only. */
export default function Index(): JSX.Element {
  return (
    <main class="page-main max-w-3xl font-sans">
      <h1 class="page-title">topolactor</h1>
      <p class="mb-4 leading-relaxed text-gray-700">
        管理画面では、新規 manifest 作成、{UX_UI_BUILDER}、既存 manifest の
        relation / hub 操作を順に進めます。 管理画面を使うには先に<strong>
          ログイン
        </strong>が必要です。
      </p>

      <section class="card mb-6">
        <h2 class="section-title">はじめ方（canonical admin workflow）</h2>
        <ol class="list-decimal space-y-2 pl-5 text-sm leading-relaxed">
          <li>
            <a href="/auth" class="link font-semibold">ログイン</a>{" "}
            — 管理画面を使う前に認証します
          </li>
          <li>
            <a href="/admin/contents" class="link font-semibold">
              新規 manifest 作成
            </a>{" "}
            — ドラフト作成、内容確認、プレビュー、有効化
          </li>
          <li>
            <a href="/admin/ui-builder" class="link font-semibold">
              {UX_UI_BUILDER}
            </a>{" "}
            — 部品登録とレイアウト保存反映
          </li>
          <li>
            <a href="/admin/manifests" class="link font-semibold">
              {UX_HUB_MANIFESTS}
            </a>{" "}
            — 既存 manifest の relation / hub 操作
          </li>
        </ol>
      </section>

      <section class="mb-6 grid gap-3 sm:grid-cols-2">
        <a href="/admin" class="card block hover:border-blue-300">
          <strong class="link">管理 — 作業の流れ</strong>
          <p class="text-muted-xs mt-1">正本 route registry に沿った管理導線</p>
        </a>
        <a href="/demo" class="card block hover:border-blue-300">
          <strong class="link">デモ（/demo）</strong>
          <p class="text-muted-xs mt-1">動作確認は管理導線とは別経路です</p>
        </a>
      </section>

      <p class="nav-footer">
        <a href="/runtime-status" class="link">接続状態</a>
      </p>
    </main>
  );
}
