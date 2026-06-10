/**
 * Typed client for the /draft-preview/* API endpoints.
 * All endpoints require a valid JWT token — pass token from auth context.
 *
 * SSOT: content preview uses manifest screen_data_shape.initialDataRows (Contents Step 3),
 * not content_entity_drafts (content_bundle promote path only).
 */

export type DraftPreviewLayout = {
  layoutId: string;
  layoutKey: string;
  routeKey: string;
  layoutKind: string;
  slotKeys: string[];
};

export type DraftPreviewNodeDesign = {
  designId?: string;
  inlineText?: string;
  linkHref?: string;
  linkTarget?: string;
  cssTokenRefs?: string[];
  responsiveTokenRefs?: Record<string, string[]>;
  classname?: string;
  tailwind?: string;
  hasDesignTmpDraft?: boolean;
};

export type DraftPreviewLayoutNode = {
  nodeId?: string;
  nodeKind?: string;
  htmlTag?: string;
  componentKey?: string;
  componentId?: string;
  componentKind?: string;
  parentNodeId?: string | null;
  slotKey?: string;
  orderIndex: number;
  x?: number;
  y?: number;
  width?: number | string;
  height?: number | string;
  widthMode?: "auto" | "preset" | "custom";
  heightMode?: "auto" | "preset" | "custom";
  layoutClassRefs?: string[];
  design?: DraftPreviewNodeDesign | null;
  /** @deprecated legacy field — prefer nodeId + layoutClassRefs */
  layoutPatchJson?: string;
};

export type DraftPreviewInitialDataRow = Record<string, string>;

export type DraftPreviewResult = {
  success: boolean;
  layoutId?: string;
  previewMode?: "layout_only" | "layout_and_topology_intent";
  routeKey?: string;
  packageId?: string;
  layoutNodes?: DraftPreviewLayoutNode[];
  rootLayoutClassRefs?: string[];
  designByNodeId?: Record<string, DraftPreviewNodeDesign>;
  manifestId?: string;
  manifestStatus?: string;
  topologySystemName?: string;
  initialDataRows?: DraftPreviewInitialDataRow[];
  errors?: Array<{ code?: string; message?: string }>;
};

type ListLayoutsResponse = {
  success: boolean;
  layouts?: DraftPreviewLayout[];
  errors?: Array<{ code?: string; message?: string }>;
};

export async function fetchDraftPreviewLayouts(
  token?: string,
): Promise<ListLayoutsResponse> {
  try {
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const response = await fetch("/api/draft-preview/layouts", { headers });
    const json = await response.json() as ListLayoutsResponse;
    return json;
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}

export async function fetchDraftPreview(
  layoutId: string,
  token?: string,
): Promise<DraftPreviewResult> {
  try {
    const headers: Record<string, string> = { "Content-Type": "application/json" };
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const response = await fetch("/api/draft-preview/preview", {
      method: "POST",
      headers,
      body: JSON.stringify({ layoutId }),
    });
    const json = await response.json() as DraftPreviewResult;
    return json;
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}
