import { JSX } from "preact";
import { resolveOperationVector } from "../runtime/resolveOperationVector.ts";
import { lookupStructureMap, defaultStructureMap } from "../structure_map.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { ProjectionView } from "../components/ProjectionView.tsx";
import { ContextTokenBadgeList, type ContextToken } from "../components/ContextTokenBadgeList.tsx";
import type { Emission } from "../api/dispatch.ts";

// Seed reference values mirroring db/demo_seed.sql rows.
// NOT runtime-resolved — these are static documentation only.
const demoTokens: ContextToken[] = [
  { tokenId: "00000000-0000-0000-0000-000000000021", label: "active",   group: "status", value: 1.0,  status: "active" },
  { tokenId: "00000000-0000-0000-0000-000000000022", label: "warning",  group: "status", value: 0.0,  status: "active" },
  { tokenId: "00000000-0000-0000-0000-000000000023", label: "critical", group: "status", value: -1.0, status: "active" },
];

/**
 * /demo-static — static structure diagram for frontend-side canonical flow.
 *
 * This page runs the frontend-only pipeline against defaultStructureMap and
 * defaultComponentRegistry without any backend API call. It is a developer
 * reference diagram, not a runtime demo.
 *
 * For backend runtime emission, use /demo.
 */
export default function DemoStatic(): JSX.Element {
  const demoOperation = {
    operationType: "Search" as const,
    target: "demo",
    layer: "hub",
    action: "overview",
  };

  const demoVector = resolveOperationVector(demoOperation);
  const demoMapEntry = lookupStructureMap(defaultStructureMap, demoVector.attractorKey);

  // Synthetic emission for structure diagram only — NOT a runtime result.
  const demoEmission: Emission = {
    packageId:    demoMapEntry?.packageId,
    schemaId:     demoMapEntry?.schemaId,
    componentIds: demoMapEntry?.componentIds ?? [],
    data:         { note: "static structure diagram — frontend-side resolution only, not a runtime result" },
  };

  const componentSpecs = renderEmission(demoEmission, defaultComponentRegistry);

  return (
    <main style={{ fontFamily: "sans-serif", padding: "24px", maxWidth: "900px" }}>
      <h1>topolactor — static structure diagram</h1>

      <div style={{
        background: "#fffbe6",
        border: "1px solid #e6c700",
        borderRadius: "4px",
        padding: "12px 16px",
        marginBottom: "24px",
      }}>
        <strong>Static diagram (not runtime):</strong> This page exercises the{" "}
        <em>frontend-side</em> canonical flow only. No backend API is called.
        No DB data is read. Output reflects <code>defaultStructureMap</code> and{" "}
        <code>defaultComponentRegistry</code> module contents, not live DB state.
        For backend runtime emission, use <a href="/demo">/demo</a>.
      </div>

      <h2>Frontend-side canonical flow</h2>
      <p style={{ color: "#555", fontSize: "0.9em" }}>
        <code>
          UserOperation → resolveOperationVector → attractorKey
          → lookupStructureMap → Emission → renderEmission → ComponentSpec[]
        </code>
      </p>
      <p style={{ color: "#555", fontSize: "0.9em" }}>
        Changing <code>defaultStructureMap</code> or <code>defaultComponentRegistry</code>{" "}
        entries changes what this page resolves. Backend attractor_resolve (against the DB)
        is exercised via the <a href="/">dispatch panel</a> or <a href="/demo">/demo</a>.
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

      <h2>Context Token Registry (seed reference values)</h2>
      <p style={{ color: "#555", fontSize: "0.9em" }}>
        These values mirror <code>db/demo_seed.sql</code> and are{" "}
        <strong>not</strong> runtime-resolved. Changing token{" "}
        <code>value</code> in the DB changes recommendation scores but does not
        update this static display. Live token state requires the dispatch API at{" "}
        <a href="/demo">/demo</a>.
      </p>
      <ContextTokenBadgeList tokens={demoTokens} activeOnly />

      <hr style={{ marginTop: "32px" }} />
      <p style={{ color: "#888", fontSize: "0.85em" }}>
        See <a href="/demo">/demo</a> for backend runtime dispatch,{" "}
        <a href="/admin">admin</a> for topology inspection,{" "}
        <a href="/runtime-status">runtime-status</a> for pipeline step validation,{" "}
        <a href="/">index</a> for the dispatch panel.
      </p>
    </main>
  );
}
