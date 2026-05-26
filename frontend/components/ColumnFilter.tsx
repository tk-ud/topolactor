import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ColumnFilterProps = ComponentIdentityProps & {
  column?: string;
  value?: string;
  placeholder?: string;
  onChange: (value: string) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function ColumnFilter({
  column,
  value = "",
  placeholder,
  onChange,
  className,
  design,
}: ColumnFilterProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:4px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {column && (
        <label style="font-size:0.78rem;color:#555;font-family:monospace">
          {column}
        </label>
      )}
      <input
        type="text"
        value={value}
        placeholder={placeholder}
        onInput={(e) => onChange((e.target as HTMLInputElement).value)}
        style="padding:5px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.88rem"
      />
    </div>
  );
}
