import { ComponentChildren, JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ModalProps = ComponentIdentityProps & {
  open: boolean;
  title?: string;
  description?: string;
  onClose: () => void;
  footer?: ComponentChildren;
  children: ComponentChildren;
  className?: string;
  design?: ComponentDesignParams;
};

export function Modal({
  open,
  title,
  description,
  onClose,
  footer,
  children,
  className,
  design,
}: ModalProps): JSX.Element | null {
  if (!open) return null;
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "background:#fff;border-radius:8px;padding:24px;max-width:600px;width:100%;box-shadow:0 4px 24px rgba(0,0,0,0.18);font-family:monospace",
    design,
  );
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title}
      style="position:fixed;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,0.4);z-index:1000"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className={mergedClassName} style={mergedStyle}>
        <div style="display:flex;align-items:flex-start;justify-content:space-between;margin-bottom:12px">
          <div>
            {title && (
              <h2 style="margin:0 0 4px;font-size:1.1rem;color:#222">{title}</h2>
            )}
            {description && (
              <p style="margin:0;font-size:0.85rem;color:#666">{description}</p>
            )}
          </div>
          <button
            onClick={onClose}
            aria-label="Close"
            style="background:none;border:none;font-size:1.2rem;cursor:pointer;color:#888;padding:0 0 0 16px"
          >
            ✕
          </button>
        </div>
        <div>{children}</div>
        {footer && (
          <div style="margin-top:16px;padding-top:12px;border-top:1px solid #eee">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
}
