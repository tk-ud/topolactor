import { ComponentChildren, JSX } from "preact";

export type CardProps = {
  title?: string;
  children: ComponentChildren;
  footer?: ComponentChildren;
  variant?: "default" | "info" | "warning" | "error";
};

const variantBorder: Record<string, string> = {
  default: "#ccc",
  info:    "#0070f3",
  warning: "#f5a623",
  error:   "#e00",
};

/**
 * Primitive Card component.
 * Code-only primitive (drift). DB registration pending via ui_component_bucket → package generator flow.
 * component_key: "card.primitive"
 */
export function Card({
  title,
  children,
  footer,
  variant = "default",
}: CardProps): JSX.Element {
  return (
    <section
      style={`border:1px solid ${variantBorder[variant]};border-radius:6px;padding:16px;margin-bottom:16px;font-family:monospace`}
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
