const FLOW_STEPS = [
  "stored_topology_data",
  "user_operation",
  "operation_vector",
  "attractor_resolve",
  "structure_map_resolve",
  "package_resolve",
  "schema_resolve",
  "component_expand",
  "emission_or_projection",
];

export default function RuntimeStatus() {
  return (
    <main style={{ fontFamily: "monospace", padding: "2rem" }}>
      <h1>topolactor — runtime validation status</h1>
      <p>
        This page is a static checklist of the canonical runtime flow. It is
        not a runtime result page; live runtime verification happens through
        /demo dispatch against backend + DB.
      </p>

      <h2>Canonical Flow</h2>
      <ul>
        {FLOW_STEPS.map((step, i) => (
          <li key={step}>
            <strong>{step}</strong>
            {i < FLOW_STEPS.length - 1 ? " →" : ""}
            {" "}
            <span style={{ color: "#888" }}>[static checklist]</span>
          </li>
        ))}
      </ul>

      <h2>Boundary Check</h2>
      <table cellPadding="6">
        <thead>
          <tr>
            <th>Boundary</th>
            <th>Role</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>DB</td>
            <td>semantic topology space</td>
            <td>DB-backed runtime source of truth</td>
          </tr>
          <tr>
            <td>Backend</td>
            <td>abstract runtime</td>
            <td>canonical runtime + explicit errors (no silent fallback)</td>
          </tr>
          <tr>
            <td>Frontend</td>
            <td>physical interaction projection</td>
            <td>API proxy to backend runtime (/api/dispatch)</td>
          </tr>
        </tbody>
      </table>
    </main>
  );
}
