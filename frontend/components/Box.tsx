import { ComponentChildren, JSX } from "preact";

export type BoxProps = {
  children?: ComponentChildren;
  className?: string;
  style?: Record<string, string>;
  role?: JSX.HTMLAttributes<HTMLDivElement>["role"];
  "aria-label"?: string;
};

export default function Box({
  children,
  className,
  style,
  role,
  "aria-label": ariaLabel,
}: BoxProps): JSX.Element {
  return (
    <div
      class={className}
      style={style}
      role={role}
      aria-label={ariaLabel}
    >
      {children}
    </div>
  );
}
