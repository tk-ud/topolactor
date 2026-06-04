import { Handlers } from "$fresh/server.ts";
import { proxyJson } from "../../../lib/authBackendProxy.ts";

/** POST /api/auth/logout */
export const handler: Handlers = {
  async POST(req) {
    const cookie = req.headers.get("cookie") ?? "";
    const body = await req.text();
    return await proxyJson("/auth/logout", {
      method: "POST",
      headers: { "Content-Type": "application/json", Cookie: cookie },
      body: body || "{}",
    });
  },
};
