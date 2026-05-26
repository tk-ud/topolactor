import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ValidationErrorEntry = {
  message: string;
  field?: string;
  code?: string;
};

export type ValidationErrorPanelProps = ComponentIdentityProps & {
  errors?: ValidationErrorEntry[];
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function ValidationErrorPanel({
  errors = [],
  title,
  className,
  design,
}: ValidationErrorPanelProps): JSX.Element | null {
  if (errors.length === 0) return null;
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "border:1px solid #f5c6c4;border-radius:6px;padding:12px;background:#fce8e6;font-family:monospace",
    design,
  );
  return (
    <div role="alert" className={mergedClassName} style={mergedStyle}>
      {title && (
        <div style="font-weight:600;color:#c5221f;margin-bottom:6px;font-size:0.9rem">
          {title}
        </div>
      )}
      <ul style="margin:0;padding:0 0 0 16px">
        {errors.map((e, i) => (
          <li
            key={e.code ?? String(i)}
            style="color:#c5221f;font-size:0.875rem;margin-bottom:2px"
          >
            {e.field && <span style="font-weight:600">{e.field}: </span>}
            {e.message}
          </li>
        ))}
      </ul>
    </div>
  );
}
