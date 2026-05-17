import { useState } from "preact/hooks";
import { JSX } from "preact";
import type { UserOperation, OperationType } from "../runtime/resolveOperationVector.ts";
import { resolveOperationVector } from "../runtime/resolveOperationVector.ts";
import { dispatchOperation } from "../api/dispatch.ts";
import type { Emission } from "../api/dispatch.ts";
import { EmissionView } from "../components/EmissionView.tsx";

const OPERATION_TYPES: OperationType[] = [
  "Search",
  "Create",
  "diffUpdate",
  "logicalDelete",
];

type Props = {
  initialOperation?: Partial<UserOperation>;
};

/**
 * OperationPanel is a Fresh island (client-side interactive).
 *
 * It provides a form that lets the user compose a UserOperation, converts it
 * to an OperationVector via resolveOperationVector, dispatches it to the
 * backend via dispatchOperation, and displays the resulting Emission through
 * EmissionView.
 *
 * This is the primary physical interaction point for the frontend skeleton.
 */
export default function OperationPanel({ initialOperation }: Props): JSX.Element {
  const [target, setTarget] = useState(initialOperation?.target ?? "default");
  const [layer, setLayer] = useState(initialOperation?.layer ?? "entity");
  const [action, setAction] = useState(initialOperation?.action ?? "Search");
  const [operationType, setOperationType] = useState<OperationType>(
    initialOperation?.operationType ?? "Search",
  );

  const [emission, setEmission] = useState<Emission | null>(null);
  const [loading, setLoading] = useState(false);
  const [vectorPreview, setVectorPreview] = useState<string | null>(null);

  async function handleSubmit(e: Event): Promise<void> {
    e.preventDefault();

    const op: UserOperation = {
      operationType,
      target,
      layer,
      action,
    };

    const vector = resolveOperationVector(op);
    setVectorPreview(JSON.stringify(vector, null, 2));
    setEmission(null);
    setLoading(true);

    const response = await dispatchOperation({
      operationType: op.operationType,
      target: op.target,
      layer: op.layer,
      action: op.action,
    });

    setLoading(false);

    if (response.emission) {
      setEmission(response.emission);
    } else {
      setEmission({
        errors: response.errors ?? [{ message: "dispatch: no emission returned" }],
      });
    }
  }

  const labelStyle = {
    display: "block",
    marginBottom: "4px",
    fontWeight: "bold" as const,
  };
  const inputStyle = {
    display: "block",
    width: "100%",
    marginBottom: "12px",
    padding: "4px 6px",
    fontFamily: "monospace",
  };

  return (
    <div class="operation-panel" style={{ maxWidth: "640px" }}>
      <h2>Operation Panel</h2>
      <form onSubmit={handleSubmit}>
        <label style={labelStyle}>
          target
          <input
            style={inputStyle}
            type="text"
            value={target}
            onInput={(e) => setTarget((e.target as HTMLInputElement).value)}
            required
          />
        </label>

        <label style={labelStyle}>
          layer
          <input
            style={inputStyle}
            type="text"
            value={layer}
            onInput={(e) => setLayer((e.target as HTMLInputElement).value)}
            required
          />
        </label>

        <label style={labelStyle}>
          action
          <input
            style={inputStyle}
            type="text"
            value={action}
            onInput={(e) => setAction((e.target as HTMLInputElement).value)}
            required
          />
        </label>

        <label style={labelStyle}>
          operationType
          <select
            style={inputStyle}
            value={operationType}
            onChange={(e) =>
              setOperationType((e.target as HTMLSelectElement).value as OperationType)
            }
          >
            {OPERATION_TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
        </label>

        <button type="submit" disabled={loading}>
          {loading ? "Dispatching…" : "Dispatch operation"}
        </button>
      </form>

      {vectorPreview && (
        <div style={{ marginTop: "16px" }}>
          <h4>OperationVector (pre-dispatch)</h4>
          <pre
            style={{
              background: "#f4f4f4",
              padding: "8px",
              fontSize: "0.8rem",
              overflowX: "auto",
            }}
          >
            {vectorPreview}
          </pre>
        </div>
      )}

      {loading && <p aria-live="polite">Dispatching to /api/dispatch…</p>}

      {emission && (
        <div style={{ marginTop: "16px" }}>
          <EmissionView emission={emission} />
        </div>
      )}
    </div>
  );
}
