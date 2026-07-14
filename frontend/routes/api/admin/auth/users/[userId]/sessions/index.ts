import { Handlers } from "$fresh/server.ts";
import { proxyToBackend } from "../../../../../../../lib/backendProxy.ts";

/** GET /api/admin/auth/users/:userId/sessions — admin-only session list for a target account. */
export const handler: Handlers = {
  GET(req, ctx) {
    return proxyToBackend(req, `/admin/auth/users/${ctx.params.userId}/sessions`);
  },
};
