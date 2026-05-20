import ReplyPanel from "../islands/ReplyPanel.tsx";

/**
 * Demo article page.
 *
 * Demonstrates two auth guard patterns:
 * 1. Full-page auth guard: /auth redirects the entire page for authenticated-only routes
 * 2. Component-level auth guard: article body is public; reply panel (ReplyPanel island)
 *    shows "login to reply" for unauthenticated users and the reply form for authenticated users.
 *
 * Auth state is determined by demo_jwt_token presence in sessionStorage (island-side).
 */
export default function DemoArticle() {
  return (
    <main style={{ fontFamily: "serif", maxWidth: "720px", margin: "0 auto", padding: "2rem" }}>
      <nav style={{ fontFamily: "monospace", marginBottom: "1.5rem" }}>
        <a href="/">&larr; top</a>
        {" | "}
        <a href="/auth">ログイン (/auth)</a>
      </nav>

      <article>
        <h1 style={{ fontSize: "1.8rem", marginBottom: "0.5rem" }}>
          demo article — コンポーネント認証ガードのデモ
        </h1>
        <p style={{ color: "#666", fontFamily: "monospace", fontSize: "0.85rem", marginBottom: "1.5rem" }}>
          この記事本文は未認証ユーザーにも表示されます（全面 auth ガードなし）。
          <br />
          返信フォームはコンポーネントレベルの auth ガードで制御されています。
        </p>

        <section style={{ lineHeight: 1.7, marginBottom: "2rem" }}>
          <p>
            topolactor は topology ベースのデータドリブン runtime フレームワークです。
            manifest、registry、schema の三層が連動して、
            projection と dispatch を制御します。
          </p>
          <p>
            このデモページは、認証ガードの二つのパターンを示します:
          </p>
          <ul>
            <li>
              <strong>全面 auth ガード (/auth ページ)</strong>: 認証が必要なページ全体を /auth にリダイレクト
            </li>
            <li>
              <strong>コンポーネント単位 auth ガード (ReplyPanel)</strong>:
              記事本文は誰でも閲覧可能、返信フォームだけを認証済みユーザーに限定
            </li>
          </ul>
        </section>

        <hr style={{ borderColor: "#eee", marginBottom: "1.5rem" }} />

        <section>
          <h2 style={{ fontFamily: "monospace", fontSize: "1rem" }}>返信</h2>
          <p style={{ color: "#555", fontSize: "0.9rem", fontFamily: "monospace", marginBottom: "1rem" }}>
            ↓ ReplyPanel island — sessionStorage の demo_jwt_token 有無で認証状態を判定
          </p>
          <ReplyPanel />
        </section>
      </article>
    </main>
  );
}
