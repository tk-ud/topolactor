import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  type ContextToken,
  createContextToken,
  deprecateContextToken,
  fetchContextTokens,
} from "../api/adminApi.ts";

/**
 * ContextTokenRegistryEditor — admin island for context_token_registry.
 *
 * context_token_registry is the hub Registry for the discrete tokens that
 * form the sparse cosine vectors used by the recommendation engine.
 * Each token has a value in [-1.0, 1.0] that defines its meaning direction.
 *
 * When the backend registry endpoint is not yet bound (501), this island
 * shows a "レジストリ未接続" notice rather than displaying seed tokens.
 */
export default function ContextTokenRegistryEditor(): JSX.Element {
  const [tokens, setTokens] = useState<ContextToken[]>([]);
  const [notBound, setNotBound] = useState(false);
  const [newLabel, setNewLabel] = useState("");
  const [newGroup, setNewGroup] = useState("");
  const [newValue, setNewValue] = useState("0.0");
  const [status, setStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetchContextTokens().then((result) => {
      if (result === null) {
        setNotBound(true);
      } else {
        setTokens(result);
      }
    }).catch((err: unknown) => {
      setStatus(err instanceof Error ? err.message : String(err));
    });
  }, []);

  if (notBound) {
    return (
      <p style={{ fontFamily: "monospace", color: "#888" }}>
        レジストリ未接続 — context_token_registry エンドポイントは未実装です (501)。
      </p>
    );
  }

  async function handleAdd(e: Event): Promise<void> {
    e.preventDefault();
    const value = parseFloat(newValue);
    if (!newLabel || isNaN(value) || value < -1 || value > 1) {
      setStatus("label と value（-1.0〜1.0）は必須です。");
      return;
    }
    setLoading(true);
    setStatus(null);
    const result = await createContextToken({
      label: newLabel,
      group: newGroup || null,
      value,
    });
    if (result.ok) {
      setTokens((prev) => [
        ...prev,
        {
          tokenId: result.tokenId ?? crypto.randomUUID(),
          label: newLabel,
          group: newGroup || null,
          value,
          status: "active",
        },
      ]);
      setNewLabel("");
      setNewGroup("");
      setNewValue("0.0");
    }
    setStatus(result.message);
    setLoading(false);
  }

  async function handleDeprecate(tokenId: string): Promise<void> {
    setLoading(true);
    const result = await deprecateContextToken(tokenId);
    if (result.ok) {
      setTokens((prev) =>
        prev.map((t) => t.tokenId === tokenId ? { ...t, status: "deprecated" } : t)
      );
    }
    setStatus(result.message);
    setLoading(false);
  }

  const inputStyle = {
    padding: "4px 6px",
    fontFamily: "monospace",
    marginRight: "8px",
  };

  return (
    <div style={{ maxWidth: "760px" }}>
      <h3 style={{ fontFamily: "monospace" }}>アクティブトークン一覧</h3>
      <table style={{ borderCollapse: "collapse", width: "100%", marginBottom: "24px" }}>
        <thead>
          <tr style={{ background: "#f4f4f4" }}>
            {["label", "group", "value", "status", ""].map((h) => (
              <th
                key={h}
                style={{ border: "1px solid #ccc", padding: "6px 10px", textAlign: "left" }}
              >
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {tokens.map((t) => (
            <tr key={t.tokenId} style={{ opacity: t.status === "deprecated" ? 0.5 : 1 }}>
              <td style={{ border: "1px solid #ccc", padding: "4px 8px" }}>{t.label}</td>
              <td style={{ border: "1px solid #ccc", padding: "4px 8px" }}>{t.group ?? "—"}</td>
              <td style={{ border: "1px solid #ccc", padding: "4px 8px", fontFamily: "monospace" }}>
                {t.value.toFixed(2)}
              </td>
              <td style={{ border: "1px solid #ccc", padding: "4px 8px" }}>{t.status}</td>
              <td style={{ border: "1px solid #ccc", padding: "4px 8px" }}>
                {t.status === "active" && (
                  <button
                    onClick={() => handleDeprecate(t.tokenId)}
                    disabled={loading}
                    style={{ color: "#c00", fontSize: "0.8rem" }}
                  >
                    非推奨にする
                  </button>
                )}
              </td>
            </tr>
          ))}
          {tokens.length === 0 && (
            <tr>
              <td colSpan={5} style={{ padding: "8px", color: "#888", textAlign: "center" }}>
                トークンなし
              </td>
            </tr>
          )}
        </tbody>
      </table>

      <h3 style={{ fontFamily: "monospace" }}>新規トークン追加</h3>
      <form onSubmit={handleAdd} style={{ display: "flex", flexWrap: "wrap", gap: "8px", alignItems: "flex-end" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.85rem", marginBottom: "2px" }}>
            label <span style={{ color: "crimson" }}>*</span>
          </label>
          <input
            style={inputStyle}
            type="text"
            placeholder="例: 点検中"
            value={newLabel}
            onInput={(e) => setNewLabel((e.target as HTMLInputElement).value)}
            required
          />
        </div>
        <div>
          <label style={{ display: "block", fontSize: "0.85rem", marginBottom: "2px" }}>group</label>
          <input
            style={inputStyle}
            type="text"
            placeholder="例: status"
            value={newGroup}
            onInput={(e) => setNewGroup((e.target as HTMLInputElement).value)}
          />
        </div>
        <div>
          <label style={{ display: "block", fontSize: "0.85rem", marginBottom: "2px" }}>
            value [-1.0〜1.0] <span style={{ color: "crimson" }}>*</span>
          </label>
          <input
            style={{ ...inputStyle, width: "80px" }}
            type="number"
            step="0.1"
            min="-1.0"
            max="1.0"
            value={newValue}
            onInput={(e) => setNewValue((e.target as HTMLInputElement).value)}
            required
          />
        </div>
        <button type="submit" disabled={loading}>
          {loading ? "追加中…" : "トークン追加"}
        </button>
      </form>

      {status && (
        <p
          style={{
            marginTop: "12px",
            padding: "8px",
            background: "#f0f0f0",
            fontFamily: "monospace",
            fontSize: "0.85rem",
          }}
        >
          {status}
        </p>
      )}
    </div>
  );
}
