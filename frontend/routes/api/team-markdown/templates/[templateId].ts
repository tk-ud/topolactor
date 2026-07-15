import { Handlers } from "$fresh/server.ts";
import { proxyToBackend } from "../../../../lib/backendProxy.ts";

/** GET /api/team-markdown/templates/:templateId — viewer read (any authenticated JWT). */
export const handler: Handlers = {
  GET(req, ctx) {
    return proxyToBackend(req, `/team-markdown/templates/${ctx.params.templateId}`);
  },
};
