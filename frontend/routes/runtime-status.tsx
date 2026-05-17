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
        This page shows the canonical flow steps and their current skeleton
        status. Real runtime validation is out of scope for this skeleton.
      </p>

      <h2>Canonical Flow</h2>
      <ul>
        {FLOW_STEPS.map((step, i) => (
          <li key={step}>
            <strong>{step}</strong>
            {i < FLOW_STEPS.length - 1 ? " →" : ""}
            {" "}
            <span style={{ color: "#888" }}>
              [skeleton — not yet wired to real data]
            </span>
          </li>
        ))}
      </ul>

      <h2>Boundary Check</h2>
      <table border={1} cellPadding={6}>
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
            <td>schema defined, seed empty</td>
          </tr>
          <tr>
            <td>Backend</td>
            <td>abstract runtime</td>
            <td>skeleton wired, stubs return null</td>
          </tr>
          <tr>
            <td>Frontend</td>
            <td>physical interaction projection</td>
            <td>skeleton wired, dispatches to /api/dispatch</td>
          </tr>
        </tbody>
      </table>
    </main>
  );
}
