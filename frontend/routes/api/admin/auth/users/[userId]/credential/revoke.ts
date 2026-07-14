import { Handlers } from "$fresh/server.ts";
import { proxyToBackend } from "../../../../../../../lib/backendProxy.ts";

/** POST /api/admin/auth/users/:userId/credential/revoke — admin kill-switch; never sets a new password. */
export const handler: Handlers = {
  POST(req, ctx) {
    return proxyToBackend(req, `/admin/auth/users/${ctx.params.userId}/credential/revoke`, { method: "POST" });
  },
};
