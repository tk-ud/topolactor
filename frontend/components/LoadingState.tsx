import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type LoadingStateProps = ComponentIdentityProps & {
  message?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function LoadingState({
  message = "Loading...",
  className,
  design,
}: LoadingStateProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;align-items:center;gap:10px;padding:24px;font-family:monospace;color:#555",
    design,
  );
  return (
    <div
      aria-live="polite"
      aria-busy="true"
      className={mergedClassName}
      style={mergedStyle}
    >
      <span
        aria-hidden="true"
        style="display:inline-block;width:16px;height:16px;border:2px solid #ccc;border-top-color:#555;border-radius:50%;animation:spin 0.8s linear infinite"
      />
      <span>{message}</span>
    </div>
  );
}
