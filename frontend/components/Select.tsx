import { JSX } from "preact";
import {
  computeDesignDisabled,
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type SelectOption = {
  value: string;
  label: string;
  disabled?: boolean;
};

export type SelectProps = ComponentIdentityProps & {
  value: string;
  options: SelectOption[];
  onChange: (value: string) => void;
  onFocus?: () => void;
  onBlur?: () => void;
  placeholder?: string;
  disabled?: boolean;
  required?: boolean;
  label?: string;
  error?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function Select({
  value,
  options,
  onChange,
  onFocus,
  onBlur,
  placeholder,
  disabled,
  required,
  label,
  error,
  className,
  design,
}: SelectProps): JSX.Element {
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
      <select
        value={value}
        disabled={computedDisabled}
        required={required}
        onFocus={onFocus}
        onBlur={onBlur}
        onChange={(e) => onChange((e.target as HTMLSelectElement).value)}
        style="padding:6px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.9rem;background:#fff"
      >
        {placeholder && (
          // round 38: the placeholder option is only disabled when this select is REQUIRED
          // (forcing a real choice, unchanged prior behavior for every existing required
          // select). For a non-required select -- e.g. an optional filter such as
          // enum_group_filter -- disabling it left NO way for a real browser user to ever
          // reach the empty/"no filter" value: a disabled <option> cannot be selected via
          // native <select> UI at all, only by directly assigning .value from test/script
          // code, which is not a real user interaction. Leaving it enabled for a
          // non-required select makes it a genuine, native, keyboard/mouse-reachable
          // clear/all option — no separate dedicated "Clear" control needed.
          <option value="" disabled={required}>
            {placeholder}
          </option>
        )}
        {options.map((opt) => (
          <option key={opt.value} value={opt.value} disabled={opt.disabled}>
            {opt.label}
          </option>
        ))}
      </select>
      {error && (
        <span role="alert" style="font-size:0.8rem;color:#e00">
          {error}
        </span>
      )}
    </div>
  );
}
