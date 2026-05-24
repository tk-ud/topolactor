import { ComponentChildren, JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ErrorStateProps = ComponentIdentityProps & {
  errorCode?: string;
  message?: string;
  description?: string;
  actions?: ComponentChildren;
  className?: string;
  design?: ComponentDesignParams;
};

export function ErrorState({
  errorCode,
  message = "An error occurred.",
  description,
  actions,
  className,
  design,
}: ErrorStateProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;align-items:center;gap:8px;padding:40px 24px;font-family:monospace;text-align:center",
    design,
  );
  return (
    <div role="alert" className={mergedClassName} style={mergedStyle}>
      {errorCode && (
        <div style="font-size:0.75rem;color:#c5221f;background:#fce8e6;border:1px solid #f5c6c4;border-radius:4px;padding:2px 8px;font-family:monospace">
          {errorCode}
        </div>
      )}
      <div style="font-size:1rem;color:#c5221f;font-weight:600">{message}</div>
      {description && (
        <div style="font-size:0.85rem;color:#888">{description}</div>
      )}
      {actions && <div style="margin-top:12px">{actions}</div>}
    </div>
  );
}
