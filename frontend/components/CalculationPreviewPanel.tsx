import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type CalculationPreviewPanelProps = ComponentIdentityProps & {
  title?: string;
  result?: unknown;
  status?: string;
  className?: string;
  design?: ComponentDesignParams;
};

const STATUS_STYLES: Record<string, string> = {
  ok: "background:#e6f4ea;color:#1e7e34;border:1px solid #b7dfbe",
  error: "background:#fce8e6;color:#c5221f;border:1px solid #f4b8b5",
  pending: "background:#fff8e1;color:#795500;border:1px solid #ffe082",
};

function statusStyle(status: string | undefined): string {
  if (!status) return "background:#f5f5f5;color:#888;border:1px solid #e0e0e0";
  return (
    STATUS_STYLES[status] ??
    "background:#f5f5f5;color:#555;border:1px solid #e0e0e0"
  );
}

export function CalculationPreviewPanel({
  title,
  result,
  status,
  className,
  design,
}: CalculationPreviewPanelProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "border:1px solid #ddd;border-radius:6px;padding:16px;font-family:monospace;font-size:0.88rem",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      <div style="display:flex;align-items:center;gap:10px;margin-bottom:12px">
        {title && (
          <div style="font-weight:600;color:#333;font-size:0.92rem;flex:1">
            {title}
          </div>
        )}
        {status && (
          <span
            style={`${statusStyle(status)};border-radius:3px;padding:2px 9px;font-size:0.78rem;font-weight:600;font-family:monospace`}
          >
            {status}
          </span>
        )}
      </div>
      {result !== undefined
        ? (
          <pre style="background:#f8f8f8;border:1px solid #eee;border-radius:4px;padding:10px;overflow-x:auto;font-size:0.82rem;color:#222;margin:0;white-space:pre-wrap;word-break:break-all">
            {JSON.stringify(result, null, 2)}
          </pre>
        )
        : (
          <div style="color:#aaa">No result data.</div>
        )}
    </div>
  );
}
