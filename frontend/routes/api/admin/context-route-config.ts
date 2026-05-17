import { Handlers } from "$fresh/server.ts";

/**
 * GET  /api/admin/context-route-config  — not yet bound to backend registry
 * PUT  /api/admin/context-route-config  — not yet bound to backend registry
 *
 * Returns 501 until this route is wired to ContextRouteConfigRepository.
 * Frontend must handle 501 explicitly — no hardcoded fallback config here.
 */
export const handler: Handlers = {
  GET(_req) {
    return Response.json(
      { ok: false, code: "CONFIG_REGISTRY_ENDPOINT_NOT_BOUND", message: "Not implemented." },
      { status: 501 },
    );
  },

  PUT(_req) {
    return Response.json(
      { ok: false, code: "CONFIG_REGISTRY_ENDPOINT_NOT_BOUND", message: "Not implemented." },
      { status: 501 },
    );
  },
};
