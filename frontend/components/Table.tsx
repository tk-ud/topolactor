import { JSX } from "preact";
import type { ComponentDesignParams } from "./Button.tsx";

export type TableColumn<T> = {
  key: keyof T & string;
  header: string;
  render?: (value: T[keyof T], row: T) => JSX.Element | string;
};

export type TableProps<T> = {
  columns: TableColumn<T>[];
  rows: T[];
  rowKey: (row: T) => string;
  emptyMessage?: string;
  onRowClick?: (row: T) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function Table<T extends Record<string, unknown>>(
  {
    columns,
    rows,
    rowKey,
    emptyMessage = "No data.",
    onRowClick,
    className,
    design,
  }: TableProps<T>,
): JSX.Element {
  const designStyle = [design?.style].filter(Boolean).join(";");
  return (
    <table
      cellPadding="6"
      className={[
        className,
        design?.classname,
        design?.className,
        design?.tailwind,
      ].filter(Boolean).join(" ") || undefined}
      style={`border-collapse:collapse;width:100%;font-family:monospace;font-size:0.9rem;${designStyle}`}
    >
      <thead>
        <tr>
          {columns.map((col) => (
            <th
              key={col.key}
              style="text-align:left;border-bottom:2px solid #ccc;padding:4px 8px;background:#f5f5f5"
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
                colSpan={columns.length}
                style="color:#888;padding:12px 8px;text-align:center"
              >
                {emptyMessage}
              </td>
            </tr>
          )
          : (
            rows.map((row) => (
              <tr
                key={rowKey(row)}
                style="border-bottom:1px solid #eee"
                onClick={onRowClick ? () => onRowClick(row) : undefined}
              >
                {columns.map((col) => (
                  <td key={col.key} style="padding:4px 8px">
                    {col.render
                      ? col.render(row[col.key], row)
                      : String(row[col.key] ?? "")}
                  </td>
                ))}
              </tr>
            ))
          )}
      </tbody>
    </table>
  );
}
