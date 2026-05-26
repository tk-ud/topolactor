import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type RowDetailDrawerProps = ComponentIdentityProps & {
  open: boolean;
  onClose: () => void;
  row?: Record<string, unknown>;
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function RowDetailDrawer({
  open,
  onClose,
  row,
  title = "Row Detail",
  className,
  design,
}: RowDetailDrawerProps): JSX.Element | null {
  if (!open) return null;
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "background:#fff;border-radius:8px;padding:24px;max-width:560px;width:100%;box-shadow:0 4px 24px rgba(0,0,0,0.18);font-family:monospace",
    design,
  );
  const entries = row ? Object.entries(row) : [];
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title}
      style="position:fixed;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,0.4);z-index:1000"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className={mergedClassName} style={mergedStyle}>
        <div style="display:flex;align-items:flex-start;justify-content:space-between;margin-bottom:16px">
          <h2 style="margin:0;font-size:1.05rem;color:#222;font-family:monospace">
            {title}
          </h2>
          <button
            onClick={onClose}
            aria-label="Close"
            style="background:none;border:none;font-size:1.2rem;cursor:pointer;color:#888;padding:0 0 0 16px"
          >
            ✕
          </button>
        </div>
        {entries.length === 0
          ? (
            <p style="color:#888;font-family:monospace;font-size:0.9rem;margin:0">
              No data.
            </p>
          )
          : (
            <table style="border-collapse:collapse;width:100%;font-family:monospace;font-size:0.88rem">
              <tbody>
                {entries.map(([key, val]) => (
                  <tr key={key} style="border-bottom:1px solid #f0f0f0">
                    <td style="padding:5px 10px 5px 0;color:#555;vertical-align:top;white-space:nowrap;font-weight:bold">
                      {key}
                    </td>
                    <td style="padding:5px 0;color:#222;word-break:break-all">
                      {val === null || val === undefined
                        ? <span style="color:#aaa">—</span>
                        : typeof val === "object"
                        ? (
                          <span style="color:#666">
                            {JSON.stringify(val)}
                          </span>
                        )
                        : String(val)}
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
