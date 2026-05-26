import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type GroupByControlOption = {
  key: string;
  label: string;
};

export type GroupByControlProps = ComponentIdentityProps & {
  field?: string;
  options?: GroupByControlOption[];
  onChange: (field: string) => void;
  label?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function GroupByControl({
  field = "",
  options = [],
  onChange,
  label = "Group by",
  className,
  design,
}: GroupByControlProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:4px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      <label style="font-size:0.78rem;color:#555;font-family:monospace">
        {label}
      </label>
      <select
        value={field}
        onChange={(e) => onChange((e.target as HTMLSelectElement).value)}
        style="padding:5px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.88rem;background:#fff"
      >
        <option value="">None</option>
        {options.map((opt) => (
          <option key={opt.key} value={opt.key}>
            {opt.label}
          </option>
        ))}
      </select>
    </div>
  );
}
