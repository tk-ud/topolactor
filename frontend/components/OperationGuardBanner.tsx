import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type OperationGuardLevel = "warning" | "error" | "info";

export type OperationGuardBannerProps = ComponentIdentityProps & {
  message?: string;
  level?: OperationGuardLevel;
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

const levelStyles: Record<OperationGuardLevel, { bg: string; border: string; color: string; icon: string }> = {
  info: { bg: "#e8f4fd", border: "#90caf9", color: "#0d47a1", icon: "ℹ" },
  warning: { bg: "#fffde7", border: "#ffe082", color: "#7b5800", icon: "⚠" },
  error: { bg: "#fdecea", border: "#f48fb1", color: "#b71c1c", icon: "✖" },
};

export function OperationGuardBanner({
  message,
  level = "info",
  title,
  className,
  design,
}: OperationGuardBannerProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const { bg, border, color, icon } = levelStyles[level];
  const mergedStyle = mergeDesignStyle(
    `background:${bg};border:1px solid ${border};border-radius:6px;padding:12px 16px;font-family:monospace;color:${color}`,
    design,
  );
  return (
    <div role="alert" aria-live="polite" className={mergedClassName} style={mergedStyle}>
      <div style="display:flex;align-items:flex-start;gap:10px">
        <span style={`font-size:1rem;flex-shrink:0;color:${color}`} aria-hidden="true">
          {icon}
        </span>
        <div style="flex:1;min-width:0">
          {title && (
            <div style="font-size:0.92rem;font-weight:600;margin-bottom:3px">
              {title}
            </div>
          )}
          {message && (
            <div style="font-size:0.87rem;opacity:0.9">
              {message}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
