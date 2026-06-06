/**
 * Typed client for the /draft-preview/* API endpoints.
 * All 3 endpoints require a valid JWT token — pass token from auth context.
 */

export type DraftPreviewLayout = {
  layoutId: string;
  layoutKey: string;
  routeKey: string;
  layoutKind: string;
  slotKeys: string[];
};

export type DraftPreviewDraft = {
  draftId: string;
  label: string;
  hubId: string;
  status: string;
  createdAt: string;
};

export type DraftPreviewLayoutNode = {
  slotKey?: string;
  orderIndex: number;
  layoutPatchJson?: string;
};

export type DraftPreviewResult = {
  success: boolean;
  layoutId?: string;
  draftId?: string;
  layoutNodes?: DraftPreviewLayoutNode[];
  draftEntityJson?: Record<string, unknown>;
  draftStatus?: string;
  errors?: Array<{ code?: string; message?: string }>;
};

type ListLayoutsResponse = {
  success: boolean;
  layouts?: DraftPreviewLayout[];
  errors?: Array<{ code?: string; message?: string }>;
};

type ListDraftsResponse = {
  success: boolean;
  drafts?: DraftPreviewDraft[];
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

export async function fetchDraftPreviewDrafts(
  token?: string,
): Promise<ListDraftsResponse> {
  try {
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const response = await fetch("/api/draft-preview/drafts", { headers });
    const json = await response.json() as ListDraftsResponse;
    return json;
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}

export async function fetchDraftPreview(
  layoutId: string,
  draftId: string,
  token?: string,
): Promise<DraftPreviewResult> {
  try {
    const headers: Record<string, string> = { "Content-Type": "application/json" };
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const response = await fetch("/api/draft-preview/preview", {
      method: "POST",
      headers,
      body: JSON.stringify({ layoutId, draftId }),
    });
    const json = await response.json() as DraftPreviewResult;
    return json;
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}
