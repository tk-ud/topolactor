import { JSX } from "preact";

export type ButtonProps = {
  label: string;
  onClick?: () => void;
  disabled?: boolean;
  variant?: "primary" | "secondary" | "danger";
  type?: "button" | "submit" | "reset";
};

const variantStyles: Record<string, string> = {
  primary:   "background:#0070f3;color:#fff;border:none",
  secondary: "background:#eee;color:#333;border:1px solid #ccc",
  danger:    "background:#e00;color:#fff;border:none",
};

/**
 * Primitive Button component.
 * DB-registered primitive — not code-only. See db/ui_topology_tables.sql ui_component_registry.
 * component_key: "button.primitive"
 */
export function Button({
  label,
  onClick,
  disabled = false,
  variant = "primary",
  type = "button",
}: ButtonProps): JSX.Element {
  return (
    <button
      type={type}
      onClick={onClick}
      disabled={disabled}
      style={`${variantStyles[variant]};padding:6px 14px;border-radius:4px;cursor:${disabled ? "not-allowed" : "pointer"};opacity:${disabled ? 0.6 : 1};font-family:monospace`}
    >
      {label}
    </button>
  );
}
