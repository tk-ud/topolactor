import { JSX } from "preact";
import { LayoutComponentPreviewFallback } from "./LayoutComponentPreviewFallback.tsx";
import {
  renderLayoutComponentPreview,
  resolveComponentKindForLayoutPreview,
} from "../runtime/layoutComponentPreview.ts";

export type LayoutPreviewNodeFrameProps = {
  componentKey: string;
  componentKind?: string;
  componentId?: string;
  isDraftOnly?: boolean;
  className?: string;
};

/** Shared read-only runtime primitive preview frame for canvas and visual audit modal. */
export function LayoutPreviewNodeFrame({
  componentKey,
  componentKind,
  componentId,
  isDraftOnly = false,
  className = "",
}: LayoutPreviewNodeFrameProps): JSX.Element {
  const resolvedKind = componentKind ??
    resolveComponentKindForLayoutPreview(componentKey) ??
    "—";
  const result = renderLayoutComponentPreview({
    componentKey,
    componentKind,
    componentId,
    isDraftOnly,
  });
  if (!result.ok) {
    return (
      <LayoutComponentPreviewFallback
        componentKey={componentKey}
        componentKind={resolvedKind}
        code={result.code}
        reason={result.reason}
      />
    );
  }
  return (
    <div class={`h-full w-full overflow-auto bg-white ${className}`}>
      {result.node as JSX.Element}
    </div>
  );
}
