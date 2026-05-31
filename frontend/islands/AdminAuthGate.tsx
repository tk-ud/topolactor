import { ComponentChildren } from "preact";
import { JSX } from "preact";
import { useEffect, useState } from "preact/hooks";
import { SESSION_TOKEN_KEY, syncClientSessionToken } from "../lib/demoSession.ts";

type Props = {
  children?: ComponentChildren;
};

export default function AdminAuthGate({ children }: Props): JSX.Element {
  const [authState, setAuthState] = useState<"loading" | "authed" | "unauthed">("loading");
  const [redirectPath, setRedirectPath] = useState("/admin");

  useEffect(() => {
    setRedirectPath(globalThis.location?.pathname ?? "/admin");
    const token = syncClientSessionToken();
    setAuthState(token ? "authed" : "unauthed");
  }, []);

  if (authState === "loading") {
    return (
      <main class="page-main max-w-md font-sans">
        <p class="text-muted">ログイン状態を確認しています...</p>
      </main>
    );
  }

  if (authState === "unauthed") {
    const loginUrl = `/auth?redirect=${encodeURIComponent(redirectPath)}`;
    return (
      <main class="page-main max-w-md font-sans">
        <h1 class="text-xl font-bold text-gray-900">ログインが必要です</h1>
        <p class="mt-2 mb-4 text-sm leading-relaxed text-gray-600">
          管理画面を使うにはログインが必要です。未ログインの場合は自動的にログイン画面へ案内されます。
        </p>
        <a href={loginUrl} class="btn-primary">
          ログインページへ
        </a>
        <details class="mt-4 text-xs text-gray-500">
          <summary class="cursor-pointer">技術情報</summary>
          <p class="mt-2">
            認証トークンは <code class="rounded bg-gray-100 px-1">sessionStorage</code> と cookie（
            <code class="rounded bg-gray-100 px-1">{SESSION_TOKEN_KEY}</code>）に保存されます。
            /admin へのアクセスはサーバー middleware でも cookie を検証します。
          </p>
        </details>
      </main>
    );
  }

  return <>{children}</>;
}
