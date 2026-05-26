import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ColumnVisibilityEditorColumn = {
  key: string;
  label: string;
  visible: boolean;
};

export type ColumnVisibilityEditorProps = ComponentIdentityProps & {
  columns?: ColumnVisibilityEditorColumn[];
  onChange: (key: string, visible: boolean) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function ColumnVisibilityEditor({
  columns = [],
  onChange,
  className,
  design,
}: ColumnVisibilityEditorProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:8px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {columns.map((col) => (
        <label
          key={col.key}
          style="display:flex;align-items:center;gap:8px;cursor:pointer;font-size:0.9rem;color:#333;font-family:monospace"
        >
          <input
            type="checkbox"
            checked={col.visible}
            onChange={(e) =>
              onChange(col.key, (e.target as HTMLInputElement).checked)}
            style="cursor:pointer"
          />
          <span>{col.label}</span>
        </label>
      ))}
    </div>
  );
}
