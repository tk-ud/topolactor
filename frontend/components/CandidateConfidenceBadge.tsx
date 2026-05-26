import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ConfidenceLevel = "high" | "medium" | "low" | "unknown";

export type CandidateConfidenceBadgeProps = ComponentIdentityProps & {
  label: string;
  confidence?: ConfidenceLevel;
  score?: number;
  className?: string;
  design?: ComponentDesignParams;
};

const levelStyles: Record<ConfidenceLevel, string> = {
  high: "background:#e6f4ea;color:#188038;border:1px solid #a8d5b5",
  medium: "background:#fef7e0;color:#b06000;border:1px solid #f9c852",
  low: "background:#fce8e6;color:#c5221f;border:1px solid #f5c6c4",
  unknown: "background:#eee;color:#555;border:1px solid #ccc",
};

export function CandidateConfidenceBadge({
  label,
  confidence = "unknown",
  score,
  className,
  design,
}: CandidateConfidenceBadgeProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    `${levelStyles[confidence]};display:inline-flex;align-items:center;gap:4px;padding:2px 8px;border-radius:12px;font-family:monospace;font-size:0.8rem;font-weight:600`,
    design,
  );
  const displayLabel = score !== undefined
    ? `${label} (${Math.round(score * 100)}%)`
    : label;
  return (
    <span className={mergedClassName} style={mergedStyle}>
      {displayLabel}
    </span>
  );
}
