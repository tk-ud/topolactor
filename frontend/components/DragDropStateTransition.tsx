import { JSX } from "preact";
import { mergeDesignClassName, mergeDesignStyle, type ComponentDesignParams, type ComponentIdentityProps } from "./types.ts";

export type DragDropStateTransitionProps = ComponentIdentityProps & {
  title?: string;
  value?: string;
  items?: unknown[];
  preview?: unknown;
  className?: string;
  design?: ComponentDesignParams;
  onChange?: (value: string) => void;
  onSelect?: (value: string) => void;
  onConfirm?: () => void;
};

export function DragDropStateTransition({ title, value, items, preview, className, design, onChange, onSelect, onConfirm }: DragDropStateTransitionProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle("border:1px solid #ddd;border-radius:6px;padding:12px;font-family:monospace", design);
  return (
    <section className={mergedClassName} style={mergedStyle}>
      <div style="font-weight:600;margin-bottom:8px">{title ?? "DragDropStateTransition"}</div>
      {onChange && <input value={value ?? ""} onInput={(e) => onChange((e.currentTarget as HTMLInputElement).value)} style="width:100%;margin-bottom:8px" />}
      {Array.isArray(items) && items.length > 0 && (
        <ul style="margin:0 0 8px 16px;padding:0">
          {items.map((item, idx) => <li key={idx} onClick={() => onSelect?.(String(idx))}>{typeof item === "string" ? item : JSON.stringify(item)}</li>)}
        </ul>
      )}
      {preview !== undefined && <pre style="margin:0 0 8px;background:#f8f8f8;padding:8px">{JSON.stringify(preview, null, 2)}</pre>}
      {onConfirm && <button type="button" onClick={() => onConfirm()}>Confirm</button>}
      <div style="font-size:0.78rem;color:#666;margin-top:6px">kanban drag operation preview</div>
    </section>
  );
}
