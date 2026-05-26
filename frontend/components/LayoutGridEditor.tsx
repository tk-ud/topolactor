import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type LayoutGridEditorProps = ComponentIdentityProps & {
  value?: string;
  label?: string;
  onChange?: (value: string) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function LayoutGridEditor({
  value,
  label,
  onChange,
  className,
  design,
}: LayoutGridEditorProps): JSX.Element {
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
        onInput={onChange
          ? (e) => onChange((e.target as HTMLInputElement).value)
          : undefined}
        placeholder='e.g. 12-col | gap:16px'
        style="font-family:monospace;font-size:0.9rem;padding:5px 8px;border:1px solid #ccc;border-radius:4px;width:100%;box-sizing:border-box"
      />
      {value && (
        <div style="font-size:0.8rem;color:#888;padding:2px 0">
          <span style="color:#555;font-weight:500">grid: </span>
          <span style="color:#0070f3">{value}</span>
        </div>
      )}
    </div>
  );
}
