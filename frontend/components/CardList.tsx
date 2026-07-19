import { JSX } from "preact";
import { useMemo, useState } from "preact/hooks";
import { Card } from "./Card.tsx";
import type { ComponentDesignParams } from "./Button.tsx";

export type CardListItem = {
  id?: string | number;
  title?: string;
  body?: string;
  footer?: string;
  variant?: "default" | "info" | "warning" | "error";
};

export type CardListProps = {
  items?: CardListItem[];
  emptyMessage?: string;
  className?: string;
  design?: ComponentDesignParams;
  onSelect?: (item: CardListItem, index: number) => void;
  /**
   * Built-in client-side title/body substring filter box, rendered above the cards. Local
   * Preact state only — filters the already-resolved items array component-side; no
   * propBinding/backend plumbing (the generic renderEmission pipeline has no mechanism to
   * filter a bound list prop by a live local input value, and none is needed for this).
   */
  searchable?: boolean;
  searchPlaceholder?: string;
};

function matchesQuery(item: CardListItem, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (!q) return true;
  return (item.title?.toLowerCase().includes(q) ?? false) ||
    (item.body?.toLowerCase().includes(q) ?? false);
}

export function CardList({
  items = [],
  emptyMessage = "データがありません",
  className,
  design,
  onSelect,
  searchable = false,
  searchPlaceholder = "検索",
}: CardListProps): JSX.Element {
  const [query, setQuery] = useState("");
  const mergedClass = [
    className,
    design?.classname,
    design?.className,
    design?.tailwind,
  ].filter(Boolean).join(" ") || undefined;

  const visibleItems = useMemo(
    () => searchable ? items.filter((item) => matchesQuery(item, query)) : items,
    [items, searchable, query],
  );

  const searchBox = searchable
    ? (
      <input
        type="search"
        value={query}
        placeholder={searchPlaceholder}
        onInput={(e) => setQuery((e.target as HTMLInputElement).value)}
        class="input-mono mb-2 w-full"
        data-card-list-search
      />
    )
    : null;

  if (visibleItems.length === 0) {
    return (
      <div class={mergedClass} data-component-kind="display/card_list">
        {searchBox}
        <div
          style="padding:16px;font-family:monospace;color:#888;font-size:0.85rem"
          data-card-list-empty
        >
          {emptyMessage}
        </div>
      </div>
    );
  }

  return (
    <div
      class={mergedClass}
      style={design?.style}
      data-component-kind="display/card_list"
    >
      {searchBox}
      {visibleItems.map((item, idx) => (
        <Card
          key={String(item.id ?? idx)}
          title={item.title}
          variant={item.variant ?? "default"}
          footer={item.footer}
          onClick={onSelect ? () => onSelect(item, idx) : undefined}
        >
          <div>{item.body ?? ""}</div>
        </Card>
      ))}
    </div>
  );
}
