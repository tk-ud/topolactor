import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type BackgroundColorEditorProps = ComponentIdentityProps & {
  value?: string;
  label?: string;
  onChange: (value: string) => void;
  tokens?: { key: string; value: string; label?: string }[];
  className?: string;
  design?: ComponentDesignParams;
};

export function BackgroundColorEditor({
  value,
  label,
  onChange,
  tokens,
  className,
  design,
}: BackgroundColorEditorProps): JSX.Element {
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
          value={value ?? "#ffffff"}
          onInput={(e) => onChange((e.target as HTMLInputElement).value)}
          style="width:38px;height:32px;padding:1px;border:1px solid #ccc;border-radius:4px;cursor:pointer;background:none"
        />
        <input
          type="text"
          value={value ?? ""}
          onInput={(e) => onChange((e.target as HTMLInputElement).value)}
          placeholder="#ffffff or rgba(0,0,0,0)"
          style="font-family:monospace;font-size:0.9rem;padding:5px 8px;border:1px solid #ccc;border-radius:4px;flex:1;box-sizing:border-box"
        />
      </div>
      {value && (
        <div
          style={`height:28px;border-radius:4px;border:1px solid #e0e0e0;background:${value}`}
        />
      )}
      {tokens && tokens.length > 0 && (
        <div style="display:flex;flex-wrap:wrap;gap:6px;margin-top:2px">
          {tokens.map((t) => (
            <button
              key={t.key}
              type="button"
              onClick={() => onChange(t.value)}
              style={`padding:3px 9px;border-radius:4px;cursor:pointer;font-family:monospace;font-size:0.8rem;border:1px solid ${
                value === t.value ? "#1a73e8" : "#ddd"
              };background:${
                value === t.value ? "#e8f0fe" : "#fff"
              };color:${value === t.value ? "#1a73e8" : "#333"}`}
            >
              {t.label ?? t.key}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
