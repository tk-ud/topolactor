import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type BorderRadiusEditorProps = ComponentIdentityProps & {
  value?: string;
  label?: string;
  onChange: (value: string) => void;
  tokens?: { key: string; value: string; label?: string }[];
  className?: string;
  design?: ComponentDesignParams;
};

export function BorderRadiusEditor({
  value,
  label,
  onChange,
  tokens,
  className,
  design,
}: BorderRadiusEditorProps): JSX.Element {
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
        placeholder="e.g. 4px, 0.5rem, 50%"
        style="font-family:monospace;font-size:0.9rem;padding:5px 8px;border:1px solid #ccc;border-radius:4px;width:100%;box-sizing:border-box"
      />
      {value && (
        <div style="display:flex;align-items:center;gap:8px">
          <div
            style={`width:40px;height:40px;background:#e3f2fd;border:2px solid #90caf9;border-radius:${value}`}
          />
          <span style="font-size:0.8rem;color:#666">{value}</span>
        </div>
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
