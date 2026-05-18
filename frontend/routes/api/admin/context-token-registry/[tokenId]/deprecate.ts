import { Handlers } from "$fresh/server.ts";

/**
 * POST /api/admin/context-token-registry/:tokenId/deprecate
 *
 * Proxies to backend POST /admin/context-token-registry/{tokenId}/deprecate.
 * Returns 501 when DEMO_BACKEND_URL is not set.
 * Returns 502 when the backend is unreachable.
 * Returns 400 when tokenId is not a valid UUID.
 * Returns 401 when no valid JWT is provided.
 *
 * Backend route: backend/endpoint/AdminEndpoint.cs
 */
export const handler: Handlers = {
  async POST(req, ctx) {
    const { tokenId } = ctx.params;
    if (!tokenId) {
      return Response.json(
        { ok: false, message: "tokenId path parameter is required." },
        { status: 400 },
      );
    }

    const uuidPattern =
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (!uuidPattern.test(tokenId)) {
      return Response.json(
        { ok: false, message: "tokenId must be a valid UUID." },
        { status: 400 },
      );
    }

    const backendUrl = Deno.env.get("DEMO_BACKEND_URL");
    if (!backendUrl) {
      return Response.json(
        {
          ok: false,
          code: "ADMIN_BACKEND_NOT_CONFIGURED",
          message: "DEMO_BACKEND_URL is not set. Admin registry endpoint not wired.",
        },
        { status: 501 },
      );
    }

    try {
      const authHeader = req.headers.get("Authorization");
      const headers: Record<string, string> = { "Content-Type": "application/json" };
      if (authHeader) headers["Authorization"] = authHeader;

      const response = await fetch(
        `${backendUrl}/admin/context-token-registry/${tokenId}/deprecate`,
        { method: "POST", headers },
      );
      const json: unknown = await response.json();
      return Response.json(json, { status: response.status });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return Response.json(
        { ok: false, code: "ADMIN_BACKEND_UNREACHABLE", message },
        { status: 502 },
      );
    }
  },
};
