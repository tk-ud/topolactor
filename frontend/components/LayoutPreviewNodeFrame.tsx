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
  inlineText?: string;
  linkHref?: string;
  linkTarget?: string;
  /** frontend_local_derived_calculation_binding: fires when this node's onChange fires. */
  calcTriggerCallback?: (value: unknown) => void;
  /** frontend_local_derived_calculation_binding: prop overrides to inject into this node's display. */
  calcValueOverrides?: Record<string, unknown>;
  /** search_suggest candidate boundary: wires onSearch → debounce → provider loop. */
  searchCallback?: (componentId: string, query: string) => void;
  /** Live suggestions for autocomplete_input / suggest_input preview. */
  searchSuggestions?: string[];
  /** Combobox preview options from uiBuilderAutocompleteCandidates local derivation. */
  comboboxOptions?: { label: string; value: string }[];
};

/** Shared read-only runtime primitive preview frame for canvas and visual audit modal. */
export function LayoutPreviewNodeFrame({
  componentKey,
  componentKind,
  componentId,
  isDraftOnly = false,
  className = "",
  inlineText,
  linkHref,
  linkTarget,
  calcTriggerCallback,
  calcValueOverrides,
  searchCallback,
  searchSuggestions,
  comboboxOptions,
}: LayoutPreviewNodeFrameProps): JSX.Element {
  const resolvedKind = componentKind ??
    resolveComponentKindForLayoutPreview(componentKey) ??
    "—";
  const result = renderLayoutComponentPreview({
    componentKey,
    componentKind,
    componentId,
    isDraftOnly,
    inlineText,
    linkHref,
    linkTarget,
    calcTriggerCallback,
    calcValueOverrides,
    searchCallback,
    searchSuggestions,
    comboboxOptions,
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
