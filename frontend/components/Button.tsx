import { ComponentChildren, JSX } from "preact";

export type ComponentDesignParams = {
  className?: string;
  style?: string;
  tailwind?: string;
  state?: "default" | "loading" | "success" | "error";
};

export type ButtonProps = {
  label: string;
  onClick?: () => void;
  disabled?: boolean;
  variant?: "primary" | "secondary" | "danger";
  type?: "button" | "submit" | "reset";
  className?: string;
  design?: ComponentDesignParams;
  children?: ComponentChildren;
};

const variantStyles: Record<string, string> = {
  primary: "background:#0070f3;color:#fff;border:none",
  secondary: "background:#eee;color:#333;border:1px solid #ccc",
  danger: "background:#e00;color:#fff;border:none",
};

export function Button({
  label,
  onClick,
  disabled = false,
  variant = "primary",
  type = "button",
  className,
  design,
  children,
}: ButtonProps): JSX.Element {
  const designStyle = [design?.tailwind, design?.style].filter(Boolean).join(";");
  return (
    <button
      type={type}
      onClick={onClick}
      disabled={disabled || design?.state === "loading"}
      className={className ?? design?.className}
      style={`${variantStyles[variant]};padding:6px 14px;border-radius:4px;cursor:${disabled ? "not-allowed" : "pointer"};opacity:${disabled ? 0.6 : 1};font-family:monospace;${designStyle}`}
    >
      {children ?? label}
    </button>
  );
}
