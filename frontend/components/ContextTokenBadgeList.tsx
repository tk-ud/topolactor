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
    <section class="mb-4">
      <div class="flex flex-wrap gap-2">
        {displayed.map((token) => (
          <span
            key={token.tokenId}
            title={`value: ${token.value} | group: ${token.group ?? "—"} | id: ${token.tokenId}`}
            class={`rounded-full border px-2.5 py-0.5 font-mono text-xs ${
              token.status === "deprecated"
                ? "border-gray-300 bg-gray-100 text-gray-400"
                : "border-green-300 bg-green-50 text-gray-800"
            }`}
          >
            {token.label}
            {token.group && <span class="ml-1 text-gray-500">({token.group})</span>}
          </span>
        ))}
        {displayed.length === 0 && <span class="text-muted">トークンなし</span>}
      </div>
    </section>
  );
}
