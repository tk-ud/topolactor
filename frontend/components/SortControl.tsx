import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type SortControlField = {
  key: string;
  label: string;
};

export type SortControlProps = ComponentIdentityProps & {
  field?: string;
  direction?: "asc" | "desc" | null;
  fields?: SortControlField[];
  onChange: (field: string, direction: "asc" | "desc") => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function SortControl({
  field = "",
  direction = null,
  fields = [],
  onChange,
  className,
  design,
}: SortControlProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:row;align-items:center;gap:8px;font-family:monospace",
    design,
  );

  function handleFieldChange(newField: string) {
    onChange(newField, direction === "desc" ? "desc" : "asc");
  }

  function handleToggleDirection() {
    if (!field) return;
    const nextDirection: "asc" | "desc" = direction === "asc" ? "desc" : "asc";
    onChange(field, nextDirection);
  }

  return (
    <div className={mergedClassName} style={mergedStyle}>
      <label style="font-size:0.78rem;color:#555;font-family:monospace">
        Sort by
      </label>
      <select
        value={field}
        onChange={(e) =>
          handleFieldChange((e.target as HTMLSelectElement).value)}
        style="padding:5px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.88rem;background:#fff"
      >
        <option value="">None</option>
        {fields.map((f) => (
          <option key={f.key} value={f.key}>
            {f.label}
          </option>
        ))}
      </select>
      {field && (
        <button
          type="button"
          onClick={handleToggleDirection}
          style="padding:5px 10px;border:1px solid #ccc;border-radius:4px;background:#f5f5f5;font-family:monospace;font-size:0.88rem;cursor:pointer"
        >
          {direction === "desc" ? "desc ↓" : "asc ↑"}
        </button>
      )}
    </div>
  );
}
