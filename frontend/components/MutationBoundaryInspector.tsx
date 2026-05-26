import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type MutationBoundaryInspectorProps = ComponentIdentityProps & {
  operationKind?: string;
  fieldName?: string;
  currentValue?: string;
  pendingValue?: string;
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

type InspectorRow = {
  label: string;
  value: string | undefined;
};

export function MutationBoundaryInspector({
  operationKind,
  fieldName,
  currentValue,
  pendingValue,
  title,
  className,
  design,
}: MutationBoundaryInspectorProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "border:1px solid #e0e0e0;border-radius:6px;padding:16px;font-family:monospace;font-size:0.88rem",
    design,
  );

  const rows: InspectorRow[] = [
    { label: "Operation Kind", value: operationKind },
    { label: "Field Name", value: fieldName },
    { label: "Current Value", value: currentValue },
    { label: "Pending Value", value: pendingValue },
  ];

  return (
    <div className={mergedClassName} style={mergedStyle}>
      {title && (
        <div style="font-weight:600;color:#333;margin-bottom:12px;font-size:0.92rem">
          {title}
        </div>
      )}
      <table style="border-collapse:collapse;width:100%">
        <tbody>
          {rows.map((row) => (
            <tr key={row.label} style="border-bottom:1px solid #f0f0f0">
              <td style="padding:5px 8px;color:#777;white-space:nowrap;width:40%;font-weight:500">
                {row.label}
              </td>
              <td style="padding:5px 8px;color:#222">
                {row.value !== undefined && row.value !== ""
                  ? row.value
                  : <span style="color:#bbb">—</span>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
