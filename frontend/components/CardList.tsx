import { JSX } from "preact";
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
};

export function CardList({
  items = [],
  emptyMessage = "データがありません",
  className,
  design,
  onSelect,
}: CardListProps): JSX.Element {
  const mergedClass = [
    className,
    design?.classname,
    design?.className,
    design?.tailwind,
  ].filter(Boolean).join(" ") || undefined;

  if (items.length === 0) {
    return (
      <div
        class={mergedClass}
        style="padding:16px;font-family:monospace;color:#888;font-size:0.85rem"
        data-component-kind="display/card_list"
      >
        {emptyMessage}
      </div>
    );
  }

  return (
    <div
      class={mergedClass}
      style={design?.style}
      data-component-kind="display/card_list"
    >
      {items.map((item, idx) => (
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
