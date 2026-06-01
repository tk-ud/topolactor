import { JSX } from "preact";
import { useEffect, useState } from "preact/hooks";
import { loginDemo, authErrorText, probeDemoSessionToken, type LoginResponse } from "../api/authApi.ts";
import {
  DEMO_ADMIN_FINAL_AUTH_BOUNDARY_SUMMARY,
  ensureValidClientSession,
  persistSessionToken,
} from "../lib/demoSession.ts";

type AuthState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "success"; token: string }
  | { status: "error"; errors: LoginResponse["errors"] };

export default function AuthPanel(): JSX.Element {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [redirectTo, setRedirectTo] = useState("/");
  const [state, setState] = useState<AuthState>({ status: "idle" });
  const [sessionCheckDone, setSessionCheckDone] = useState(false);

  useEffect(() => {
    const params = new URLSearchParams(globalThis.location?.search ?? "");
    const redirect = params.get("redirect");
    const target =
      redirect && redirect.startsWith("/") && !redirect.startsWith("//")
        ? redirect
        : "/";
    if (redirect && redirect.startsWith("/") && !redirect.startsWith("//")) {
      setRedirectTo(redirect);
    }
    void (async () => {
      const validToken = await ensureValidClientSession(probeDemoSessionToken);
      setSessionCheckDone(true);
      if (validToken) {
        globalThis.location.replace(target);
      }
    })();
  }, []);

  async function handleSubmit(e: JSX.TargetedEvent<HTMLFormElement, Event>) {
    e.preventDefault();
    setState({ status: "loading" });
    const result = await loginDemo({ username, password });
    if (result.success && result.token) {
      persistSessionToken(result.token);
      setState({ status: "success", token: result.token });
    } else {
      setState({ status: "error", errors: result.errors });
    }
  }

  if (!sessionCheckDone) {
    return (
      <section>
        <p class="text-muted text-sm">ログイン状態を確認しています...</p>
      </section>
    );
  }

  return (
    <section>
      <form onSubmit={handleSubmit} class="flex max-w-xs flex-col gap-3">
        <label class="text-sm font-medium text-gray-700">
          ユーザー名
          <input
            type="text"
            name="username"
            value={username}
            onInput={(e) => setUsername((e.target as HTMLInputElement).value)}
            required
            class="input mt-1"
            autoComplete="username"
          />
        </label>
        <label class="text-sm font-medium text-gray-700">
          パスワード
          <input
            type="password"
            name="password"
            value={password}
            onInput={(e) => setPassword((e.target as HTMLInputElement).value)}
            required
            class="input mt-1"
            autoComplete="current-password"
          />
        </label>
        <button type="submit" disabled={state.status === "loading"} class="btn-primary">
          {state.status === "loading" ? "ログイン中..." : "ログイン"}
        </button>
      </form>

      {state.status === "success" && (
        <div class="alert-success mt-4">
          <strong>ログイン成功。</strong> デモ用トークンをブラウザと cookie に保存しました。
          <br />
          <a href={redirectTo} class="link mt-2 inline-block font-semibold">
            → {redirectTo === "/" ? "ディスパッチパネルへ" : "元のページへ戻る"}
          </a>
          <p class="text-muted-xs mt-2">{DEMO_ADMIN_FINAL_AUTH_BOUNDARY_SUMMARY}</p>
          <details class="text-muted-xs mt-1">
            <summary class="cursor-pointer">技術情報</summary>
            <small class="mt-1 block">トークン（デモ用のみ）:</small>
            <pre class="pre-box mt-1 break-all">{state.token}</pre>
          </details>
        </div>
      )}

      {state.status === "error" && (
        <div class="alert-error mt-4">
          <strong>ログインに失敗しました。</strong>
          <ul class="mt-2 list-inside list-disc text-sm">
            {(state.errors ?? [{ message: "不明なエラー。" }]).map((e, i) => (
              <li key={i}>{authErrorText(e)}</li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
