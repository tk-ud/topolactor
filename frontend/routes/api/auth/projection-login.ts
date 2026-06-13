import { Handlers } from "$fresh/server.ts";
import { proxyJson } from "../../../lib/authBackendProxy.ts";

/**
 * POST /api/auth/projection-login
 *
 * Projection surface login proxy. Backend issues admin JWT when the user has admin grants,
 * user JWT otherwise. Admin users entering via /auth retain admin capability.
 * Backend route: POST /auth/projection-login → AuthEndpoint.LoginProjectionAsync.
 */
export const handler: Handlers = {
  async POST(req) {
    const body = await req.text();
    return await proxyJson("/auth/projection-login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body,
    });
  },
};
