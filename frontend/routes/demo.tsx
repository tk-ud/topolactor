import { JSX } from "preact";
import OperationPanel from "../islands/OperationPanel.tsx";

export default function Demo(): JSX.Element {
  return (
    <main style={{ fontFamily: "sans-serif", padding: "24px", maxWidth: "900px" }}>
      <h1>topolactor — runtime demo</h1>

      <p>
        This page is dedicated to <strong>backend runtime emission observation</strong>.
        Submit operations here to execute the canonical runtime route through
        <code> /api/dispatch </code> and inspect returned emission/recommendation
        outputs directly.
      </p>

      <div style={{
        background: "#eef7ff",
        border: "1px solid #6ba7d6",
        borderRadius: "4px",
        padding: "12px 16px",
        marginBottom: "24px",
      }}>
        <strong>Runtime-only:</strong> no frontend synthetic emission, no scaffold-only
        token display, and no fallback projection path is used on this route.
        Login at <a href="/login">/login</a> first, then dispatch with default demo
        operation values below.
      </div>

      <OperationPanel
        initialOperation={{
          operationType: "Search",
          target: "demo",
          layer: "hub",
          action: "overview",
        }}
      />

      <hr style={{ marginTop: "32px" }} />
      <p style={{ color: "#888", fontSize: "0.85em" }}>
        Walkthrough: <code>docs/demo-walkthrough.md</code>. See <a href="/admin">admin</a>
        for topology inspection and <a href="/runtime-status">runtime-status</a> for
        pipeline validation.
      </p>
    </main>
  );
}
