import { ComponentChildren } from "preact";
import { JSX } from "preact";
import { useEffect, useState } from "preact/hooks";
import { probeAdminSessionToken } from "../api/authApi.ts";
import {
  DEMO_ADMIN_FINAL_AUTH_BOUNDARY_SUMMARY,
  DEMO_ADMIN_SSR_PRESENCE_GATE_SUMMARY,
  SESSION_TOKEN_KEY,
  ensureValidClientSession,
} from "../lib/demoSession.ts";

type Props = {
  children?: ComponentChildren;
};

export default function AdminAuthGate({ children }: Props): JSX.Element {
  const [authState, setAuthState] = useState<"loading" | "present" | "absent">("loading");
  const [redirectPath, setRedirectPath] = useState("/admin");

  useEffect(() => {
    setRedirectPath(globalThis.location?.pathname ?? "/admin");
    void (async () => {
      const validToken = await ensureValidClientSession(probeAdminSessionToken);
      setAuthState(validToken ? "present" : "absent");
    })();
  }, []);

  if (authState === "loading") {
    return (
      <main class="page-main max-w-md font-sans">
        <p class="text-muted">ログイン状態を確認しています...</p>
      </main>
    );
  }

  if (authState === "absent") {
    const loginUrl = `/super_auth?redirect=${encodeURIComponent(redirectPath)}`;
    return (
      <main class="page-main max-w-md font-sans">
        <h1 class="text-xl font-bold text-gray-900">ログインが必要です</h1>
        <p class="mt-2 mb-4 text-sm leading-relaxed text-gray-600">
          管理画面を使うにはログインが必要です。未ログインの場合はログイン画面へ案内されます。
        </p>
        <a href={loginUrl} class="btn-primary">
          ログインページへ
        </a>
        <details class="mt-4 text-xs text-gray-500">
          <summary class="cursor-pointer">技術情報（境界）</summary>
          <ul class="mt-2 list-disc space-y-1 pl-4">
            <li>
              クライアント: <code class="rounded bg-gray-100 px-1">sessionStorage</code> と cookie（
              <code class="rounded bg-gray-100 px-1">{SESSION_TOKEN_KEY}</code>）にデモ用トークンを保持します。
            </li>
            <li>{DEMO_ADMIN_SSR_PRESENCE_GATE_SUMMARY}</li>
            <li>{DEMO_ADMIN_FINAL_AUTH_BOUNDARY_SUMMARY}</li>
          </ul>
        </details>
      </main>
    );
  }

  return <>{children}</>;
}
