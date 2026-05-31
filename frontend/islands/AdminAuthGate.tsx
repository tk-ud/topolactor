import { ComponentChildren } from "preact";
import { JSX } from "preact";
import { useEffect, useState } from "preact/hooks";

const SESSION_TOKEN_KEY = "demo_jwt_token";

type Props = {
  children?: ComponentChildren;
};

export default function AdminAuthGate({ children }: Props): JSX.Element {
  const [authState, setAuthState] = useState<"loading" | "authed" | "unauthed">("loading");
  const [redirectPath, setRedirectPath] = useState("/admin");

  useEffect(() => {
    setRedirectPath(globalThis.location?.pathname ?? "/admin");
    const token = sessionStorage.getItem(SESSION_TOKEN_KEY);
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
          管理画面（マニフェスト・インポート・UI Builder・動作確認）を使うには、
          先にデモ認証でログインしてください。ログイン後、元のページへ戻ります。
        </p>
        <a href={loginUrl} class="btn-primary">
          ログインページへ
        </a>
        <details class="mt-4 text-xs text-gray-500">
          <summary class="cursor-pointer">技術情報</summary>
          <p class="mt-2">
            認証トークンは <code class="rounded bg-gray-100 px-1">sessionStorage</code> の{" "}
            <code class="rounded bg-gray-100 px-1">{SESSION_TOKEN_KEY}</code> に保存されます（JWT）。
          </p>
        </details>
      </main>
    );
  }

  return <>{children}</>;
}
