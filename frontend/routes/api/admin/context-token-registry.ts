import { Handlers } from "$fresh/server.ts";

/**
 * GET  /api/admin/context-token-registry  — not yet bound to backend registry
 * POST /api/admin/context-token-registry  — not yet bound to backend registry
 *
 * Returns 501 until this route is wired to context_token_registry on the backend.
 * Frontend must handle 501 explicitly — no seed token list stored here.
 */
export const handler: Handlers = {
  GET(_req) {
    return Response.json(
      { ok: false, code: "TOKEN_REGISTRY_ENDPOINT_NOT_BOUND", message: "Not implemented." },
      { status: 501 },
    );
  },

  POST(_req) {
    return Response.json(
      { ok: false, code: "TOKEN_REGISTRY_ENDPOINT_NOT_BOUND", message: "Not implemented." },
      { status: 501 },
    );
  },
};
