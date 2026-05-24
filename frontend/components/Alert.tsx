import { ComponentChildren, JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type AlertTone = "info" | "success" | "warning" | "error";

export type AlertProps = ComponentIdentityProps & {
  message: string;
  tone?: AlertTone;
  title?: string;
  children?: ComponentChildren;
  className?: string;
  design?: ComponentDesignParams;
};

const toneStyles: Record<AlertTone, { border: string; bg: string; titleColor: string; textColor: string }> = {
  info: { border: "#aecbfa", bg: "#e8f0fe", titleColor: "#1a73e8", textColor: "#174ea6" },
  success: { border: "#a8d5b5", bg: "#e6f4ea", titleColor: "#188038", textColor: "#1e7e34" },
  warning: { border: "#f9c852", bg: "#fef7e0", titleColor: "#b06000", textColor: "#7a4200" },
  error: { border: "#f5c6c4", bg: "#fce8e6", titleColor: "#c5221f", textColor: "#a50e0e" },
};

export function Alert({
  message,
  tone = "info",
  title,
  children,
  className,
  design,
}: AlertProps): JSX.Element {
  const t = toneStyles[tone];
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    `background:${t.bg};border:1px solid ${t.border};border-radius:6px;padding:12px 16px;font-family:monospace`,
    design,
  );
  return (
    <div role="alert" className={mergedClassName} style={mergedStyle}>
      {title && (
        <div style={`font-weight:600;color:${t.titleColor};margin-bottom:4px;font-size:0.9rem`}>
          {title}
        </div>
      )}
      <div style={`color:${t.textColor};font-size:0.9rem`}>{message}</div>
      {children && <div style="margin-top:8px">{children}</div>}
    </div>
  );
}
