import { useState } from "preact/hooks";
import { JSX } from "preact";
import {
  type RegistryVectorValidationResult,
  validateRegistryVector,
} from "../api/adminApi.ts";

/**
 * RegistryVectorValidator — admin island for Registrar draft/promote validation.
 *
 * Validates a candidate registry ID array (UUIDs) against existing registry rows
 * using multi-hot cosine similarity. Policy is loaded from function_parameters
 * on the backend. Requires login (JWT in sessionStorage under demo_jwt_token).
 *
 * validationClass → color mapping:
 *   pass                    → green
 *   related_existing_registry → yellow (warning, non-blocking)
 *   near_duplicate_vector   → orange (blocking)
 *   duplicate_vector        → red (blocking)
 *   zero_vector             → gray (blocking)
 *   explicit_error          → red (blocking, policy/DB issue)
 */
export default function RegistryVectorValidator(): JSX.Element {
  const [registryTable, setRegistryTable] = useState("relation_registry");
  const [queryIdsText, setQueryIdsText] = useState("");
  const [result, setResult] = useState<RegistryVectorValidationResult | null>(null);
  const [notBound, setNotBound] = useState(false);
  const [notAuthed, setNotAuthed] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (notBound) {
    return (
      <p style={{ fontFamily: "monospace", color: "#888" }}>
        バックエンド未接続 — DEMO_BACKEND_URL が未設定です (501)。
        <br />
        Docker Compose 起動後に <a href="/">ログイン</a> してください。
      </p>
    );
  }

  if (notAuthed) {
    return (
      <p style={{ fontFamily: "monospace", color: "#888" }}>
        ログインが必要です — <a href="/login">ログインページ</a> で認証してから再度アクセスしてください。
      </p>
    );
  }

  async function handleValidate(e: Event): Promise<void> {
    e.preventDefault();
    setError(null);
    setResult(null);

    const rawIds = queryIdsText
      .split(/[\n,\s]+/)
      .map((s) => s.trim())
      .filter((s) => s.length > 0);

    if (!registryTable.trim()) {
      setError("registryTable は必須です。");
      return;
    }

    setLoading(true);
    try {
      const res = await validateRegistryVector(registryTable.trim(), rawIds);
      if (res === null) {
        setNotBound(true);
        return;
      }
      setResult(res);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      if (msg.includes("HTTP 401")) {
        setNotAuthed(true);
      } else {
        setError(msg);
      }
    }
    setLoading(false);
  }

  const inputStyle = {
    padding: "4px 6px",
    fontFamily: "monospace",
    width: "100%",
    boxSizing: "border-box" as const,
  };

  return (
    <div style={{ maxWidth: "760px" }}>
      <form onSubmit={handleValidate} style={{ display: "flex", flexDirection: "column", gap: "12px" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.85rem", marginBottom: "4px" }}>
            registryTable <span style={{ color: "crimson" }}>*</span>
          </label>
          <input
            style={{ ...inputStyle, width: "280px" }}
            type="text"
            placeholder="例: relation_registry"
            value={registryTable}
            onInput={(e) => setRegistryTable((e.target as HTMLInputElement).value)}
            required
          />
        </div>
        <div>
          <label style={{ display: "block", fontSize: "0.85rem", marginBottom: "4px" }}>
            queryIds — UUID を改行・カンマ・スペース区切りで入力
          </label>
          <textarea
            style={{ ...inputStyle, height: "100px", resize: "vertical" }}
            placeholder={"例:\na1b2c3d4-...\ne5f6a7b8-..."}
            value={queryIdsText}
            onInput={(e) => setQueryIdsText((e.target as HTMLTextAreaElement).value)}
          />
        </div>
        <div>
          <button type="submit" disabled={loading}>
            {loading ? "検証中…" : "レジストリベクター検証"}
          </button>
        </div>
      </form>

      {error && (
        <p style={{ marginTop: "12px", color: "#c00", fontFamily: "monospace", fontSize: "0.85rem" }}>
          {error}
        </p>
      )}

      {result && <ValidationResultView result={result} />}
    </div>
  );
}

function classColor(cls: string): string {
  switch (cls) {
    case "pass": return "#2a7a2a";
    case "related_existing_registry": return "#a07000";
    case "near_duplicate_vector": return "#c05000";
    case "duplicate_vector": return "#c00000";
    case "zero_vector": return "#666";
    case "explicit_error": return "#c00000";
    default: return "#333";
  }
}

function ValidationResultView({ result }: { result: RegistryVectorValidationResult }): JSX.Element {
  const color = classColor(result.validationClass);
  const blockingLabel = result.isBlocking ? "blocking" : "non-blocking";

  return (
    <div style={{ marginTop: "20px", fontFamily: "monospace" }}>
      <div style={{
        padding: "10px 14px",
        border: `2px solid ${color}`,
        borderRadius: "4px",
        marginBottom: "16px",
      }}>
        <div style={{ color, fontWeight: "bold", fontSize: "1.05rem" }}>
          {result.validationClass}
        </div>
        <div style={{ fontSize: "0.85rem", color: "#444", marginTop: "4px" }}>
          {blockingLabel}
          {result.statusDetail ? ` — ${result.statusDetail}` : ""}
        </div>
      </div>

      {result.neighbors.length > 0 && (
        <>
          <h4 style={{ marginBottom: "8px" }}>近傍レジストリ ({result.neighbors.length}件)</h4>
          <table style={{ borderCollapse: "collapse", width: "100%", fontSize: "0.85rem" }}>
            <thead>
              <tr style={{ background: "#f4f4f4" }}>
                {["name", "cosineScore", "matchedIds", "reason"].map((h) => (
                  <th
                    key={h}
                    style={{ border: "1px solid #ccc", padding: "5px 8px", textAlign: "left" }}
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {result.neighbors.map((n) => (
                <tr key={n.registryId}>
                  <td style={{ border: "1px solid #ccc", padding: "4px 8px" }}>{n.name}</td>
                  <td style={{ border: "1px solid #ccc", padding: "4px 8px" }}>
                    {n.cosineScore.toFixed(4)}
                  </td>
                  <td style={{ border: "1px solid #ccc", padding: "4px 8px", fontSize: "0.78rem", color: "#555" }}>
                    {n.matchedIds.join(", ") || "—"}
                  </td>
                  <td style={{ border: "1px solid #ccc", padding: "4px 8px" }}>{n.reason}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}
