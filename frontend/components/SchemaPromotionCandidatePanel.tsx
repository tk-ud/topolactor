import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type SchemaPromotionCandidate = {
  id: string;
  label: string;
  kind?: string;
};

export type SchemaPromotionCandidatePanelProps = ComponentIdentityProps & {
  candidates?: SchemaPromotionCandidate[];
  onSelect?: (id: string) => void;
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function SchemaPromotionCandidatePanel({
  candidates,
  onSelect,
  title,
  className,
  design,
}: SchemaPromotionCandidatePanelProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "background:#fff;border:1px solid #e0e0e0;border-radius:6px;font-family:monospace;overflow:hidden",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      <div style="padding:10px 14px;background:#f8f9fa;border-bottom:1px solid #e0e0e0;font-size:0.85rem;font-weight:600;color:#444">
        {title ?? "Schema Promotion Candidates"}
      </div>
      {(!candidates || candidates.length === 0)
        ? (
          <div style="padding:12px 14px;color:#aaa;font-size:0.88rem">No candidates found.</div>
        )
        : (
          <div>
            {candidates.map((c) => (
              <div
                key={c.id}
                onClick={() => onSelect && onSelect(c.id)}
                style={`display:flex;align-items:center;justify-content:space-between;padding:8px 14px;border-bottom:1px solid #f0f0f0;font-size:0.88rem;cursor:${onSelect ? "pointer" : "default"}`}
                onMouseEnter={(e) => {
                  if (onSelect) (e.currentTarget as HTMLDivElement).style.background = "#f8f9fa";
                }}
                onMouseLeave={(e) => {
                  if (onSelect) (e.currentTarget as HTMLDivElement).style.background = "";
                }}
              >
                <span style="color:#222">{c.label}</span>
                {c.kind && (
                  <span style="background:#eee;border:1px solid #ddd;border-radius:3px;padding:1px 7px;font-size:0.78rem;color:#555">
                    {c.kind}
                  </span>
                )}
              </div>
            ))}
          </div>
        )}
    </div>
  );
}
