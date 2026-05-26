import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ConfirmedUpdateButtonProps = ComponentIdentityProps & {
  label?: string;
  confirmLabel?: string;
  description?: string;
  disabled?: boolean;
  onConfirm: () => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function ConfirmedUpdateButton({
  label,
  confirmLabel,
  description,
  disabled,
  onConfirm,
  className,
  design,
}: ConfirmedUpdateButtonProps): JSX.Element {
  const isDisabled = disabled || design?.state === "loading";
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:inline-flex;flex-direction:column;gap:4px;font-family:monospace",
    design,
  );
  const buttonLabel = confirmLabel ?? label ?? "Confirm";
  return (
    <div className={mergedClassName} style={mergedStyle}>
      <button
        type="button"
        disabled={isDisabled}
        onClick={onConfirm}
        style={`background:#0070f3;color:#fff;border:none;padding:7px 16px;border-radius:4px;font-family:monospace;font-size:0.9rem;cursor:${isDisabled ? "not-allowed" : "pointer"};opacity:${isDisabled ? 0.6 : 1};font-weight:500`}
      >
        {buttonLabel}
      </button>
      {description && (
        <span style="font-size:0.78rem;color:#888;max-width:320px">{description}</span>
      )}
    </div>
  );
}
