import { Handlers } from "$fresh/server.ts";

/**
 * GET /api/draft-preview/layouts
 *
 * Proxies to DEMO_BACKEND_URL/draft-preview/layouts.
 * Returns { success, layouts } — list of admin-authored layout candidates.
 * Returns 501 when DEMO_BACKEND_URL is not set.
 */
export const handler: Handlers = {
  async GET(req) {
    const backendUrl = Deno.env.get("DEMO_BACKEND_URL");

    if (!backendUrl) {
      return Response.json(
        {
          success: false,
          errors: [{
            code: "DRAFT_PREVIEW_BACKEND_NOT_CONFIGURED",
            message: "DEMO_BACKEND_URL is not set. Draft preview backend not wired.",
          }],
        },
        { status: 501 },
      );
    }

    try {
      const authHeader = req.headers.get("Authorization");
      const headers: Record<string, string> = {};
      if (authHeader) headers["Authorization"] = authHeader;

      const response = await fetch(`${backendUrl}/draft-preview/layouts`, { headers });
      const text = await response.text();
      let json: unknown;
      try {
        json = text ? JSON.parse(text) : {};
      } catch {
        return Response.json(
          {
            success: false,
            errors: [{
              code: "DRAFT_PREVIEW_BACKEND_BAD_RESPONSE",
              message: `Backend returned non-JSON (HTTP ${response.status}): ${text.slice(0, 300)}`,
            }],
          },
          { status: 502 },
        );
      }
      return Response.json(json, { status: response.status });
    } catch (err: unknown) {
      const detail = err instanceof Error ? err.message : String(err);
      return Response.json(
        {
          success: false,
          errors: [{
            code: "DRAFT_PREVIEW_BACKEND_UNREACHABLE",
            message: `${detail} (${backendUrl})`,
          }],
        },
        { status: 502 },
      );
    }
  },
};
