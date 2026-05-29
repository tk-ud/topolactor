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

const levelClasses: Record<ConfidenceLevel, string> = {
  high: "badge-ok",
  medium: "badge-warn",
  low: "badge-error",
  unknown: "badge border border-gray-300 bg-gray-100 text-gray-600",
};

export function CandidateConfidenceBadge({
  label,
  confidence = "unknown",
  score,
  className,
  design,
}: CandidateConfidenceBadgeProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(
    `inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-xs font-semibold ${levelClasses[confidence]}`,
    design,
  );
  const mergedStyle = mergeDesignStyle("", design);
  const displayLabel = score !== undefined
    ? `${label} (${Math.round(score * 100)}%)`
    : label;
  return (
    <span className={mergedClassName} style={mergedStyle || undefined}>
      {displayLabel}
    </span>
  );
}
