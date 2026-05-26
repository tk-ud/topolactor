import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type PatchField = {
  fieldLabel: string;
  before: string;
  after: string;
};

export type PatchPreviewPanelProps = ComponentIdentityProps & {
  fields?: PatchField[];
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function PatchPreviewPanel({
  fields = [],
  title,
  className,
  design,
}: PatchPreviewPanelProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "border:1px solid #ddd;border-radius:6px;padding:12px;font-family:monospace;font-size:0.88rem",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {title && (
        <div style="font-weight:600;margin-bottom:8px;color:#333">{title}</div>
      )}
      {fields.length === 0
        ? <div style="color:#aaa">No changes.</div>
        : (
          <table style="border-collapse:collapse;width:100%">
            <thead>
              <tr style="border-bottom:1px solid #eee;color:#888;font-size:0.8rem">
                <th style="text-align:left;padding:2px 6px">Field</th>
                <th style="text-align:left;padding:2px 6px">Before</th>
                <th style="text-align:left;padding:2px 6px">After</th>
              </tr>
            </thead>
            <tbody>
              {fields.map((f) => (
                <tr key={f.fieldLabel} style="border-bottom:1px solid #f5f5f5">
                  <td style="padding:3px 6px;color:#555">{f.fieldLabel}</td>
                  <td style="padding:3px 6px;color:#c5221f;text-decoration:line-through">
                    {f.before}
                  </td>
                  <td style="padding:3px 6px;color:#188038">{f.after}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
    </div>
  );
}
