import { JSX } from "preact";
import { useEffect, useState } from "preact/hooks";
import { loginDemo, authErrorText, type LoginResponse } from "../api/authApi.ts";
import { persistSessionToken, syncClientSessionToken } from "../lib/demoSession.ts";

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
    const existing = syncClientSessionToken();
    if (existing) {
      globalThis.location.replace(target);
    }
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
          <strong>ログイン成功。</strong> トークンを保存しました（ブラウザと cookie）。
          <br />
          <a href={redirectTo} class="link mt-2 inline-block font-semibold">
            → {redirectTo === "/" ? "ディスパッチパネルへ" : "元のページへ戻る"}
          </a>
          <small class="mt-2 block text-muted-xs">トークン（デモ用のみ）:</small>
          <pre class="pre-box mt-1 break-all">{state.token}</pre>
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
