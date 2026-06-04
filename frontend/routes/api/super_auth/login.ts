import { Handlers } from "$fresh/server.ts";
import { proxyJson } from "../../../lib/authBackendProxy.ts";

/** POST /api/super_auth/login — admin realm */
export const handler: Handlers = {
  async POST(req) {
    const body = await req.text();
    return await proxyJson("/super_auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body,
    });
  },
};
