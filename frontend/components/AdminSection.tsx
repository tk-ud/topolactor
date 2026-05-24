import { ComponentChildren, JSX } from "preact";
import { Badge, type BadgeTone } from "./Badge.tsx";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type AdminSectionProps = ComponentIdentityProps & {
  title?: string;
  description?: string;
  status?: string;
  statusTone?: BadgeTone;
  actions?: ComponentChildren;
  children: ComponentChildren;
  className?: string;
  design?: ComponentDesignParams;
};

export function AdminSection({
  title,
  description,
  status,
  statusTone = "neutral",
  actions,
  children,
  className,
  design,
}: AdminSectionProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "border:1px solid #e0e0e0;border-radius:6px;padding:16px;margin-bottom:16px;font-family:monospace",
    design,
  );
  return (
    <section className={mergedClassName} style={mergedStyle}>
      {(title || status || actions) && (
        <div style="display:flex;align-items:center;gap:10px;margin-bottom:12px;flex-wrap:wrap">
          {title && (
            <h2 style="margin:0;font-size:1rem;color:#333;font-weight:600">{title}</h2>
          )}
          {status && <Badge label={status} tone={statusTone} />}
          {description && !title && (
            <p style="margin:0;font-size:0.85rem;color:#666">{description}</p>
          )}
          {actions && <div style="margin-left:auto">{actions}</div>}
        </div>
      )}
      {description && title && (
        <p style="margin:0 0 12px;font-size:0.85rem;color:#666">{description}</p>
      )}
      <div>{children}</div>
    </section>
  );
}
