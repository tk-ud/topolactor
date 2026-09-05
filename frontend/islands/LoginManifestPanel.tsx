import { JSX } from "preact";
import { useEffect, useState } from "preact/hooks";
import {
  authErrorText,
  fetchUserLoginManifest,
  loginProjection,
  probeSessionToken,
  registerUser,
  type LoginManifestResponse,
  type LoginResponse,
} from "../api/authApi.ts";
import { ensureValidClientSession, persistSessionToken } from "../lib/demoSession.ts";

type AuthState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "success"; token: string }
  | { status: "registered"; username: string; statusText?: string; approve?: boolean }
  | { status: "error"; errors: LoginResponse["errors"] };

export default function LoginManifestPanel(): JSX.Element {
  const [manifest, setManifest] = useState<LoginManifestResponse | null>(null);
  const [manifestError, setManifestError] = useState<string | null>(null);
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [registerUsername, setRegisterUsername] = useState("");
  const [registerPassword, setRegisterPassword] = useState("");
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
      const m = await fetchUserLoginManifest();
      if (!m.success) {
        setManifestError(
          m.errors?.map(authErrorText).join("; ") ?? "Login manifest could not be loaded.",
        );
      } else {
        setManifest(m);
        const successRedirect = m.uiProjection?.redirect_success;
        if (successRedirect && successRedirect.startsWith("/")) {
          setRedirectTo(successRedirect);
        }
      }

      // Probe accepts any valid JWT — projection surface is not user-realm-only.
      const validToken = await ensureValidClientSession((t) =>
        probeSessionToken(t)
      );
      setSessionCheckDone(true);
      if (validToken) {
        globalThis.location.replace(target);
      }
    })();
  }, []);

  const labels = manifest?.uiProjection?.labels ?? {};
  const submitLabel = labels.submit ?? "ログイン";

  async function handleRegister(e: JSX.TargetedEvent<HTMLFormElement, Event>) {
    e.preventDefault();
    setState({ status: "loading" });
    const result = await registerUser({ username: registerUsername, password: registerPassword });
    if (result.success) {
      setState({
        status: "registered",
        username: result.username ?? registerUsername,
        statusText: result.status,
        approve: result.approve,
      });
      setRegisterPassword("");
    } else {
      setState({ status: "error", errors: result.errors });
    }
  }

  async function handleSubmit(e: JSX.TargetedEvent<HTMLFormElement, Event>) {
    e.preventDefault();
    const binding = manifest?.authActionBinding;
    const runtimeDest = binding?.runtime_destination ?? binding?.runtimeDestination;
    if (runtimeDest !== "auth_runtime" || binding?.action !== "login") {
      setState({
        status: "error",
        errors: [{ message: "Login manifest auth_action_binding is invalid." }],
      });
      return;
    }
    setState({ status: "loading" });
    const result = await loginProjection({ username, password });
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

  if (manifestError) {
    return (
      <div class="alert-error">
        <strong>ログイン画面を読み込めませんでした。時間をおいて再度お試しください。</strong>
        <details class="mt-2 text-xs">
          <summary class="cursor-pointer text-red-800">技術情報（開発者向け）</summary>
          <p class="mt-1 text-red-900">{manifestError}</p>
        </details>
      </div>
    );
  }

  return (
    <section class="space-y-6">
      <form onSubmit={handleSubmit} class="flex max-w-xs flex-col gap-3">
        <label class="text-sm font-medium text-gray-700">
          {labels.username ?? "ユーザー名"}
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
          {labels.password ?? "パスワード"}
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
          {state.status === "loading" ? "ログイン中..." : submitLabel}
        </button>
      </form>

      <section id="register" class="rounded-lg border border-blue-100 bg-blue-50 p-4">
        <h2 class="text-base font-semibold text-gray-900">新規登録（通常ユーザー）</h2>
        <p class="mt-1 text-sm text-gray-600">
          登録直後のアカウントは承認待ちです。管理者が承認するまでログインできません。
        </p>
        <details class="mt-1 text-xs text-gray-500">
          <summary class="cursor-pointer">技術情報（開発者向け）</summary>
          <p class="mt-1">
            承認待ちアカウントのログインは <code class="rounded bg-white px-1">AUTH_USER_NOT_APPROVED</code>{" "}
            で明示的に拒否されます。
          </p>
        </details>
        <form onSubmit={handleRegister} class="mt-3 flex max-w-xs flex-col gap-3">
          <label class="text-sm font-medium text-gray-700">
            ユーザー名
            <input
              type="text"
              name="register-username"
              value={registerUsername}
              onInput={(e) => setRegisterUsername((e.target as HTMLInputElement).value)}
              required
              class="input mt-1"
              autoComplete="username"
            />
          </label>
          <label class="text-sm font-medium text-gray-700">
            パスワード
            <input
              type="password"
              name="register-password"
              value={registerPassword}
              onInput={(e) => setRegisterPassword((e.target as HTMLInputElement).value)}
              required
              class="input mt-1"
              autoComplete="new-password"
            />
          </label>
          <button type="submit" disabled={state.status === "loading"} class="btn-secondary">
            {state.status === "loading" ? "登録中..." : "承認待ちで登録"}
          </button>
        </form>
      </section>

      {state.status === "success" && (
        <div class="alert-success mt-4">
          <strong>ログイン成功。</strong>
          <br />
          <a href={redirectTo} class="link mt-2 inline-block font-semibold">
            → 続ける
          </a>
        </div>
      )}

      {state.status === "registered" && (
        <div class="alert-success mt-4">
          <strong>登録を受け付けました。</strong>
          <p class="mt-1 text-sm">
            {state.username} さんの登録は承認待ちです。管理者の承認後にログインできます。
          </p>
          <details class="mt-2 text-xs">
            <summary class="cursor-pointer text-emerald-800">技術情報（開発者向け）</summary>
            <p class="mt-1 text-emerald-900">
              realm=user, approve={String(state.approve)}, status={state.statusText ?? "active"}
            </p>
          </details>
        </div>
      )}

      {state.status === "error" && (
        <div class="alert-error mt-4">
          <strong>ログインに失敗しました。</strong>
          <ul class="mt-2 list-inside list-disc text-sm">
            {(state.errors ?? [{ message: "不明なエラー。" }]).map((e, i) => (
              <li key={i}>{e.message ?? e.Message ?? "不明なエラー。"}</li>
            ))}
          </ul>
          <details class="mt-2 text-xs">
            <summary class="cursor-pointer text-red-800">技術情報（開発者向け）</summary>
            <ul class="mt-1 list-inside list-disc text-red-900">
              {(state.errors ?? []).map((e, i) => <li key={i}>{authErrorText(e)}</li>)}
            </ul>
          </details>
        </div>
      )}

      <details class="text-muted-xs mt-4">
        <summary class="cursor-pointer">技術情報</summary>
        <p class="mt-1">
          Submit は <code>auth_runtime.login</code>（<code>/api/auth/login</code>）へ委譲。認証情報は
          manifest に含まれません。
        </p>
      </details>
    </section>
  );
}
