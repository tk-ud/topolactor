import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type OptimisticUpdateBoundaryProps = ComponentIdentityProps & {
  pending?: boolean;
  error?: string;
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function OptimisticUpdateBoundary({
  pending,
  error,
  title,
  className,
  design,
}: OptimisticUpdateBoundaryProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "font-family:monospace;border-radius:6px;border:1px solid #e0e0e0;padding:12px 16px",
    design,
  );

  let indicatorColor = "#bbb";
  let indicatorLabel = "Idle";
  let borderAccent = "1px solid #e0e0e0";
  let bgColor = "#f8f9fa";

  if (error) {
    indicatorColor = "#c5221f";
    indicatorLabel = "Error";
    borderAccent = "1px solid #f5c6c4";
    bgColor = "#fce8e6";
  } else if (pending) {
    indicatorColor = "#b06000";
    indicatorLabel = "Pending";
    borderAccent = "1px solid #f9c852";
    bgColor = "#fef7e0";
  }

  return (
    <div
      className={mergedClassName}
      style={`${mergedStyle};background:${bgColor};border:${borderAccent}`}
    >
      <div style="display:flex;align-items:center;gap:8px">
        <span
          style={`display:inline-block;width:10px;height:10px;border-radius:50%;background:${indicatorColor};flex-shrink:0`}
        />
        {title && (
          <span style="font-size:0.85rem;font-weight:600;color:#444">{title}</span>
        )}
        <span style={`font-size:0.82rem;color:${indicatorColor};font-weight:500`}>
          {indicatorLabel}
        </span>
      </div>
      {error && (
        <div style="margin-top:8px;font-size:0.82rem;color:#c5221f;padding:6px 8px;background:#fff;border:1px solid #f5c6c4;border-radius:4px;word-break:break-word">
          {error}
        </div>
      )}
    </div>
  );
}
