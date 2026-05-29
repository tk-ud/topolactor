import ReplyPanel from "../islands/ReplyPanel.tsx";

/**
 * デモ記事ページ — コンポーネント認証ガードのデモ。
 */
export default function DemoArticle() {
  return (
    <main class="page-main max-w-2xl font-serif">
      <nav class="mb-6 font-mono text-sm">
        <a href="/" class="link">&larr; トップ</a>
        {" | "}
        <a href="/auth" class="link">ログイン (/auth)</a>
      </nav>

      <article>
        <h1 class="mb-2 text-3xl font-bold">デモ記事 — コンポーネント認証ガード</h1>
        <p class="mb-6 font-mono text-sm text-gray-600">
          記事本文は未認証ユーザーにも表示されます。
          返信フォームのみコンポーネントレベルの auth ガードで制御されます。
        </p>

        <section class="mb-8 leading-relaxed text-gray-800">
          <p class="mb-4">
            topolactor はトポロジーベースのデータドリブン runtime フレームワークです。
            manifest、registry、schema の三層が連動して projection と dispatch を制御します。
          </p>
          <p class="mb-2">このデモページは認証ガードの二つのパターンを示します:</p>
          <ul class="list-inside list-disc space-y-1">
            <li><strong>全面 auth ガード</strong>: 認証が必要なページ全体を /auth にリダイレクト</li>
            <li><strong>コンポーネント単位 auth ガード (ReplyPanel)</strong>: 本文は公開、返信フォームのみ認証必須</li>
          </ul>
        </section>

        <hr class="mb-6 border-gray-200" />

        <section>
          <h2 class="mb-2 font-mono text-base font-semibold">返信</h2>
          <p class="mb-4 font-mono text-sm text-gray-600">
            ↓ ReplyPanel island — sessionStorage の demo_jwt_token 有無で認証状態を判定
          </p>
          <ReplyPanel />
        </section>
      </article>
    </main>
  );
}
