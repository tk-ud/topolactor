import { JSX } from "preact";
import { resolveOperationVector } from "../runtime/resolveOperationVector.ts";
import { lookupStructureMap, defaultStructureMap } from "../structure_map.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { ProjectionView } from "../components/ProjectionView.tsx";
import { ContextTokenBadgeList, type ContextToken } from "../components/ContextTokenBadgeList.tsx";
import { RecommendationPanel } from "../components/RecommendationPanel.tsx";
import type { Emission } from "../api/dispatch.ts";

// Seed reference: mirrors db/demo_seed.sql rows.
// NOT runtime-resolved — changing these values in the DB does not change this display.
// Live token state requires the dispatch API + recommendation resolver.
const demoTokens: ContextToken[] = [
  { tokenId: "00000000-0000-0000-0000-000000000021", label: "active",   group: "status", value: 1.0,  status: "active" },
  { tokenId: "00000000-0000-0000-0000-000000000022", label: "warning",  group: "status", value: 0.0,  status: "active" },
  { tokenId: "00000000-0000-0000-0000-000000000023", label: "critical", group: "status", value: -1.0, status: "active" },
];

export default function Demo(): JSX.Element {
  // Frontend-side canonical flow (no DB / no backend API required):
  //   UserOperation → resolveOperationVector → attractorKey
  //   → lookupStructureMap → StructureMapEntry
  //   → synthetic Emission → renderEmission → ComponentSpec[]
  //
  // Backend-side flow (attractor_resolve against DB, entity data) requires the dispatch API.
  const demoOperation = {
    operationType: "Search" as const,
    target: "demo",
    layer: "hub",
    action: "overview",
  };

  const demoVector = resolveOperationVector(demoOperation);
  const demoMapEntry = lookupStructureMap(defaultStructureMap, demoVector.attractorKey);

  const demoEmission: Emission = {
    packageId:    demoMapEntry?.packageId,
    schemaId:     demoMapEntry?.schemaId,
    componentIds: demoMapEntry?.componentIds ?? [],
    data:         { note: "demo scaffold — frontend-side resolution only" },
  };

  const componentSpecs = renderEmission(demoEmission, defaultComponentRegistry);

  return (
    <main style={{ fontFamily: "sans-serif", padding: "24px", maxWidth: "900px" }}>
      <h1>topolactor — public scaffold demo</h1>

      <div style={{
        background: "#fffbe6",
        border: "1px solid #e6c700",
        borderRadius: "4px",
        padding: "12px 16px",
        marginBottom: "24px",
      }}>
        <strong>Scaffold notice:</strong> This page exercises the <em>frontend-side</em> canonical
        flow only. Backend resolution (DB entity data, live recommendations) requires the{" "}
        <a href="/">dispatch panel</a>. No real business data is used.
        Demo seed: <code>db/demo_seed.sql</code>. Walkthrough: <code>docs/demo-walkthrough.md</code>.
      </div>

      <h2>Frontend Canonical Resolution</h2>
      <p style={{ color: "#555", fontSize: "0.9em" }}>
        <code>
          UserOperation → resolveOperationVector → attractorKey
          → lookupStructureMap → Emission → renderEmission → ComponentSpec[]
        </code>
      </p>
      <p style={{ color: "#555", fontSize: "0.9em" }}>
        Changing <code>defaultStructureMap</code> or <code>defaultComponentRegistry</code> entries
        changes what this page resolves and renders. Backend attractor_resolve (against the DB)
        is exercised via the <a href="/">dispatch panel</a>.
      </p>

      <details open style={{ marginBottom: "16px" }}>
        <summary style={{ cursor: "pointer", fontWeight: "bold" }}>OperationVector</summary>
        <pre style={{ background: "#f5f5f5", padding: "12px", fontSize: "0.85em" }}>
          {JSON.stringify(demoVector, null, 2)}
        </pre>
      </details>

      <ProjectionView emission={demoEmission} structureMap={demoMapEntry ?? undefined} />

      <h3>Expanded ComponentSpecs</h3>
      {componentSpecs.length === 0 ? (
        <p style={{ color: "#888" }}>no components resolved</p>
      ) : (
        <ul>
          {componentSpecs.map((spec) => (
            <li key={spec.componentId}>
              <code>{spec.componentId}</code> — <em>{spec.componentType}</em>
              {spec.componentType === "error" && (
                <span style={{ color: "crimson" }}> — {String(spec.def.error)}</span>
              )}
            </li>
          ))}
        </ul>
      )}

      <hr style={{ margin: "24px 0" }} />

      <h2>Context Token Registry (seed reference)</h2>
      <p style={{ color: "#555", fontSize: "0.9em" }}>
        These mirror <code>db/demo_seed.sql</code> values and are <strong>not</strong> runtime-resolved.
        Changing token <code>value</code> in the DB changes recommendation scores — but does not
        update this display. Live token state requires the dispatch API.
      </p>
      <ContextTokenBadgeList tokens={demoTokens} activeOnly />

      <h2>Context Route Recommendation</h2>
      <p style={{ color: "#555", fontSize: "0.9em" }}>
        Backend resolution via the dispatch API is required for live recommendations.
        Use the <a href="/">dispatch panel</a> with a <code>demo:hub:overview</code> operation
        and a context session ID.
      </p>
      <RecommendationPanel
        status="insufficient_history"
        statusDetail="NO_CONTEXT_HISTORY — backend dispatch required for live recommendations"
        nextOperations={[]}
        nextTokens={[]}
      />

      <hr style={{ marginTop: "32px" }} />
      <p style={{ color: "#888", fontSize: "0.85em" }}>
        See <a href="/admin">admin</a> for topology inspection,{" "}
        <a href="/runtime-status">runtime-status</a> for pipeline step validation,{" "}
        <a href="/">index</a> for the dispatch panel.
      </p>
    </main>
  );
}
