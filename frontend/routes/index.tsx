import { JSX } from "preact";
import OperationPanel from "../islands/OperationPanel.tsx";

/**
 * Index route — the topolactor frontend entry point.
 *
 * OperationPanel is the runtime dispatch entrypoint on the frontend side.
 * It runs client-side, submits user operations to /api/dispatch, and
 * projects backend runtime emissions into the UI.
 */
export default function Index(): JSX.Element {
  return (
    <main style={{ fontFamily: "sans-serif", padding: "24px", maxWidth: "800px" }}>
      <h1>topolactor</h1>

      <p>
        This is the <strong>physical interaction projection space</strong> — the
        frontend layer that sends user operations through the runtime dispatch
        route and projects backend / DB-backed emissions as UI projections.
      </p>

      <p>
        Use <a href="/admin">admin</a> for topology inspection,
        <a href="/runtime-status"> runtime-status</a> for the static runtime checklist,
        and <a href="/demo-static"> demo-static</a> for fixture-based structure references
        that are intentionally isolated from runtime results.
      </p>

      <hr />

      {/* OperationPanel is a Fresh island — interactive, client-rendered */}
      <OperationPanel
        initialOperation={{
          operationType: "Search",
          target: "default",
          layer: "entity",
          action: "Search",
        }}
      />
    </main>
  );
}
