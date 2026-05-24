import { ComponentChildren, JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type TabItem = {
  key: string;
  label: string;
  disabled?: boolean;
  children: ComponentChildren;
};

export type TabsProps = ComponentIdentityProps & {
  items: TabItem[];
  activeKey: string;
  onSelect: (key: string) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function Tabs({
  items,
  activeKey,
  onSelect,
  className,
  design,
}: TabsProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle("font-family:monospace", design);
  const activeItem = items.find((t) => t.key === activeKey);
  return (
    <div className={mergedClassName} style={mergedStyle}>
      <div
        role="tablist"
        style="display:flex;border-bottom:2px solid #e0e0e0;margin-bottom:16px;gap:0"
      >
        {items.map((tab) => {
          const isActive = tab.key === activeKey;
          return (
            <button
              key={tab.key}
              role="tab"
              aria-selected={isActive}
              disabled={tab.disabled}
              onClick={() => !tab.disabled && onSelect(tab.key)}
              style={`padding:8px 16px;border:none;background:transparent;font-family:monospace;font-size:0.9rem;cursor:${tab.disabled ? "not-allowed" : "pointer"};color:${isActive ? "#0070f3" : "#555"};border-bottom:2px solid ${isActive ? "#0070f3" : "transparent"};margin-bottom:-2px;opacity:${tab.disabled ? 0.5 : 1}`}
            >
              {tab.label}
            </button>
          );
        })}
      </div>
      <div role="tabpanel">
        {activeItem ? activeItem.children : null}
      </div>
    </div>
  );
}
