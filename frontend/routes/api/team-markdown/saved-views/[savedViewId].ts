import { Handlers } from "$fresh/server.ts";
import { proxyToBackend } from "../../../../lib/backendProxy.ts";

/** GET /api/team-markdown/saved-views/:savedViewId — viewer detail read (any authenticated JWT). */
export const handler: Handlers = {
  GET(req, ctx) {
    return proxyToBackend(req, `/team-markdown/saved-views/${ctx.params.savedViewId}`);
  },
};
