import { JSX } from "preact";
import { Badge, type BadgeTone } from "./Badge.tsx";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ValidationStatus = "pass" | "warning" | "fail" | "blocking";

export type ValidationResultItem = {
  key: string;
  status: ValidationStatus;
  label?: string;
  message?: string;
  detail?: unknown;
};

export type ValidationResultPayload = {
  items: ValidationResultItem[];
  overall?: ValidationStatus;
  message?: string;
};

export type ValidationResultPanelProps = ComponentIdentityProps & {
  result?: ValidationResultPayload | null;
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

const statusToneMap: Record<ValidationStatus, BadgeTone> = {
  pass: "success",
  warning: "warning",
  fail: "error",
  blocking: "error",
};

export function ValidationResultPanel({
  result,
  title,
  className,
  design,
}: ValidationResultPanelProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "border:1px solid #e0e0e0;border-radius:6px;padding:16px;font-family:monospace",
    design,
  );

  if (!result) {
    return (
      <div className={mergedClassName} style={mergedStyle}>
        <span style="color:#888;font-size:0.9rem">No validation result.</span>
      </div>
    );
  }

  return (
    <div className={mergedClassName} style={mergedStyle}>
      <div style="display:flex;align-items:center;gap:10px;margin-bottom:12px">
        {title && (
          <span style="font-weight:600;color:#333;font-size:0.95rem">{title}</span>
        )}
        {result.overall && (
          <Badge label={result.overall} tone={statusToneMap[result.overall]} />
        )}
      </div>
      {result.message && (
        <div style="font-size:0.9rem;color:#555;margin-bottom:12px">
          {result.message}
        </div>
      )}
      {result.items.length > 0 && (
        <ul style="margin:0;padding:0;list-style:none;display:flex;flex-direction:column;gap:6px">
          {result.items.map((item) => (
            <li
              key={item.key}
              style="display:flex;align-items:flex-start;gap:10px;padding:6px 8px;background:#fafafa;border-radius:4px"
            >
              <Badge label={item.status} tone={statusToneMap[item.status]} />
              <div style="flex:1">
                {item.label && (
                  <div style="font-size:0.85rem;color:#333;font-weight:600">
                    {item.label}
                  </div>
                )}
                {item.message && (
                  <div style="font-size:0.85rem;color:#555">{item.message}</div>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
