import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type RollbackCandidate = {
  id: string;
  label: string;
  timestamp?: string;
};

export type RollbackCandidatePanelProps = ComponentIdentityProps & {
  candidates?: RollbackCandidate[];
  onSelect?: (id: string) => void;
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function RollbackCandidatePanel({
  candidates = [],
  onSelect,
  title,
  className,
  design,
}: RollbackCandidatePanelProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "border:1px solid #ddd;border-radius:6px;padding:16px;font-family:monospace;font-size:0.88rem",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {title && (
        <div style="font-weight:600;color:#333;margin-bottom:12px;font-size:0.92rem">
          {title}
        </div>
      )}
      {candidates.length === 0
        ? (
          <div style="color:#aaa">No rollback candidates available.</div>
        )
        : (
          <ul style="margin:0;padding:0;list-style:none;display:flex;flex-direction:column;gap:4px">
            {candidates.map((candidate) => (
              <li
                key={candidate.id}
                style={`display:flex;align-items:center;justify-content:space-between;padding:8px 10px;border-radius:4px;background:#fafafa;border:1px solid #f0f0f0;${onSelect ? "cursor:pointer;" : ""}`}
                onClick={() => onSelect?.(candidate.id)}
              >
                <span style="color:#222;font-weight:500">
                  {candidate.label}
                </span>
                {candidate.timestamp && (
                  <span style="color:#999;font-size:0.8rem;flex-shrink:0;margin-left:12px">
                    {candidate.timestamp}
                  </span>
                )}
              </li>
            ))}
          </ul>
        )}
    </div>
  );
}
