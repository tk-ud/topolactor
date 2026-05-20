import { JSX } from "preact";
import AuthPanel from "../islands/AuthPanel.tsx";

/**
 * /auth — demo login scaffold.
 *
 * Shows a login form for the public demo and admin demo paths.
 * Authentication is handled by /api/auth/login (proxied to backend when configured).
 * Demo scaffold only — not for production use.
 */
export default function AuthPage(): JSX.Element {
  return (
    <main style={{ fontFamily: "sans-serif", padding: "24px", maxWidth: "480px" }}>
      <h1>topolactor — demo login</h1>

      <div style={{
        background: "#fffbe6",
        border: "1px solid #e6c700",
        borderRadius: "4px",
        padding: "12px 16px",
        marginBottom: "24px",
        fontSize: "0.9em",
      }}>
        <strong>Demo scaffold only.</strong> Not for production use.
        Credentials are stored as bcrypt hashes in{" "}
        <code>function_parameters</code> (<code>demo_auth / demo_users</code>).
        JWT config from <code>DEMO_JWT_SECRET</code> / <code>DEMO_JWT_ISSUER</code> env vars.
        Backend auth requires <code>DEMO_BACKEND_URL</code>.
      </div>

      <AuthPanel />

      <hr style={{ margin: "24px 0" }} />
      <p style={{ color: "#888", fontSize: "0.85em" }}>
        See <a href="/">index</a> ·{" "}
        <a href="/admin">admin</a> ·{" "}
        <a href="/demo">demo</a>
      </p>
    </main>
  );
}
