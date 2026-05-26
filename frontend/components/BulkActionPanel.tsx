import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type BulkActionPanelAction = {
  id: string;
  label: string;
};

export type BulkActionPanelProps = ComponentIdentityProps & {
  selectedCount?: number;
  actions?: BulkActionPanelAction[];
  onSelect: (id: string) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function BulkActionPanel({
  selectedCount = 0,
  actions = [],
  onSelect,
  className,
  design,
}: BulkActionPanelProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:row;align-items:center;gap:12px;padding:8px 12px;background:#f5f5f5;border:1px solid #e0e0e0;border-radius:6px;font-family:monospace",
    design,
  );
  const actionsDisabled = selectedCount === 0;
  return (
    <div className={mergedClassName} style={mergedStyle}>
      <span style="font-size:0.9rem;color:#444;font-family:monospace;white-space:nowrap">
        {selectedCount} selected
      </span>
      <div style="display:flex;flex-direction:row;gap:8px;flex-wrap:wrap">
        {actions.map((action) => (
          <button
            key={action.id}
            type="button"
            disabled={actionsDisabled}
            onClick={() => onSelect(action.id)}
            style={`padding:5px 12px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.88rem;background:#fff;cursor:${
              actionsDisabled ? "not-allowed" : "pointer"
            };opacity:${actionsDisabled ? 0.5 : 1};color:#333`}
          >
            {action.label}
          </button>
        ))}
      </div>
    </div>
  );
}
