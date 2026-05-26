import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type DiffStrikeTextProps = ComponentIdentityProps & {
  before?: string;
  after?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function DiffStrikeText({
  before,
  after,
  className,
  design,
}: DiffStrikeTextProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:inline;font-family:monospace;font-size:0.9rem",
    design,
  );
  if (before === undefined && after === undefined) {
    return (
      <span className={mergedClassName} style={mergedStyle}>
        —
      </span>
    );
  }
  return (
    <span className={mergedClassName} style={mergedStyle}>
      {before !== undefined && (
        <span style="text-decoration:line-through;color:#c5221f">{before}</span>
      )}
      {before !== undefined && after !== undefined && (
        <span style="margin:0 4px;color:#999">→</span>
      )}
      {after !== undefined && (
        <span style="color:#188038">{after}</span>
      )}
    </span>
  );
}
