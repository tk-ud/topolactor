import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type PaginationControlProps = ComponentIdentityProps & {
  page?: number;
  pageSize?: number;
  total?: number;
  onChange: (page: number) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function PaginationControl({
  page = 1,
  pageSize = 10,
  total = 0,
  onChange,
  className,
  design,
}: PaginationControlProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:row;align-items:center;gap:12px;font-family:monospace",
    design,
  );
  const totalPages = pageSize > 0 ? Math.max(1, Math.ceil(total / pageSize)) : 1;
  const atFirst = page <= 1;
  const atLast = page >= totalPages;

  return (
    <div className={mergedClassName} style={mergedStyle}>
      <button
        type="button"
        disabled={atFirst}
        onClick={() => onChange(page - 1)}
        style={`padding:5px 12px;border:1px solid #ccc;border-radius:4px;background:#fff;font-family:monospace;font-size:0.88rem;cursor:${
          atFirst ? "not-allowed" : "pointer"
        };opacity:${atFirst ? 0.5 : 1};color:#333`}
      >
        Prev
      </button>
      <span style="font-size:0.9rem;color:#444;font-family:monospace">
        Page {page} of {totalPages}
      </span>
      <button
        type="button"
        disabled={atLast}
        onClick={() => onChange(page + 1)}
        style={`padding:5px 12px;border:1px solid #ccc;border-radius:4px;background:#fff;font-family:monospace;font-size:0.88rem;cursor:${
          atLast ? "not-allowed" : "pointer"
        };opacity:${atLast ? 0.5 : 1};color:#333`}
      >
        Next
      </button>
    </div>
  );
}
