import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type JsonViewerProps = ComponentIdentityProps & {
  value: unknown;
  collapsed?: boolean;
  className?: string;
  design?: ComponentDesignParams;
};

function renderValue(value: unknown, depth: number): JSX.Element {
  if (value === null) {
    return <span style="color:#888">null</span>;
  }
  if (value === undefined) {
    return <span style="color:#888">undefined</span>;
  }
  if (typeof value === "boolean") {
    return <span style="color:#0070f3">{String(value)}</span>;
  }
  if (typeof value === "number") {
    return <span style="color:#188038">{String(value)}</span>;
  }
  if (typeof value === "string") {
    return <span style="color:#b06000">&quot;{value}&quot;</span>;
  }
  if (Array.isArray(value)) {
    if (value.length === 0) return <span style="color:#555">[]</span>;
    return (
      <span>
        {"["}
        <div style={`margin-left:${(depth + 1) * 16}px`}>
          {value.map((item, i) => (
            <div key={i}>
              {renderValue(item, depth + 1)}
              {i < value.length - 1 ? "," : ""}
            </div>
          ))}
        </div>
        {"]"}
      </span>
    );
  }
  if (typeof value === "object") {
    const entries = Object.entries(value as Record<string, unknown>);
    if (entries.length === 0) return <span style="color:#555">{"{}"}</span>;
    return (
      <span>
        {"{"}
        <div style={`margin-left:${(depth + 1) * 16}px`}>
          {entries.map(([k, v], i) => (
            <div key={k}>
              <span style="color:#555">&quot;{k}&quot;</span>
              {": "}
              {renderValue(v, depth + 1)}
              {i < entries.length - 1 ? "," : ""}
            </div>
          ))}
        </div>
        {"}"}
      </span>
    );
  }
  return <span style="color:#555">{String(value)}</span>;
}

export function JsonViewer({
  value,
  className,
  design,
}: JsonViewerProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "background:#f8f8f8;border:1px solid #e0e0e0;border-radius:4px;padding:12px;font-family:monospace;font-size:0.85rem;overflow:auto",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {renderValue(value, 0)}
    </div>
  );
}
