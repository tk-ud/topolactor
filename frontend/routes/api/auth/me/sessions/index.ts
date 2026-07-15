import { Handlers } from "$fresh/server.ts";
import { proxyToBackend } from "../../../../../lib/backendProxy.ts";

/** GET /api/auth/me/sessions — self session list (own account only). */
export const handler: Handlers = {
  GET(req) {
    return proxyToBackend(req, "/auth/me/sessions");
  },
};
