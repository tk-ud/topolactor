import { ComponentChildren, JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ApplyConfirmDialogProps = ComponentIdentityProps & {
  open: boolean;
  title?: string;
  description?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  onConfirm: () => void;
  onCancel: () => void;
  children?: ComponentChildren;
  className?: string;
  design?: ComponentDesignParams;
};

export function ApplyConfirmDialog({
  open,
  title,
  description,
  confirmLabel = "Apply",
  cancelLabel = "Cancel",
  onConfirm,
  onCancel,
  children,
  className,
  design,
}: ApplyConfirmDialogProps): JSX.Element | null {
  if (!open) return null;
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "background:#fff;border-radius:8px;padding:24px;max-width:480px;width:100%;box-shadow:0 4px 24px rgba(0,0,0,0.18);font-family:monospace",
    design,
  );
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title ?? "Confirm"}
      style="position:fixed;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,0.4);z-index:1000"
      onClick={(e) => {
        if (e.target === e.currentTarget) onCancel();
      }}
    >
      <div className={mergedClassName} style={mergedStyle}>
        {title && (
          <h2 style="margin:0 0 8px;font-size:1.05rem;color:#222">{title}</h2>
        )}
        {description && (
          <p style="margin:0 0 12px;font-size:0.875rem;color:#555">
            {description}
          </p>
        )}
        {children && <div style="margin-bottom:12px">{children}</div>}
        <div style="display:flex;gap:8px;justify-content:flex-end">
          <button
            onClick={onCancel}
            style="padding:6px 16px;border:1px solid #ccc;border-radius:4px;background:#fff;cursor:pointer;font-family:monospace"
          >
            {cancelLabel}
          </button>
          <button
            onClick={onConfirm}
            style="padding:6px 16px;border:1px solid #c5221f;border-radius:4px;background:#c5221f;color:#fff;cursor:pointer;font-family:monospace"
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
