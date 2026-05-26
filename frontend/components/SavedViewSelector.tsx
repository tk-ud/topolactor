import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type SavedViewSelectorView = {
  id: string;
  label: string;
};

export type SavedViewSelectorProps = ComponentIdentityProps & {
  views?: SavedViewSelectorView[];
  selectedId?: string;
  onSelect?: (id: string) => void;
  label?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function SavedViewSelector({
  views = [],
  selectedId,
  onSelect,
  label = "Saved views",
  className,
  design,
}: SavedViewSelectorProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:4px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      <div style="font-size:0.78rem;color:#555;font-family:monospace;margin-bottom:4px">
        {label}
      </div>
      {views.map((view) => {
        const isSelected = view.id === selectedId;
        return (
          <div
            key={view.id}
            role="option"
            aria-selected={isSelected}
            onClick={() => onSelect?.(view.id)}
            style={`padding:6px 10px;border-radius:4px;cursor:pointer;font-size:0.9rem;font-family:monospace;border:1px solid ${
              isSelected ? "#0070f3" : "#e0e0e0"
            };background:${isSelected ? "#e8f0fe" : "#fff"};color:${
              isSelected ? "#0070f3" : "#333"
            }`}
          >
            {view.label}
          </div>
        );
      })}
    </div>
  );
}
