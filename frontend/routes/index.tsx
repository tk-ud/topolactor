import { JSX } from "preact";
import {
  UX_CONTENTS,
  UX_HUB_MANIFESTS,
  UX_UI_BUILDER,
} from "../content/adminUxTerms.ts";

/** Top page: user-facing admin workflow entry and demo links only. */
export default function Index(): JSX.Element {
  return (
    <main class="page-main max-w-3xl font-sans">
      <h1 class="page-title">topolactor</h1>
      <p class="mb-4 leading-relaxed text-gray-700">
        管理画面では、{UX_CONTENTS}、{UX_UI_BUILDER}、{UX_HUB_MANIFESTS}を順に進めます。
        管理画面を使うには先に<strong>管理ログイン</strong>（
        <a href="/super_auth" class="link">/super_auth</a>）が必要です。
      </p>

      <section class="card mb-6">
        <h2 class="section-title">はじめ方</h2>
        <ol class="list-decimal space-y-2 pl-5 text-sm leading-relaxed">
          <li>
            <a href="/super_auth" class="link font-semibold">管理ログイン</a>{" "}
            — 管理画面を使う前に認証します（admin realm）
          </li>
          <li>
            <a href="/admin/contents" class="link font-semibold">
              {UX_CONTENTS}
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
            — 作成済みページの所属先、ページ間のつながり、表示順を管理
          </li>
        </ol>
      </section>

      <section class="mb-6 grid gap-3 sm:grid-cols-2">
        <a href="/admin" class="card block hover:border-blue-300">
          <strong class="link">管理 — 作業の流れ</strong>
          <p class="text-muted-xs mt-1">ページ設定を順番に進める管理画面</p>
        </a>
        <a href="/demo" class="card block hover:border-blue-300">
          <strong class="link">デモ（/demo）</strong>
          <p class="text-muted-xs mt-1">動作確認は管理導線とは別経路です</p>
        </a>
      </section>

      <p class="nav-footer">
        <a href="/auth" class="link">ユーザログイン</a>
        {" · "}
        <a href="/super_auth" class="link">管理ログイン</a>
        {" · "}
        <a href="/runtime-status" class="link">接続状態</a>
      </p>
    </main>
  );
}
