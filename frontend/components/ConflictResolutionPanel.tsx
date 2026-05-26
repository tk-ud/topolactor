import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ConflictEntry = {
  field: string;
  localValue: string;
  remoteValue: string;
};

export type ConflictResolutionPanelProps = ComponentIdentityProps & {
  conflicts?: ConflictEntry[];
  onSelect?: (field: string) => void;
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function ConflictResolutionPanel({
  conflicts,
  onSelect,
  title,
  className,
  design,
}: ConflictResolutionPanelProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "background:#fff;border:1px solid #e0e0e0;border-radius:6px;font-family:monospace;overflow:hidden",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      <div style="padding:10px 14px;background:#f8f9fa;border-bottom:1px solid #e0e0e0;font-size:0.85rem;font-weight:600;color:#444">
        {title ?? "Conflict Resolution"}
      </div>
      {(!conflicts || conflicts.length === 0)
        ? (
          <div style="padding:12px 14px;color:#aaa;font-size:0.88rem">No conflicts detected.</div>
        )
        : (
          <table style="width:100%;border-collapse:collapse;font-size:0.85rem">
            <thead>
              <tr style="background:#f8f9fa">
                <th style="text-align:left;padding:7px 12px;border-bottom:2px solid #e0e0e0;color:#555;font-weight:600;font-family:monospace">
                  Field
                </th>
                <th style="text-align:left;padding:7px 12px;border-bottom:2px solid #e0e0e0;color:#555;font-weight:600;font-family:monospace">
                  Local
                </th>
                <th style="text-align:left;padding:7px 12px;border-bottom:2px solid #e0e0e0;color:#555;font-weight:600;font-family:monospace">
                  Remote
                </th>
              </tr>
            </thead>
            <tbody>
              {conflicts.map((c, idx) => (
                <tr
                  key={c.field}
                  onClick={() => onSelect && onSelect(c.field)}
                  style={`cursor:${onSelect ? "pointer" : "default"};background:${idx % 2 === 0 ? "#fff" : "#f8f9fa"}`}
                  onMouseEnter={(e) => {
                    if (onSelect) {
                      (e.currentTarget as HTMLTableRowElement).style.background = "#e8f0fe";
                    }
                  }}
                  onMouseLeave={(e) => {
                    if (onSelect) {
                      (e.currentTarget as HTMLTableRowElement).style.background =
                        idx % 2 === 0 ? "#fff" : "#f8f9fa";
                    }
                  }}
                >
                  <td style="padding:7px 12px;border-bottom:1px solid #f0f0f0;color:#333;font-weight:500">
                    {c.field}
                  </td>
                  <td style="padding:7px 12px;border-bottom:1px solid #f0f0f0;color:#1a73e8;word-break:break-word">
                    {c.localValue}
                  </td>
                  <td style="padding:7px 12px;border-bottom:1px solid #f0f0f0;color:#b06000;word-break:break-word">
                    {c.remoteValue}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
    </div>
  );
}
