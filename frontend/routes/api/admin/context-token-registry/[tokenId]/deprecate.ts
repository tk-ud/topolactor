import { Handlers } from "$fresh/server.ts";

/**
 * POST /api/admin/context-token-registry/:tokenId/deprecate
 *
 * Marks a context_token_registry entry as deprecated (status → 'deprecated').
 * This endpoint is called by ContextTokenRegistryEditor.handleDeprecate.
 *
 * Current status: 501 — not yet bound to the backend registry DB.
 * Production wiring: forward to backend AdminEndpoint or connect via Deno postgres client.
 *
 * Response shape: { ok: boolean; message: string }
 */
export const handler: Handlers = {
  async POST(_req, ctx) {
    const { tokenId } = ctx.params;
    if (!tokenId) {
      return Response.json(
        { ok: false, message: "tokenId path parameter is required." },
        { status: 400 },
      );
    }
    // Validate UUID format before forwarding to backend to prevent injection.
    const uuidPattern =
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (!uuidPattern.test(tokenId)) {
      return Response.json(
        { ok: false, message: "tokenId must be a valid UUID." },
        { status: 400 },
      );
    }
    // TODO: bind to NpgsqlContextRouteRepository or backend admin endpoint.
    return Response.json(
      {
        ok: false,
        code: "TOKEN_REGISTRY_ENDPOINT_NOT_BOUND",
        message: "Deprecate endpoint is defined but not yet bound to the database.",
      },
      { status: 501 },
    );
  },
};
