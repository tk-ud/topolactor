import { JSX } from "preact";

export type EntityRow = {
  entityId: string;
  label: string;
  state: string;
  relations: string[];
};

export type EntityTableProjectionProps = {
  entities: EntityRow[];
  schemaName?: string;
};

export function EntityTableProjection(props: EntityTableProjectionProps): JSX.Element {
  return (
    <section style={{ marginBottom: "16px" }}>
      {props.schemaName && (
        <p style={{ fontSize: "0.85em", color: "#666" }}>schema: {props.schemaName}</p>
      )}
      <table cellPadding="6" style={{ borderCollapse: "collapse", width: "100%" }}>
        <thead>
          <tr style={{ background: "#f5f5f5" }}>
            <th style={{ textAlign: "left" }}>entity_id</th>
            <th style={{ textAlign: "left" }}>label</th>
            <th style={{ textAlign: "left" }}>state</th>
            <th style={{ textAlign: "left" }}>relations</th>
          </tr>
        </thead>
        <tbody>
          {props.entities.map((row) => (
            <tr key={row.entityId} style={{ borderTop: "1px solid #eee" }}>
              <td><code style={{ fontSize: "0.8em" }}>{row.entityId.slice(0, 8)}…</code></td>
              <td>{row.label}</td>
              <td>{row.state}</td>
              <td>{row.relations.join(", ")}</td>
            </tr>
          ))}
          {props.entities.length === 0 && (
            <tr>
              <td colSpan={4} style={{ color: "#888", textAlign: "center" }}>no entities resolved</td>
            </tr>
          )}
        </tbody>
      </table>
    </section>
  );
}
