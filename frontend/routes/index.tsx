import { JSX } from "preact";
import UserDemoStepper from "../islands/UserDemoStepper.tsx";

/** Top page: production application projection entry. */
export default function Index(): JSX.Element {
  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">topolactor</h1>

      <section class="mb-8">
        <UserDemoStepper />
      </section>

      <p class="nav-footer">
        <a href="/auth" class="link">ログイン</a>
        {" · "}
        <a href="/admin" class="link">管理</a>
        {" · "}
        <a href="/runtime-status" class="link">接続状態</a>
      </p>
    </main>
  );
}
