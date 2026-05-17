import { Handlers } from "$fresh/server.ts";

/**
 * GET  /api/admin/context-route-config  — load current config (skeleton: returns Default)
 * PUT  /api/admin/context-route-config  — save config       (skeleton: echoes back with ok)
 *
 * In-memory skeleton.  Production implementation calls
 * ContextRouteConfigRepository on the C# backend.
 */

const DEFAULT_CONFIG = {
  minSimilarity: 0.05,
  topK: 50,
  minNeighbors: 10,
  recentDays: 90,
  maxCandidatesShown: 5,
  baselineWeight: 0.5,
  neighborWeight: 0.5,
};

export const handler: Handlers = {
  GET(_req) {
    return Response.json(DEFAULT_CONFIG);
  },

  async PUT(req) {
    let body: unknown;
    try {
      body = await req.json();
    } catch {
      return Response.json({ ok: false, message: "Invalid JSON body." }, { status: 400 });
    }

    if (typeof body !== "object" || body === null) {
      return Response.json({ ok: false, message: "Body must be an object." }, { status: 400 });
    }

    // In-memory skeleton: no-op save.
    // Production: call ContextRouteConfigRepository.SaveConfigAsync.
    return Response.json({
      ok: true,
      message: "Config accepted (skeleton — not persisted to DB).",
      config: body,
    });
  },
};
