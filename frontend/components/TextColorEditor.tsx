import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type TextColorEditorProps = ComponentIdentityProps & {
  value?: string;
  label?: string;
  onChange: (value: string) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function TextColorEditor({
  value,
  label,
  onChange,
  className,
  design,
}: TextColorEditorProps): JSX.Element {
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
      <div style="display:flex;align-items:center;gap:8px">
        <input
          type="color"
          value={value ?? "#000000"}
          onInput={(e) => onChange((e.target as HTMLInputElement).value)}
          style="width:38px;height:32px;padding:1px;border:1px solid #ccc;border-radius:4px;cursor:pointer;background:none"
        />
        <input
          type="text"
          value={value ?? ""}
          onInput={(e) => onChange((e.target as HTMLInputElement).value)}
          placeholder="#000000 or rgba(0,0,0,1)"
          style="font-family:monospace;font-size:0.9rem;padding:5px 8px;border:1px solid #ccc;border-radius:4px;flex:1;box-sizing:border-box"
        />
      </div>
      {value && (
        <div
          style={`font-size:0.9rem;padding:4px 8px;border:1px solid #e8e8e8;border-radius:4px;background:#fafafa;color:${value};font-family:monospace`}
        >
          Sample text — {value}
        </div>
      )}
    </div>
  );
}
