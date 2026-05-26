import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type SpacingTokenEditorProps = ComponentIdentityProps & {
  value?: string;
  label?: string;
  onChange: (value: string) => void;
  tokens?: { key: string; value: string; label?: string }[];
  className?: string;
  design?: ComponentDesignParams;
};

export function SpacingTokenEditor({
  value,
  label,
  onChange,
  tokens,
  className,
  design,
}: SpacingTokenEditorProps): JSX.Element {
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
        placeholder="e.g. 8px, 1rem, 0.5em"
        style="font-family:monospace;font-size:0.9rem;padding:5px 8px;border:1px solid #ccc;border-radius:4px;width:100%;box-sizing:border-box"
      />
      {value && (
        <div style="display:flex;align-items:center;gap:8px">
          <div
            style={`width:${value};height:16px;background:#c8e6c9;border-radius:2px;min-width:4px;max-width:200px`}
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
