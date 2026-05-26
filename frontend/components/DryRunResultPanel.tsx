import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type DryRunResultItem = {
  id: string;
  label: string;
  kind?: string;
  impact?: string;
};

export type DryRunResultPanelProps = ComponentIdentityProps & {
  results?: DryRunResultItem[];
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function DryRunResultPanel({
  results = [],
  title,
  className,
  design,
}: DryRunResultPanelProps): JSX.Element {
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
      {results.length === 0
        ? (
          <div style="color:#aaa">No dry run results.</div>
        )
        : (
          <table style="border-collapse:collapse;width:100%">
            <thead>
              <tr style="border-bottom:1px solid #eee;color:#888;font-size:0.8rem">
                <th style="text-align:left;padding:3px 6px">Label</th>
                <th style="text-align:left;padding:3px 6px">Kind</th>
                <th style="text-align:left;padding:3px 6px">Impact</th>
              </tr>
            </thead>
            <tbody>
              {results.map((item) => (
                <tr key={item.id} style="border-bottom:1px solid #f5f5f5">
                  <td style="padding:5px 6px;color:#222">{item.label}</td>
                  <td style="padding:5px 6px">
                    {item.kind
                      ? (
                        <span style="display:inline-block;background:#e8eaf6;color:#283593;border-radius:3px;padding:1px 7px;font-size:0.78rem">
                          {item.kind}
                        </span>
                      )
                      : <span style="color:#bbb">—</span>}
                  </td>
                  <td style="padding:5px 6px;color:#555">
                    {item.impact ?? <span style="color:#bbb">—</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
    </div>
  );
}
