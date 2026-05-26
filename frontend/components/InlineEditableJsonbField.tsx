import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type InlineEditableJsonbFieldProps = ComponentIdentityProps & {
  value?: Record<string, unknown>;
  editing?: boolean;
  label?: string;
  disabled?: boolean;
  onChange: (raw: string) => void;
  onToggle?: (editing: boolean) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function InlineEditableJsonbField({
  value,
  editing = false,
  label,
  disabled,
  onChange,
  onToggle,
  className,
  design,
}: InlineEditableJsonbFieldProps): JSX.Element {
  const isDisabled = disabled || design?.state === "loading";
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:4px;font-family:monospace",
    design,
  );
  const raw = value !== undefined ? JSON.stringify(value, null, 2) : "";
  const preview = raw.length > 120 ? raw.slice(0, 120) + "…" : raw;
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {label && (
        <span style="font-size:0.75rem;color:#888">{label}</span>
      )}
      {editing
        ? (
          <textarea
            rows={6}
            disabled={isDisabled}
            autoFocus
            onInput={(e) => onChange((e.target as HTMLTextAreaElement).value)}
            onBlur={onToggle ? () => onToggle(false) : undefined}
            style="padding:6px 8px;border:1px solid #aaa;border-radius:4px;font-family:monospace;font-size:0.85rem;resize:vertical;width:100%;box-sizing:border-box"
          >
            {raw}
          </textarea>
        )
        : (
          <pre
            onClick={onToggle && !isDisabled ? () => onToggle(true) : undefined}
            style={`background:#f8f9fa;border:1px solid #e0e0e0;border-radius:4px;padding:6px 10px;font-family:monospace;font-size:0.82rem;color:#333;cursor:${isDisabled ? "default" : "text"};white-space:pre-wrap;word-break:break-all;margin:0;overflow:hidden`}
          >
            {raw ? preview : <span style="color:#ccc">null</span>}
          </pre>
        )}
    </div>
  );
}
