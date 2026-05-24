import { ComponentChildren, JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type EmptyStateProps = ComponentIdentityProps & {
  message?: string;
  description?: string;
  actions?: ComponentChildren;
  className?: string;
  design?: ComponentDesignParams;
};

export function EmptyState({
  message = "No data.",
  description,
  actions,
  className,
  design,
}: EmptyStateProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;align-items:center;gap:8px;padding:40px 24px;font-family:monospace;text-align:center",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      <div style="font-size:1rem;color:#555;font-weight:600">{message}</div>
      {description && (
        <div style="font-size:0.85rem;color:#888">{description}</div>
      )}
      {actions && <div style="margin-top:12px">{actions}</div>}
    </div>
  );
}
