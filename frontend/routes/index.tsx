import { JSX } from "preact";
import OperationPanel from "../islands/OperationPanel.tsx";
import { defaultStructureMap } from "../structure_map.ts";

/**
 * Index route — the topolactor frontend entry point.
 *
 * This page demonstrates the canonical pipeline skeleton:
 *   stored_topology_data → user_operation → operation_vector → attractor_resolve
 *   → structure_map_resolve → package_resolve → schema_resolve
 *   → component_expand → emission_or_projection
 *
 * OperationPanel is a Fresh island: it runs client-side and handles the
 * interactive portion of the flow (user_operation → dispatch → emission).
 */
export default function Index(): JSX.Element {
  const mapEntryCount = Object.keys(defaultStructureMap).length;

  return (
    <main style={{ fontFamily: "sans-serif", padding: "24px", maxWidth: "800px" }}>
      <h1>topolactor</h1>

      <p>
        This is the <strong>physical interaction projection space</strong> — the
        frontend layer that converts user operations into operation vectors,
        resolves them through the structure_map → package → schema → component
        pipeline, and renders backend emissions as UI projections.
      </p>

      <p>
        The default structure map contains{" "}
        <strong>{mapEntryCount} entr{mapEntryCount === 1 ? "y" : "ies"}</strong>.
        See <a href="/admin">admin</a> for topology inspection or{" "}
        <a href="/runtime-status">runtime-status</a> for pipeline step validation.
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
