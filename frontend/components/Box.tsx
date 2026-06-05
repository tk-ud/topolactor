import { ComponentChildren, JSX } from "preact";

export type BoxProps = {
  children?: ComponentChildren;
  className?: string;
  style?: Record<string, string>;
  role?: JSX.HTMLAttributes<HTMLDivElement>["role"];
  "aria-label"?: string;
  "data-layout-preview"?: string;
};

export default function Box({
  children,
  className,
  style,
  role,
  "aria-label": ariaLabel,
  "data-layout-preview": dataLayoutPreview,
}: BoxProps): JSX.Element {
  return (
    <div
      class={className}
      style={style}
      role={role}
      aria-label={ariaLabel}
      data-layout-preview={dataLayoutPreview}
    >
      {children}
    </div>
  );
}
