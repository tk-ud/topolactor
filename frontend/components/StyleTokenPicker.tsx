import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type StyleToken = { key: string; value: string; label?: string };

export type StyleTokenPickerProps = ComponentIdentityProps & {
  value?: string;
  tokens?: StyleToken[];
  onSelect: (token: StyleToken) => void;
  label?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function StyleTokenPicker({
  value,
  tokens = [],
  onSelect,
  label,
  className,
  design,
}: StyleTokenPickerProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:6px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {label && (
        <div style="font-size:0.85rem;color:#555;font-weight:600">{label}</div>
      )}
      <div style="display:flex;flex-wrap:wrap;gap:6px">
        {tokens.length === 0
          ? (
            <span style="color:#bbb;font-size:0.85rem">
              No tokens available.
            </span>
          )
          : tokens.map((t) => (
            <button
              key={t.key}
              onClick={() => onSelect(t)}
              style={`padding:4px 10px;border-radius:4px;cursor:pointer;font-family:monospace;font-size:0.82rem;border:2px solid ${
                value === t.key ? "#1a73e8" : "#ddd"
              };background:${
                value === t.key ? "#e8f0fe" : "#fff"
              };color:${value === t.key ? "#1a73e8" : "#333"}`}
            >
              {t.label ?? t.key}
            </button>
          ))}
      </div>
    </div>
  );
}
