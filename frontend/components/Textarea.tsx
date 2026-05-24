import { JSX } from "preact";
import {
  computeDesignDisabled,
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type TextareaProps = ComponentIdentityProps & {
  value: string;
  onChange: (value: string) => void;
  onFocus?: () => void;
  onBlur?: () => void;
  placeholder?: string;
  disabled?: boolean;
  required?: boolean;
  rows?: number;
  label?: string;
  error?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function Textarea({
  value,
  onChange,
  onFocus,
  onBlur,
  placeholder,
  disabled,
  required,
  rows = 4,
  label,
  error,
  className,
  design,
}: TextareaProps): JSX.Element {
  const computedDisabled = computeDesignDisabled(disabled, design);
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:4px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {label && (
        <label style="font-size:0.85rem;color:#555">
          {label}
          {required && (
            <span style="color:#e00;margin-left:2px" aria-label="required">
              *
            </span>
          )}
        </label>
      )}
      <textarea
        value={value}
        rows={rows}
        disabled={computedDisabled}
        required={required}
        placeholder={placeholder}
        onFocus={onFocus}
        onBlur={onBlur}
        onInput={(e) => onChange((e.target as HTMLTextAreaElement).value)}
        style="padding:6px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.9rem;resize:vertical"
      />
      {error && (
        <span role="alert" style="font-size:0.8rem;color:#e00">
          {error}
        </span>
      )}
    </div>
  );
}
