import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type PermissionHintPanelProps = ComponentIdentityProps & {
  requiredPermission?: string;
  message?: string;
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function PermissionHintPanel({
  requiredPermission,
  message,
  title,
  className,
  design,
}: PermissionHintPanelProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "border:1px solid #b0bec5;border-radius:6px;padding:16px;font-family:monospace;background:#f5f7fa",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      <div style="display:flex;align-items:flex-start;gap:12px">
        <span
          aria-hidden="true"
          style="font-size:1.2rem;flex-shrink:0;color:#546e7a;margin-top:1px"
        >
          🔒
        </span>
        <div style="flex:1;min-width:0">
          {title && (
            <div style="font-size:0.92rem;font-weight:600;color:#263238;margin-bottom:6px">
              {title}
            </div>
          )}
          {requiredPermission && (
            <div style="display:flex;align-items:center;gap:6px;margin-bottom:6px">
              <span style="font-size:0.78rem;color:#546e7a;font-weight:500">
                Required permission:
              </span>
              <code style="font-size:0.82rem;background:#e8eaf6;color:#283593;border-radius:3px;padding:1px 6px;font-family:monospace">
                {requiredPermission}
              </code>
            </div>
          )}
          {message && (
            <div style="font-size:0.87rem;color:#546e7a">
              {message}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
