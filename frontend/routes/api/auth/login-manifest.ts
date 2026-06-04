import { Handlers } from "$fresh/server.ts";
import { proxyJson } from "../../../lib/authBackendProxy.ts";

/** GET /api/auth/login-manifest — user login UI seed manifest */
export const handler: Handlers = {
  async GET() {
    return await proxyJson("/auth/login-manifest", { method: "GET" });
  },
};
