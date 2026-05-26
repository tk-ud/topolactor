import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ExportCandidatePanelCandidate = {
  id: string;
  label: string;
  format?: string;
};

export type ExportCandidatePanelProps = ComponentIdentityProps & {
  candidates?: ExportCandidatePanelCandidate[];
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function ExportCandidatePanel({
  candidates = [],
  title = "Export Candidates",
  className,
  design,
}: ExportCandidatePanelProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "background:#fff;border:1px solid #e0e0e0;border-radius:8px;padding:16px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      <div style="font-size:0.95rem;font-weight:bold;color:#222;font-family:monospace;margin-bottom:12px;padding-bottom:8px;border-bottom:1px solid #eee">
        {title}
      </div>
      {candidates.length === 0
        ? (
          <p style="color:#888;font-family:monospace;font-size:0.88rem;margin:0">
            No candidates.
          </p>
        )
        : (
          <div style="display:flex;flex-direction:column;gap:6px">
            {candidates.map((candidate) => (
              <div
                key={candidate.id}
                style="display:flex;flex-direction:row;align-items:center;justify-content:space-between;padding:6px 10px;border:1px solid #f0f0f0;border-radius:4px;background:#fafafa"
              >
                <span style="font-size:0.88rem;color:#333;font-family:monospace">
                  {candidate.label}
                </span>
                {candidate.format && (
                  <span style="font-size:0.75rem;color:#666;background:#e8e8e8;border-radius:3px;padding:2px 7px;font-family:monospace;border:1px solid #d0d0d0">
                    {candidate.format}
                  </span>
                )}
              </div>
            ))}
          </div>
        )}
    </div>
  );
}
