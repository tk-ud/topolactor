import { ComponentChildren, JSX } from "preact";
import { useEffect, useState } from "preact/hooks";
import { useCurrentSession } from "../hooks/useCurrentSession.ts";

type Props = {
  children?: ComponentChildren;
};

/**
 * Client-side gate for authenticated (not admin-only) route subtrees. Any valid role passes —
 * this only decides page-level visibility; per-operation mutation authority is always re-checked
 * backend-side (see useCurrentSession.ts doc comment on the display-only role boundary).
 */
export default function AuthenticatedGate({ children }: Props): JSX.Element {
  const session = useCurrentSession();
  const [redirectPath, setRedirectPath] = useState("/dashboard");

  useEffect(() => {
    setRedirectPath(globalThis.location?.pathname ?? "/dashboard");
  }, []);

  if (session.status === "loading") {
    return (
      <main class="page-main max-w-md font-sans">
        <p class="text-muted">ログイン状態を確認しています...</p>
      </main>
    );
  }

  if (session.status === "absent") {
    const loginUrl = `/auth?redirect=${encodeURIComponent(redirectPath)}`;
    return (
      <main class="page-main max-w-md font-sans">
        <h1 class="text-xl font-bold text-gray-900">ログインが必要です</h1>
        <p class="mt-2 mb-4 text-sm leading-relaxed text-gray-600">
          このページを使うにはログインが必要です。未ログインの場合はログイン画面へ案内されます。
        </p>
        <a href={loginUrl} class="btn-primary">
          ログインページへ
        </a>
      </main>
    );
  }

  return <>{children}</>;
}
