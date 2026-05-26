import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type SelectImportDialogCandidate = {
  label: string;
  value: string;
  description?: string;
};

export type SelectImportDialogProps = ComponentIdentityProps & {
  open: boolean;
  title?: string;
  candidates?: SelectImportDialogCandidate[];
  selectedValue?: string;
  onSelect?: (value: string) => void;
  onSubmit: () => void;
  onCancel: () => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function SelectImportDialog({
  open,
  title,
  candidates,
  selectedValue,
  onSelect,
  onSubmit,
  onCancel,
  className,
  design,
}: SelectImportDialogProps): JSX.Element | null {
  if (!open) return null;
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "background:#fff;border-radius:8px;padding:24px;max-width:560px;width:100%;box-shadow:0 4px 24px rgba(0,0,0,0.18);font-family:monospace",
    design,
  );
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title ?? "Select Import"}
      style="position:fixed;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,0.4);z-index:1000"
      onClick={(e) => {
        if (e.target === e.currentTarget) onCancel();
      }}
    >
      <div className={mergedClassName} style={mergedStyle}>
        {title && (
          <h2 style="margin:0 0 12px;font-size:1.05rem;color:#222">{title}</h2>
        )}
        {candidates && candidates.length > 0 && (
          <ul style="list-style:none;margin:0 0 16px;padding:0;max-height:320px;overflow-y:auto;border:1px solid #e8e8e8;border-radius:4px">
            {candidates.map((c) => (
              <li
                key={c.value}
                onClick={() => onSelect && onSelect(c.value)}
                style={`padding:10px 14px;cursor:pointer;border-bottom:1px solid #f0f0f0;background:${
                  selectedValue === c.value ? "#e8f0fe" : "#fff"
                };color:${selectedValue === c.value ? "#1a73e8" : "#222"}`}
              >
                <div style="font-size:0.9rem;font-weight:500">{c.label}</div>
                {c.description && (
                  <div style="font-size:0.78rem;color:#888;margin-top:2px">
                    {c.description}
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
        {(!candidates || candidates.length === 0) && (
          <p style="font-size:0.875rem;color:#888;margin:0 0 16px">
            No candidates available.
          </p>
        )}
        <div style="display:flex;gap:8px;justify-content:flex-end">
          <button
            type="button"
            onClick={onCancel}
            style="padding:6px 16px;border:1px solid #ccc;border-radius:4px;background:#fff;cursor:pointer;font-family:monospace"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={onSubmit}
            disabled={selectedValue === undefined || selectedValue === ""}
            style={`padding:6px 16px;border-radius:4px;cursor:pointer;font-family:monospace;border:1px solid ${
              selectedValue ? "#1a73e8" : "#ccc"
            };background:${selectedValue ? "#1a73e8" : "#f5f5f5"};color:${
              selectedValue ? "#fff" : "#aaa"
            }`}
          >
            Import
          </button>
        </div>
      </div>
    </div>
  );
}
