import { ComponentChildren, JSX } from "preact";
import {
  computeDesignDisabled,
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type FormFieldProps = ComponentIdentityProps & {
  label?: string;
  required?: boolean;
  error?: string;
  help?: string;
  disabled?: boolean;
  className?: string;
  design?: ComponentDesignParams;
  children: ComponentChildren;
};

export function FormField({
  label,
  required,
  error,
  help,
  disabled,
  className,
  design,
  children,
}: FormFieldProps): JSX.Element {
  const computedDisabled = computeDesignDisabled(disabled, design);
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:4px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {label && (
        <label
          style={`font-size:0.85rem;color:${computedDisabled ? "#aaa" : "#555"}`}
        >
          {label}
          {required && (
            <span style="color:#e00;margin-left:2px" aria-label="required">
              *
            </span>
          )}
        </label>
      )}
      {children}
      {error && (
        <span role="alert" style="font-size:0.8rem;color:#e00">
          {error}
        </span>
      )}
      {help && !error && (
        <span style="font-size:0.8rem;color:#888">{help}</span>
      )}
    </div>
  );
}
