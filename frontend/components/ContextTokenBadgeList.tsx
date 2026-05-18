import { JSX } from "preact";

export type ContextToken = {
  tokenId: string;
  label: string;
  group?: string;
  value: number;
  status: "active" | "deprecated";
};

export type ContextTokenBadgeListProps = {
  tokens: ContextToken[];
  activeOnly?: boolean;
};

export function ContextTokenBadgeList(props: ContextTokenBadgeListProps): JSX.Element {
  const displayed = props.activeOnly
    ? props.tokens.filter((t) => t.status === "active")
    : props.tokens;

  return (
    <section style={{ marginBottom: "16px" }}>
      <div style={{ display: "flex", flexWrap: "wrap", gap: "8px" }}>
        {displayed.map((token) => (
          <span
            key={token.tokenId}
            title={`value: ${token.value} | group: ${token.group ?? "—"} | id: ${token.tokenId}`}
            style={{
              padding: "3px 10px",
              borderRadius: "12px",
              border: "1px solid #aaa",
              background: token.status === "deprecated" ? "#f5f5f5" : "#e8f4e8",
              color: token.status === "deprecated" ? "#999" : "#333",
              fontSize: "0.85em",
              fontFamily: "monospace",
            }}
          >
            {token.label}
            {token.group && <span style={{ color: "#888", marginLeft: "4px" }}>({token.group})</span>}
          </span>
        ))}
        {displayed.length === 0 && <span style={{ color: "#888" }}>no tokens</span>}
      </div>
    </section>
  );
}
