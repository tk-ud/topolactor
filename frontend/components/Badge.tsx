import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type BadgeTone = "neutral" | "info" | "success" | "warning" | "error";

export type BadgeProps = ComponentIdentityProps & {
  label: string;
  tone?: BadgeTone;
  className?: string;
  design?: ComponentDesignParams;
};

const toneStyles: Record<BadgeTone, string> = {
  neutral: "background:#eee;color:#555;border:1px solid #ccc",
  info: "background:#e8f0fe;color:#1a73e8;border:1px solid #aecbfa",
  success: "background:#e6f4ea;color:#188038;border:1px solid #a8d5b5",
  warning: "background:#fef7e0;color:#b06000;border:1px solid #f9c852",
  error: "background:#fce8e6;color:#c5221f;border:1px solid #f5c6c4",
};

export function Badge({
  label,
  tone = "neutral",
  className,
  design,
}: BadgeProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    `${toneStyles[tone]};display:inline-block;padding:2px 8px;border-radius:12px;font-family:monospace;font-size:0.8rem;font-weight:600`,
    design,
  );
  return (
    <span className={mergedClassName} style={mergedStyle}>
      {label}
    </span>
  );
}

export type StatusBadgeProps = BadgeProps & {
  status?: string;
};

export function StatusBadge({ label, tone, status, ...rest }: StatusBadgeProps): JSX.Element {
  const resolvedLabel = status ?? label;
  const resolvedTone = tone ?? "neutral";
  return <Badge {...rest} label={resolvedLabel} tone={resolvedTone} />;
}
