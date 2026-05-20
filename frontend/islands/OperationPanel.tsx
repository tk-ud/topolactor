import { useState, useEffect } from "preact/hooks";
import { JSX } from "preact";
import type { UserOperation, OperationType } from "../runtime/resolveOperationVector.ts";
import { resolveOperationVector } from "../runtime/resolveOperationVector.ts";
import { dispatchOperation } from "../api/dispatch.ts";
import type { Emission } from "../api/dispatch.ts";
import { EmissionView } from "../components/EmissionView.tsx";

const SESSION_TOKEN_KEY = "demo_jwt_token";

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
 * backend via dispatchOperation (with JWT token from sessionStorage), and
 * displays the resulting Emission through EmissionView.
 *
 * Login is required: the backend /dispatch endpoint is JWT-guarded.
 * Token is read from sessionStorage (demo_jwt_token) set by LoginPanel after login.
 */
export default function OperationPanel({ initialOperation }: Props): JSX.Element {
  const [target, setTarget] = useState(initialOperation?.target ?? "default");
  const [layer, setLayer] = useState(initialOperation?.layer ?? "entity");
  const [action, setAction] = useState(initialOperation?.action ?? "Search");
  const [operationType, setOperationType] = useState<OperationType>(
    initialOperation?.operationType ?? "Search",
  );
  const [contextSessionId, setContextSessionId] = useState("");
  const [contextTokenIds, setContextTokenIds] = useState("");

  const [token, setToken] = useState<string | null>(null);
  const [emission, setEmission] = useState<Emission | null>(null);
  const [loading, setLoading] = useState(false);
  const [vectorPreview, setVectorPreview] = useState<string | null>(null);

  useEffect(() => {
    setToken(sessionStorage.getItem(SESSION_TOKEN_KEY));
  }, []);

  async function handleSubmit(e: Event): Promise<void> {
    e.preventDefault();

    const currentToken = sessionStorage.getItem(SESSION_TOKEN_KEY);
    setToken(currentToken);

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

    const context: Record<string, string> = {};
    if (contextSessionId.trim()) context["ContextSessionId"] = contextSessionId.trim();
    if (contextTokenIds.trim()) context["ContextTokenIds"] = contextTokenIds.trim();

    const response = await dispatchOperation(
      {
        operationType: op.operationType,
        target: op.target,
        layer: op.layer,
        action: op.action,
        context: Object.keys(context).length > 0 ? context : undefined,
      },
      currentToken ?? undefined,
    );

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

  const authBannerStyle = token
    ? {
        background: "#e6f9e6",
        border: "1px solid #4caf50",
        borderRadius: "4px",
        padding: "8px 12px",
        marginBottom: "16px",
        fontSize: "0.9em",
      }
    : {
        background: "#fffbe6",
        border: "1px solid #e6c700",
        borderRadius: "4px",
        padding: "8px 12px",
        marginBottom: "16px",
        fontSize: "0.9em",
      };

  return (
    <div class="operation-panel" style={{ maxWidth: "640px" }}>
      <h2>Operation Panel</h2>

      <div style={authBannerStyle}>
        {token
          ? (
            <>
              <strong>Authenticated.</strong> JWT token loaded from sessionStorage.
              {" "}
              <a href="/auth" style={{ fontSize: "0.9em" }}>Re-login</a>
            </>
          )
          : (
            <>
              <strong>Not logged in.</strong> Dispatch requires authentication.{" "}
              <a href="/auth" style={{ fontWeight: "bold" }}>Login →</a>
              {" "}
              <span style={{ color: "#888" }}>
                (Without a token, the backend returns AUTH_TOKEN_MISSING.)
              </span>
            </>
          )}
      </div>

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

        <details style={{ marginBottom: "12px" }}>
          <summary style={{ cursor: "pointer", color: "#555", fontSize: "0.9em" }}>
            Context fields (optional — enables recommendation)
          </summary>
          <div style={{ marginTop: "8px", paddingLeft: "4px" }}>
            <label style={labelStyle}>
              Context Session ID
              <input
                style={inputStyle}
                type="text"
                placeholder="e.g. 00000000-0000-0000-0000-000000000031 (demo session)"
                value={contextSessionId}
                onInput={(e) => setContextSessionId((e.target as HTMLInputElement).value)}
              />
            </label>
            <label style={labelStyle}>
              Context Token IDs (comma-separated)
              <input
                style={inputStyle}
                type="text"
                placeholder="e.g. 00000000-0000-0000-0000-000000000021 (token_active)"
                value={contextTokenIds}
                onInput={(e) => setContextTokenIds((e.target as HTMLInputElement).value)}
              />
            </label>
          </div>
        </details>

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
