import { Handlers } from "$fresh/server.ts";
import { proxyToBackend } from "../../../../../lib/backendProxy.ts";

/** POST /api/auth/me/sessions/revoke-others — revoke every session except the caller's current one. */
export const handler: Handlers = {
  POST(req) {
    return proxyToBackend(req, "/auth/me/sessions/revoke-others", { method: "POST" });
  },
};
