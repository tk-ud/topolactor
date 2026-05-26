import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type RelationPathSegment = {
  label: string;
  kind?: string;
};

export type RelationPathPreviewProps = ComponentIdentityProps & {
  segments?: RelationPathSegment[];
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function RelationPathPreview({
  segments,
  title,
  className,
  design,
}: RelationPathPreviewProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "background:#f8f9fa;border:1px solid #e0e0e0;border-radius:6px;padding:12px 16px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {title && (
        <div style="font-size:0.8rem;color:#888;margin-bottom:8px;font-weight:600;text-transform:uppercase;letter-spacing:0.04em">
          {title}
        </div>
      )}
      {(!segments || segments.length === 0)
        ? (
          <span style="color:#aaa;font-size:0.9rem">No path.</span>
        )
        : (
          <div style="display:flex;flex-wrap:wrap;align-items:center;gap:4px">
            {segments.map((seg, idx) => (
              <span key={idx} style="display:inline-flex;align-items:center;gap:4px">
                <span style="display:inline-flex;flex-direction:column;align-items:center;gap:1px">
                  <span style="background:#e8f0fe;color:#1a73e8;border:1px solid #aecbfa;border-radius:4px;padding:2px 8px;font-size:0.85rem;font-weight:500">
                    {seg.label}
                  </span>
                  {seg.kind && (
                    <span style="font-size:0.7rem;color:#888">{seg.kind}</span>
                  )}
                </span>
                {idx < segments.length - 1 && (
                  <span style="color:#999;font-size:0.9rem;margin:0 2px">→</span>
                )}
              </span>
            ))}
          </div>
        )}
    </div>
  );
}
