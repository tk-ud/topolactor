import { ComponentChildren, JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type BreadcrumbItem = {
  label: string;
  href?: string;
};

export type AdminPageShellProps = ComponentIdentityProps & {
  title: string;
  description?: string;
  breadcrumbs?: BreadcrumbItem[];
  actions?: ComponentChildren;
  children: ComponentChildren;
  className?: string;
  design?: ComponentDesignParams;
};

export function AdminPageShell({
  title,
  description,
  breadcrumbs,
  actions,
  children,
  className,
  design,
}: AdminPageShellProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "font-family:monospace;max-width:1200px;margin:0 auto;padding:24px",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {breadcrumbs && breadcrumbs.length > 0 && (
        <nav aria-label="Breadcrumb" style="font-size:0.8rem;color:#888;margin-bottom:12px">
          {breadcrumbs.map((crumb, i) => (
            <span key={i}>
              {i > 0 && <span style="margin:0 6px">/</span>}
              {crumb.href
                ? <a href={crumb.href} style="color:#0070f3;text-decoration:none">{crumb.label}</a>
                : <span>{crumb.label}</span>}
            </span>
          ))}
        </nav>
      )}
      <div style="display:flex;align-items:flex-start;justify-content:space-between;gap:16px;margin-bottom:16px">
        <div>
          <h1 style="margin:0 0 4px;font-size:1.4rem;color:#222">{title}</h1>
          {description && (
            <p style="margin:0;font-size:0.9rem;color:#666">{description}</p>
          )}
        </div>
        {actions && <div style="flex-shrink:0">{actions}</div>}
      </div>
      <div>{children}</div>
    </div>
  );
}
