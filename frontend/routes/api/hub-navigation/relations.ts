import { Handlers } from "$fresh/server.ts";
import { proxyToBackend } from "../../../lib/backendProxy.ts";

/** GET /api/hub-navigation/relations — read-only navigation link-list fallback (any authenticated JWT). */
export const handler: Handlers = {
  GET(req) {
    return proxyToBackend(req, "/hub-navigation/relations");
  },
};
