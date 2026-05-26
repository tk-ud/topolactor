import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type VirtualizedDataTableColumn = {
  key: string;
  header: string;
};

export type VirtualizedDataTableProps = ComponentIdentityProps & {
  columns?: VirtualizedDataTableColumn[];
  rows?: Record<string, unknown>[];
  emptyMessage?: string;
  onRowSelect?: (row: Record<string, unknown>) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function VirtualizedDataTable({
  columns = [],
  rows = [],
  emptyMessage = "No data.",
  onRowSelect,
  className,
  design,
}: VirtualizedDataTableProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "width:100%;overflow-x:auto;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      <table
        cellPadding="6"
        style="border-collapse:collapse;width:100%;font-family:monospace;font-size:0.9rem"
      >
        <thead>
          <tr>
            {columns.map((col) => (
              <th
                key={col.key}
                style="text-align:left;border-bottom:2px solid #ccc;padding:4px 8px;background:#f5f5f5;font-family:monospace"
              >
                {col.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.length === 0
            ? (
              <tr>
                <td
                  colSpan={columns.length || 1}
                  style="color:#888;padding:12px 8px;text-align:center;font-family:monospace"
                >
                  {emptyMessage}
                </td>
              </tr>
            )
            : rows.map((row, rowIndex) => (
              <tr
                key={rowIndex}
                style={`border-bottom:1px solid #eee;cursor:${onRowSelect ? "pointer" : "default"}`}
                onClick={onRowSelect ? () => onRowSelect(row) : undefined}
              >
                {columns.map((col) => (
                  <td
                    key={col.key}
                    style="padding:4px 8px;font-family:monospace"
                  >
                    {String(row[col.key] ?? "")}
                  </td>
                ))}
              </tr>
            ))}
        </tbody>
      </table>
    </div>
  );
}
