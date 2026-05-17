import { JSX } from "preact";
import type { Emission, ValidationError } from "../api/dispatch.ts";
import { validationErrorText } from "../api/dispatch.ts";

type Props = {
  emission: Emission;
};

/**
 * EmissionView is a debug/inspection component that surfaces all fields of an
 * Emission directly.  It is intentionally plain and carries no business logic.
 *
 * Use this during development to inspect what the backend emits before real
 * projection components are wired up.
 */
export function EmissionView({ emission }: Props): JSX.Element {
  const errorLines = emission.errors?.map(validationErrorText).join("\n");

  return (
    <div class="emission-view" style={{ fontFamily: "monospace", fontSize: "0.85rem" }}>
      <h4>Emission</h4>
      <table style={{ borderCollapse: "collapse", width: "100%" }}>
        <tbody>
          <Row label="structureMapId" value={emission.structureMapId} />
          <Row label="packageId" value={emission.packageId} />
          <Row label="schemaId" value={emission.schemaId} />
          <Row
            label="componentIds"
            value={
              emission.componentIds ? emission.componentIds.join(", ") : undefined
            }
          />
          <Row
            label="data"
            value={
              emission.data ? JSON.stringify(emission.data, null, 2) : undefined
            }
            pre
          />
          <Row
            label="errors"
            value={errorLines}
            error={Boolean(emission.errors && emission.errors.length > 0)}
          />
        </tbody>
      </table>
    </div>
  );
}

type RowProps = {
  label: string;
  value: string | undefined;
  pre?: boolean;
  error?: boolean;
};

function Row({ label, value, pre, error }: RowProps): JSX.Element {
  const cellStyle = {
    border: "1px solid #ccc",
    padding: "4px 8px",
    verticalAlign: "top" as const,
    color: error ? "crimson" : "inherit",
  };

  return (
    <tr>
      <td style={{ ...cellStyle, fontWeight: "bold", whiteSpace: "nowrap" as const }}>
        {label}
      </td>
      <td style={cellStyle}>
        {value === undefined ? (
          <em style={{ color: "#999" }}>—</em>
        ) : pre ? (
          <pre style={{ margin: 0 }}>{value}</pre>
        ) : (
          value
        )}
      </td>
    </tr>
  );
}
