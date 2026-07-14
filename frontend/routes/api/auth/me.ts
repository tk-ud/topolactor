import { Handlers } from "$fresh/server.ts";
import { proxyToBackend } from "../../../lib/backendProxy.ts";

/** GET /api/auth/me — current authenticated account (self only; target resolved from JWT server-side). */
export const handler: Handlers = {
  GET(req) {
    return proxyToBackend(req, "/auth/me");
  },
};
