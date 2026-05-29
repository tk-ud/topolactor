import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ValidationErrorEntry = {
  message: string;
  field?: string;
  code?: string;
};

export type ValidationErrorPanelProps = ComponentIdentityProps & {
  errors?: ValidationErrorEntry[];
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function ValidationErrorPanel({
  errors = [],
  title,
  className,
  design,
}: ValidationErrorPanelProps): JSX.Element | null {
  if (errors.length === 0) return null;
  const mergedClassName = mergeDesignClassName(
    "alert-error font-mono",
    design,
  ) ?? "alert-error font-mono";
  const mergedStyle = mergeDesignStyle("", design);
  return (
    <div role="alert" className={mergedClassName} style={mergedStyle || undefined}>
      {title && (
        <div class="mb-1.5 text-sm font-semibold text-red-800">
          {title}
        </div>
      )}
      <ul class="m-0 list-inside list-disc pl-0">
        {errors.map((e, i) => (
          <li
            key={e.code ?? String(i)}
            class="mb-0.5 text-sm text-red-700"
          >
            {e.field && <span class="font-semibold">{e.field}: </span>}
            {e.message}
          </li>
        ))}
      </ul>
    </div>
  );
}
