import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type FontTokenEditorProps = ComponentIdentityProps & {
  value?: string;
  label?: string;
  onChange: (value: string) => void;
  tokens?: { key: string; value: string; label?: string }[];
  className?: string;
  design?: ComponentDesignParams;
};

export function FontTokenEditor({
  value,
  label,
  onChange,
  tokens,
  className,
  design,
}: FontTokenEditorProps): JSX.Element {
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
        placeholder="e.g. Inter, sans-serif"
        style="font-family:monospace;font-size:0.9rem;padding:5px 8px;border:1px solid #ccc;border-radius:4px;width:100%;box-sizing:border-box"
      />
      {value && (
        <div
          style={`font-size:0.9rem;padding:4px 8px;border:1px solid #e8e8e8;border-radius:4px;background:#fafafa;font-family:${value}`}
        >
          The quick brown fox — {value}
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
