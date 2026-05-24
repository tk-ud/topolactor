import { ComponentChildren, JSX } from "preact";
import type { ComponentDesignParams } from "./Button.tsx";

export type CardProps = {
  title?: string;
  children: ComponentChildren;
  footer?: ComponentChildren;
  variant?: "default" | "info" | "warning" | "error";
  onClick?: () => void;
  className?: string;
  design?: ComponentDesignParams;
};

const variantBorder: Record<string, string> = {
  default: "#ccc",
  info: "#0070f3",
  warning: "#f5a623",
  error: "#e00",
};

export function Card(
  { title, children, footer, variant = "default", onClick, className, design }:
    CardProps,
): JSX.Element {
  const designStyle = [design?.style].filter(Boolean).join(";");
  return (
    <section
      onClick={onClick}
      className={[
        className,
        design?.classname,
        design?.className,
        design?.tailwind,
      ].filter(Boolean).join(" ") || undefined}
      style={`border:1px solid ${
        variantBorder[variant]
      };border-radius:6px;padding:16px;margin-bottom:16px;font-family:monospace;${designStyle}`}
    >
      {title && (
        <h3 style="margin-top:0;margin-bottom:12px;font-size:1rem;color:#333">
          {title}
        </h3>
      )}
      <div>{children}</div>
      {footer && (
        <div style="margin-top:12px;padding-top:8px;border-top:1px solid #eee;color:#666;font-size:0.85rem">
          {footer}
        </div>
      )}
    </section>
  );
}
