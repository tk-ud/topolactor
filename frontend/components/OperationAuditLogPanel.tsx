import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type AuditLogEntry = {
  id: string;
  operation: string;
  timestamp?: string;
  actor?: string;
};

export type OperationAuditLogPanelProps = ComponentIdentityProps & {
  entries?: AuditLogEntry[];
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function OperationAuditLogPanel({
  entries = [],
  title,
  className,
  design,
}: OperationAuditLogPanelProps): JSX.Element {
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
      {entries.length === 0
        ? (
          <div style="color:#aaa">No audit log entries.</div>
        )
        : (
          <table style="border-collapse:collapse;width:100%">
            <thead>
              <tr style="border-bottom:1px solid #eee;color:#888;font-size:0.8rem">
                <th style="text-align:left;padding:3px 6px">Timestamp</th>
                <th style="text-align:left;padding:3px 6px">Operation</th>
                <th style="text-align:left;padding:3px 6px">Actor</th>
              </tr>
            </thead>
            <tbody>
              {entries.map((entry) => (
                <tr key={entry.id} style="border-bottom:1px solid #f5f5f5">
                  <td style="padding:5px 6px;color:#777;white-space:nowrap">
                    {entry.timestamp ?? <span style="color:#bbb">—</span>}
                  </td>
                  <td style="padding:5px 6px;color:#222;font-weight:500">
                    {entry.operation}
                  </td>
                  <td style="padding:5px 6px;color:#555">
                    {entry.actor ?? <span style="color:#bbb">—</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
    </div>
  );
}
