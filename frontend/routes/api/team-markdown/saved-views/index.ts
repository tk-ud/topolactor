import { Handlers } from "$fresh/server.ts";
import { proxyToBackend } from "../../../../lib/backendProxy.ts";

/** GET /api/team-markdown/saved-views — viewer search/list (any authenticated JWT). */
export const handler: Handlers = {
  GET(req) {
    const url = new URL(req.url);
    const qs = url.search;
    return proxyToBackend(req, `/team-markdown/saved-views${qs}`);
  },
};
