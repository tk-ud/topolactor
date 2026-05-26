import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type CssVariableEntry = {
  key: string;
  value: string;
  description?: string;
};

export type CssVariablePreviewProps = ComponentIdentityProps & {
  variables?: CssVariableEntry[];
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function CssVariablePreview({
  variables = [],
  title,
  className,
  design,
}: CssVariablePreviewProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:0;font-family:monospace;border:1px solid #e0e0e0;border-radius:6px;overflow:hidden",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {title && (
        <div
          style="padding:8px 12px;background:#f5f5f5;border-bottom:1px solid #e0e0e0;font-size:0.85rem;font-weight:600;color:#444"
        >
          {title}
        </div>
      )}
      {variables.length === 0
        ? (
          <div style="padding:12px;color:#bbb;font-size:0.85rem">
            No CSS variables.
          </div>
        )
        : (
          <table
            style="width:100%;border-collapse:collapse;font-size:0.82rem"
          >
            <thead>
              <tr style="background:#fafafa;border-bottom:1px solid #e0e0e0">
                <th
                  style="padding:6px 12px;text-align:left;color:#666;font-weight:600;white-space:nowrap"
                >
                  Variable
                </th>
                <th
                  style="padding:6px 12px;text-align:left;color:#666;font-weight:600"
                >
                  Value
                </th>
                <th
                  style="padding:6px 12px;text-align:left;color:#666;font-weight:600"
                >
                  Description
                </th>
              </tr>
            </thead>
            <tbody>
              {variables.map((v, i) => (
                <tr
                  key={v.key}
                  style={`border-bottom:1px solid #f0f0f0;background:${
                    i % 2 === 0 ? "#fff" : "#fafafa"
                  }`}
                >
                  <td
                    style="padding:6px 12px;color:#0070f3;white-space:nowrap;font-weight:500"
                  >
                    {v.key}
                  </td>
                  <td style="padding:6px 12px;color:#333">
                    {v.value}
                  </td>
                  <td style="padding:6px 12px;color:#888;font-size:0.8rem">
                    {v.description ?? ""}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
    </div>
  );
}
