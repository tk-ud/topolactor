import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type RecentInputSuggestProps = ComponentIdentityProps & {
  value: string;
  onChange: (value: string) => void;
  onSelect?: (value: string) => void;
  recentItems?: string[];
  placeholder?: string;
  disabled?: boolean;
  label?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function RecentInputSuggest({
  value,
  onChange,
  onSelect,
  recentItems,
  placeholder,
  disabled,
  label,
  className,
  design,
}: RecentInputSuggestProps): JSX.Element {
  const isDisabled = disabled || design?.state === "loading";
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:6px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {label && (
        <label style="font-size:0.85rem;color:#555">{label}</label>
      )}
      <input
        type="text"
        value={value}
        placeholder={placeholder}
        disabled={isDisabled}
        onInput={(e) => onChange((e.target as HTMLInputElement).value)}
        style="padding:6px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.9rem"
      />
      {recentItems && recentItems.length > 0 && (
        <div style="display:flex;flex-wrap:wrap;gap:4px">
          {recentItems.map((item) => (
            <button
              key={item}
              type="button"
              disabled={isDisabled}
              onClick={() => {
                onChange(item);
                if (onSelect) onSelect(item);
              }}
              style={`background:#f1f3f4;color:#444;border:1px solid #ddd;border-radius:12px;padding:2px 10px;font-family:monospace;font-size:0.8rem;cursor:${isDisabled ? "not-allowed" : "pointer"};opacity:${isDisabled ? 0.6 : 1}`}
            >
              {item}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
