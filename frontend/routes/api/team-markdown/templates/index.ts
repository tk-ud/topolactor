import { Handlers } from "$fresh/server.ts";
import { proxyToBackend } from "../../../../lib/backendProxy.ts";

/** GET /api/team-markdown/templates — viewer read (any authenticated JWT), status=&query= via passthrough. */
export const handler: Handlers = {
  GET(req) {
    const url = new URL(req.url);
    const qs = url.search;
    return proxyToBackend(req, `/team-markdown/templates${qs}`);
  },
};
