import { JSX } from "preact";
import OperationPanel from "../islands/OperationPanel.tsx";

/**
 * /demo — runtime dispatch entrypoint for the demo flow.
 *
 * This page exercises the backend canonical flow end-to-end:
 *   login → dispatch POST /api/dispatch → backend RuntimeExecutor
 *   → attractor_resolve → structure_map_resolve → emission → EmissionView
 *
 * No synthetic data, no frontend-only resolution.
 * Static structure explanation is at /demo-static.
 */
export default function Demo(): JSX.Element {
  return (
    <main style={{ fontFamily: "sans-serif", padding: "24px", maxWidth: "900px" }}>
      <h1>topolactor — runtime demo</h1>

      <div style={{
        background: "#e8f4fd",
        border: "1px solid #2196f3",
        borderRadius: "4px",
        padding: "12px 16px",
        marginBottom: "24px",
      }}>
        <strong>Runtime page:</strong> This page dispatches to the backend runtime.
        The emission shown below comes from{" "}
        <code>backend RuntimeExecutor → DB → emission</code>,
        not from frontend-only resolution.{" "}
        Login is required — use <a href="/auth">login</a> first.
        For the static structure diagram, see <a href="/demo-static">/demo-static</a>.
      </div>

      <section style={{ marginBottom: "32px" }}>
        <h2>Demo dispatch</h2>
        <p style={{ color: "#555", fontSize: "0.9em" }}>
          Pre-filled with demo defaults: <code>target=demo</code>,{" "}
          <code>layer=hub</code>, <code>action=overview</code>.
          To observe recommendation results, expand{" "}
          <em>Context fields</em> and enter:
        </p>
        <ul style={{ fontSize: "0.9em", color: "#555" }}>
          <li>
            Context Session ID:{" "}
            <code>00000000-0000-0000-0000-000000000031</code>
          </li>
          <li>
            Context Token IDs:{" "}
            <code>00000000-0000-0000-0000-000000000021</code>
          </li>
        </ul>
        <p style={{ color: "#555", fontSize: "0.9em" }}>
          See <code>docs/demo-walkthrough.md</code> Scenario E for the full walkthrough.
        </p>

        <OperationPanel
          initialOperation={{
            operationType: "Search",
            target: "demo",
            layer: "hub",
            action: "overview",
          }}
        />
      </section>

      <hr style={{ marginTop: "32px" }} />
      <p style={{ color: "#888", fontSize: "0.85em" }}>
        See <a href="/demo-static">demo-static</a> for the static structure diagram,{" "}
        <a href="/admin">admin</a> for topology inspection,{" "}
        <a href="/runtime-status">runtime-status</a> for pipeline step validation,{" "}
        <a href="/">index</a> for the dispatch panel.
      </p>
    </main>
  );
}
