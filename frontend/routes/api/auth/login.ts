import { Handlers } from "$fresh/server.ts";
import { proxyJson } from "../../../lib/authBackendProxy.ts";

/** POST /api/auth/login — user realm → backend POST /auth/login */
export const handler: Handlers = {
  async POST(req) {
    const body = await req.text();
    return await proxyJson("/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body,
    });
  },
};
