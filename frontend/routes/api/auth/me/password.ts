import { Handlers } from "$fresh/server.ts";
import { proxyToBackend } from "../../../../lib/backendProxy.ts";

/** POST /api/auth/me/password — self password change (current + new password; no userId field). */
export const handler: Handlers = {
  POST(req) {
    return proxyToBackend(req, "/auth/me/password", { method: "POST" });
  },
};
