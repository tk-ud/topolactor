import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ShadowTokenEditorProps = ComponentIdentityProps & {
  value?: string;
  label?: string;
  onChange: (value: string) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function ShadowTokenEditor({
  value,
  label,
  onChange,
  className,
  design,
}: ShadowTokenEditorProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:6px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {label && (
        <label style="font-size:0.85rem;color:#555;font-weight:600">
          {label}
        </label>
      )}
      <input
        type="text"
        value={value ?? ""}
        onInput={(e) => onChange((e.target as HTMLInputElement).value)}
        placeholder="e.g. 0 2px 8px rgba(0,0,0,0.15)"
        style="font-family:monospace;font-size:0.9rem;padding:5px 8px;border:1px solid #ccc;border-radius:4px;width:100%;box-sizing:border-box"
      />
      <div style="display:flex;align-items:center;gap:12px;padding:12px 0">
        <div
          style={`width:56px;height:56px;background:#fff;border:1px solid #e8e8e8;border-radius:6px;box-shadow:${value ?? "none"}`}
        />
        <span style="font-size:0.8rem;color:#888;font-family:monospace">
          {value ? value : "no shadow"}
        </span>
      </div>
    </div>
  );
}
