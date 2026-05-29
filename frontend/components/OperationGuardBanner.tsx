import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type OperationGuardLevel = "warning" | "error" | "info";

export type OperationGuardBannerProps = ComponentIdentityProps & {
  message?: string;
  level?: OperationGuardLevel;
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

const levelClasses: Record<OperationGuardLevel, string> = {
  info: "alert-info",
  warning: "alert-warn",
  error: "alert-error",
};

const levelIcons: Record<OperationGuardLevel, string> = {
  info: "ℹ",
  warning: "⚠",
  error: "✖",
};

export function OperationGuardBanner({
  message,
  level = "info",
  title,
  className,
  design,
}: OperationGuardBannerProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(
    `${levelClasses[level]} font-mono`,
    design,
  ) ?? levelClasses[level];
  const mergedStyle = mergeDesignStyle("", design);
  return (
    <div role="alert" aria-live="polite" className={mergedClassName} style={mergedStyle || undefined}>
      <div class="flex items-start gap-2.5">
        <span class="shrink-0 text-base" aria-hidden="true">
          {levelIcons[level]}
        </span>
        <div class="min-w-0 flex-1">
          {title && (
            <div class="mb-0.5 text-sm font-semibold">
              {title}
            </div>
          )}
          {message && (
            <div class="text-sm opacity-90">
              {message}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
