import { JSX } from "preact";
import { useState } from "preact/hooks";
import { loginDemo, authErrorText, type LoginResponse } from "../api/authApi.ts";

const SESSION_TOKEN_KEY = "demo_jwt_token";

type LoginState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "success"; token: string }
  | { status: "error"; errors: LoginResponse["errors"] };

/**
 * Demo login form island.
 * Sends credentials to /api/auth/login, stores the JWT token in sessionStorage
 * under demo_jwt_token, and displays the result.
 * Not for production use — demo scaffold only.
 */
export default function LoginPanel(): JSX.Element {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [state, setState] = useState<LoginState>({ status: "idle" });

  async function handleSubmit(e: JSX.TargetedEvent<HTMLFormElement, Event>) {
    e.preventDefault();
    setState({ status: "loading" });
    const result = await loginDemo({ username, password });
    if (result.success && result.token) {
      sessionStorage.setItem(SESSION_TOKEN_KEY, result.token);
      setState({ status: "success", token: result.token });
    } else {
      setState({ status: "error", errors: result.errors });
    }
  }

  return (
    <section>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "12px", maxWidth: "320px" }}>
        <label>
          Username
          <br />
          <input
            type="text"
            name="username"
            value={username}
            onInput={(e) => setUsername((e.target as HTMLInputElement).value)}
            required
            style={{ width: "100%", padding: "6px", marginTop: "4px" }}
            autoComplete="username"
          />
        </label>
        <label>
          Password
          <br />
          <input
            type="password"
            name="password"
            value={password}
            onInput={(e) => setPassword((e.target as HTMLInputElement).value)}
            required
            style={{ width: "100%", padding: "6px", marginTop: "4px" }}
            autoComplete="current-password"
          />
        </label>
        <button type="submit" disabled={state.status === "loading"}>
          {state.status === "loading" ? "Logging in…" : "Login"}
        </button>
      </form>

      {state.status === "success" && (
        <div style={{ marginTop: "16px", padding: "12px", background: "#e6f9e6", border: "1px solid #4caf50", borderRadius: "4px" }}>
          <strong>Login successful.</strong> Token saved to sessionStorage.
          <br />
          <a href="/" style={{ display: "inline-block", marginTop: "8px", fontWeight: "bold" }}>
            → Go to dispatch panel
          </a>
          <br />
          <small style={{ color: "#555", display: "block", marginTop: "8px" }}>Token (demo only):</small>
          <pre style={{ wordBreak: "break-all", fontSize: "0.8em", marginTop: "4px" }}>{state.token}</pre>
        </div>
      )}

      {state.status === "error" && (
        <div style={{ marginTop: "16px", padding: "12px", background: "#fdecea", border: "1px solid #f44336", borderRadius: "4px" }}>
          <strong>Login failed.</strong>
          <ul style={{ margin: "8px 0 0 0", paddingLeft: "20px" }}>
            {(state.errors ?? [{ message: "Unknown error." }]).map((e, i) => (
              <li key={i}>{authErrorText(e)}</li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
