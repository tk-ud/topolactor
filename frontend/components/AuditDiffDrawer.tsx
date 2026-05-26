import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type AuditDiffEntry = {
  field: string;
  before: string;
  after: string;
  timestamp?: string;
};

export type AuditDiffDrawerProps = ComponentIdentityProps & {
  open: boolean;
  onClose: () => void;
  entries?: AuditDiffEntry[];
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function AuditDiffDrawer({
  open,
  onClose,
  entries,
  title,
  className,
  design,
}: AuditDiffDrawerProps): JSX.Element | null {
  if (!open) return null;
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "background:#fff;border-radius:8px;padding:24px;max-width:640px;width:100%;box-shadow:0 4px 24px rgba(0,0,0,0.18);font-family:monospace",
    design,
  );
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title ?? "Audit Diff"}
      style="position:fixed;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,0.4);z-index:1000"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className={mergedClassName} style={mergedStyle}>
        <div style="display:flex;align-items:flex-start;justify-content:space-between;margin-bottom:16px">
          <h2 style="margin:0;font-size:1.05rem;color:#222">{title ?? "Audit Diff"}</h2>
          <button
            onClick={onClose}
            aria-label="Close"
            style="background:none;border:none;font-size:1.2rem;cursor:pointer;color:#888;padding:0 0 0 16px"
          >
            ✕
          </button>
        </div>
        {(!entries || entries.length === 0)
          ? (
            <div style="color:#aaa;font-size:0.88rem;padding:8px 0">No diff entries.</div>
          )
          : (
            <table style="width:100%;border-collapse:collapse;font-size:0.85rem">
              <thead>
                <tr style="background:#f8f9fa">
                  <th style="text-align:left;padding:6px 8px;border-bottom:2px solid #e0e0e0;color:#555;font-weight:600">Field</th>
                  <th style="text-align:left;padding:6px 8px;border-bottom:2px solid #e0e0e0;color:#555;font-weight:600">Before</th>
                  <th style="text-align:left;padding:6px 8px;border-bottom:2px solid #e0e0e0;color:#555;font-weight:600">After</th>
                  <th style="text-align:left;padding:6px 8px;border-bottom:2px solid #e0e0e0;color:#555;font-weight:600">Timestamp</th>
                </tr>
              </thead>
              <tbody>
                {entries.map((entry, idx) => (
                  <tr key={idx} style={idx % 2 === 0 ? "" : "background:#f8f9fa"}>
                    <td style="padding:6px 8px;border-bottom:1px solid #f0f0f0;color:#333">{entry.field}</td>
                    <td style="padding:6px 8px;border-bottom:1px solid #f0f0f0;color:#c5221f;text-decoration:line-through">
                      {entry.before}
                    </td>
                    <td style="padding:6px 8px;border-bottom:1px solid #f0f0f0;color:#188038">{entry.after}</td>
                    <td style="padding:6px 8px;border-bottom:1px solid #f0f0f0;color:#888;font-size:0.8rem">
                      {entry.timestamp ?? "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
      </div>
    </div>
  );
}
