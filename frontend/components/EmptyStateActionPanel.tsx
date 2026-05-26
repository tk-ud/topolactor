import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type EmptyStateAction = {
  id: string;
  label: string;
};

export type EmptyStateActionPanelProps = ComponentIdentityProps & {
  title?: string;
  description?: string;
  actions?: EmptyStateAction[];
  onSelect?: (id: string) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function EmptyStateActionPanel({
  title,
  description,
  actions = [],
  onSelect,
  className,
  design,
}: EmptyStateActionPanelProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;align-items:center;justify-content:center;padding:40px 24px;font-family:monospace;border:1px dashed #d8d8d8;border-radius:8px;background:#fafafa;text-align:center",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {title && (
        <div style="font-size:1rem;font-weight:600;color:#333;margin-bottom:8px">
          {title}
        </div>
      )}
      {description && (
        <div style="font-size:0.88rem;color:#777;margin-bottom:20px;max-width:400px">
          {description}
        </div>
      )}
      {actions.length > 0 && (
        <div style="display:flex;flex-wrap:wrap;gap:8px;justify-content:center">
          {actions.map((action) => (
            <button
              key={action.id}
              type="button"
              onClick={() => onSelect?.(action.id)}
              style="padding:7px 16px;border-radius:4px;border:1px solid #ccc;background:#fff;color:#333;font-family:monospace;font-size:0.88rem;cursor:pointer"
            >
              {action.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
