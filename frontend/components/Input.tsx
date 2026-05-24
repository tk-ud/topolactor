import { ComponentChildren, JSX } from "preact";
import type { ComponentDesignParams } from "./Button.tsx";

export type InputProps = {
  value: string;
  onChange: (value: string) => void;
  onFocus?: () => void;
  onBlur?: () => void;
  placeholder?: string;
  disabled?: boolean;
  label?: string;
  type?: "text" | "password" | "number" | "email";
  className?: string;
  design?: ComponentDesignParams;
  children?: ComponentChildren;
};

export function Input(
  {
    value,
    onChange,
    onFocus,
    onBlur,
    placeholder,
    disabled = false,
    label,
    type = "text",
    className,
    design,
    children,
  }: InputProps,
): JSX.Element {
  const computedDisabled = disabled || design?.state === "loading";
  const containerClassName =
    [className, design?.classname, design?.className, design?.tailwind].filter(
      Boolean,
    ).join(" ") || undefined;
  const designStyle = [design?.style].filter(Boolean).join(";");
  return (
    <div
      className={containerClassName}
      style={`display:flex;flex-direction:column;gap:4px;font-family:monospace;${designStyle}`}
    >
      {label && <label style="font-size:0.85rem;color:#555">{label}</label>}
      <input
        type={type}
        value={value}
        onInput={(e) => onChange((e.target as HTMLInputElement).value)}
        onFocus={onFocus}
        onBlur={onBlur}
        placeholder={placeholder}
        disabled={computedDisabled}
        style="padding:6px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.9rem"
      />
      {children}
    </div>
  );
}
