import { Handlers } from "$fresh/server.ts";
import { proxyJson } from "../../../lib/authBackendProxy.ts";

/** POST /api/auth/register — normal user registration, pending approval */
export const handler: Handlers = {
  async POST(req) {
    const body = await req.text();
    return await proxyJson("/auth/register", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body,
    });
  },
};
