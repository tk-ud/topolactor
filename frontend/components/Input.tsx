import { JSX } from "preact";

export type InputProps = {
  value: string;
  onChange: (value: string) => void;
  onFocus?: () => void;
  onBlur?: () => void;
  placeholder?: string;
  disabled?: boolean;
  label?: string;
  type?: "text" | "password" | "number" | "email";
};

/**
 * Primitive Input component.
 * Code-only primitive (drift). DB registration pending via ui_component_bucket → package generator flow.
 * component_key: "input.primitive"
 */
export function Input({
  value,
  onChange,
  onFocus,
  onBlur,
  placeholder,
  disabled = false,
  label,
  type = "text",
}: InputProps): JSX.Element {
  return (
    <div style="display:flex;flex-direction:column;gap:4px;font-family:monospace">
      {label && <label style="font-size:0.85rem;color:#555">{label}</label>}
      <input
        type={type}
        value={value}
        onInput={(e) => onChange((e.target as HTMLInputElement).value)}
        onFocus={onFocus}
        onBlur={onBlur}
        placeholder={placeholder}
        disabled={disabled}
        style="padding:6px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.9rem"
      />
    </div>
  );
}
