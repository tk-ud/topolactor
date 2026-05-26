import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type InlineEditableFieldProps = ComponentIdentityProps & {
  value: string;
  editing?: boolean;
  label?: string;
  placeholder?: string;
  disabled?: boolean;
  onChange: (value: string) => void;
  onToggle?: (editing: boolean) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function InlineEditableField({
  value,
  editing = false,
  label,
  placeholder,
  disabled = false,
  onChange,
  onToggle,
  className,
  design,
}: InlineEditableFieldProps): JSX.Element {
  const isDisabled = disabled || design?.state === "loading";
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:inline-flex;flex-direction:column;gap:2px;font-family:monospace",
    design,
  );
  return (
    <span className={mergedClassName} style={mergedStyle}>
      {label && <span style="font-size:0.75rem;color:#888">{label}</span>}
      {editing
        ? (
          <input
            type="text"
            value={value}
            onInput={(e) => onChange((e.target as HTMLInputElement).value)}
            onBlur={onToggle ? () => onToggle(false) : undefined}
            placeholder={placeholder}
            disabled={isDisabled}
            autoFocus
            style="padding:2px 6px;border:1px solid #aaa;border-radius:3px;font-family:monospace;font-size:0.9rem"
          />
        )
        : (
          <span
            onClick={onToggle && !isDisabled ? () => onToggle(true) : undefined}
            style={`font-size:0.9rem;color:#222;padding:2px 6px;border-radius:3px;cursor:${isDisabled ? "default" : "text"};border:1px solid transparent`}
          >
            {value || <span style="color:#bbb">{placeholder ?? "—"}</span>}
          </span>
        )}
    </span>
  );
}
