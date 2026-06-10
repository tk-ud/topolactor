import { buildLayoutPreviewPlaceholderProps } from "./layoutComponentPreview.ts";

export type ComponentDesignSnapshot = {
  inlineText?: string;
  linkHref?: string;
  linkTarget?: string;
  cssTokenRefs?: string[];
  classname?: string;
  tailwind?: string;
};

/** Merge persisted component_style_design snapshot into catalog component props for runtime render. */
export function mergeCatalogPropsWithComponentDesign(
  componentKind: string,
  componentKey: string,
  baseProps: Record<string, unknown>,
  design?: ComponentDesignSnapshot,
): Record<string, unknown> {
  if (!design) return baseProps;
  const designed = buildLayoutPreviewPlaceholderProps(componentKind, componentKey, {
    inlineText: design.inlineText,
    linkHref: design.linkHref,
    linkTarget: design.linkTarget,
  });
  const baseData = baseProps.data;
  const designedData = designed.data;
  if (
    typeof baseData === "object" && baseData !== null && !Array.isArray(baseData) &&
    typeof designedData === "object" && designedData !== null && !Array.isArray(designedData)
  ) {
    return {
      ...baseProps,
      ...designed,
      data: { ...baseData as Record<string, unknown>, ...designedData as Record<string, unknown> },
    };
  }
  return { ...baseProps, ...designed };
}
