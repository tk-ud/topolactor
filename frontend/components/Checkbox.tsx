import { JSX } from "preact";
import {
  computeDesignDisabled,
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type CheckboxProps = ComponentIdentityProps & {
  checked: boolean;
  onChange: (checked: boolean) => void;
  onFocus?: () => void;
  onBlur?: () => void;
  label?: string;
  disabled?: boolean;
  required?: boolean;
  className?: string;
  design?: ComponentDesignParams;
};

export function Checkbox({
  checked,
  onChange,
  onFocus,
  onBlur,
  label,
  disabled,
  required,
  className,
  design,
}: CheckboxProps): JSX.Element {
  const computedDisabled = computeDesignDisabled(disabled, design);
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;align-items:center;gap:8px;font-family:monospace;cursor:pointer",
    design,
  );
  return (
    <label className={mergedClassName} style={mergedStyle}>
      <input
        type="checkbox"
        checked={checked}
        disabled={computedDisabled}
        required={required}
        onFocus={onFocus}
        onBlur={onBlur}
        onChange={(e) => onChange((e.target as HTMLInputElement).checked)}
        style="cursor:pointer"
      />
      {label && (
        <span style={`font-size:0.9rem;color:${computedDisabled ? "#aaa" : "#333"}`}>
          {label}
        </span>
      )}
    </label>
  );
}
