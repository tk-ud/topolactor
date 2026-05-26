import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type FieldResolverInspectorProps = ComponentIdentityProps & {
  fieldName?: string;
  resolvedValue?: string;
  resolverKind?: string;
  details?: Record<string, unknown>;
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function FieldResolverInspector({
  fieldName,
  resolvedValue,
  resolverKind,
  details,
  title,
  className,
  design,
}: FieldResolverInspectorProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "background:#f8f9fa;border:1px solid #e0e0e0;border-radius:6px;padding:12px 16px;font-family:monospace",
    design,
  );
  const rowStyle = "display:flex;gap:12px;padding:4px 0;border-bottom:1px solid #f0f0f0;font-size:0.88rem";
  const keyStyle = "color:#888;min-width:120px;flex-shrink:0";
  const valStyle = "color:#222;word-break:break-all";
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {title && (
        <div style="font-size:0.8rem;color:#888;margin-bottom:10px;font-weight:600;text-transform:uppercase;letter-spacing:0.04em">
          {title}
        </div>
      )}
      <div>
        <div style={rowStyle}>
          <span style={keyStyle}>Field name</span>
          <span style={valStyle}>{fieldName ?? <span style="color:#ccc">—</span>}</span>
        </div>
        <div style={rowStyle}>
          <span style={keyStyle}>Resolved value</span>
          <span style={valStyle}>{resolvedValue ?? <span style="color:#ccc">—</span>}</span>
        </div>
        <div style={`${rowStyle};border-bottom:none`}>
          <span style={keyStyle}>Resolver kind</span>
          <span style={valStyle}>
            {resolverKind
              ? (
                <span style="background:#eee;border:1px solid #ddd;border-radius:3px;padding:1px 6px;font-size:0.82rem">
                  {resolverKind}
                </span>
              )
              : <span style="color:#ccc">—</span>}
          </span>
        </div>
      </div>
      {details && Object.keys(details).length > 0 && (
        <div style="margin-top:10px">
          <div style="font-size:0.75rem;color:#aaa;margin-bottom:4px">Details</div>
          <pre style="background:#fff;border:1px solid #e8e8e8;border-radius:4px;padding:8px;font-family:monospace;font-size:0.8rem;color:#444;overflow:auto;white-space:pre-wrap;word-break:break-all;margin:0">
            {JSON.stringify(details, null, 2)}
          </pre>
        </div>
      )}
    </div>
  );
}
