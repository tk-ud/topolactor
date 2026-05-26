import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type FacetedFilterBarFilter = {
  key: string;
  label: string;
  value: string;
  type?: "text" | "select";
  options?: { label: string; value: string }[];
};

export type FacetedFilterBarProps = ComponentIdentityProps & {
  filters?: FacetedFilterBarFilter[];
  onChange: (key: string, value: string) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function FacetedFilterBar({
  filters = [],
  onChange,
  className,
  design,
}: FacetedFilterBarProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:row;flex-wrap:wrap;gap:12px;align-items:flex-end;font-family:monospace;padding:8px 0",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {filters.map((filter) => (
        <div
          key={filter.key}
          style="display:flex;flex-direction:column;gap:4px"
        >
          <label style="font-size:0.78rem;color:#555;font-family:monospace">
            {filter.label}
          </label>
          {filter.type === "select"
            ? (
              <select
                value={filter.value}
                onChange={(e) =>
                  onChange(filter.key, (e.target as HTMLSelectElement).value)}
                style="padding:5px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.88rem;background:#fff"
              >
                <option value="">All</option>
                {(filter.options ?? []).map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            )
            : (
              <input
                type="text"
                value={filter.value}
                onInput={(e) =>
                  onChange(filter.key, (e.target as HTMLInputElement).value)}
                style="padding:5px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.88rem"
              />
            )}
        </div>
      ))}
    </div>
  );
}
