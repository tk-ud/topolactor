import { Handlers } from "$fresh/server.ts";
import { proxyToBackend } from "../../../../../lib/backendProxy.ts";

/** POST /api/auth/me/sessions/revoke — revoke one of the caller's own sessions by id. */
export const handler: Handlers = {
  POST(req) {
    return proxyToBackend(req, "/auth/me/sessions/revoke", { method: "POST" });
  },
};
