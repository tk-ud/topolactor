import { Handlers } from "$fresh/server.ts";

/**
 * GET /api/sse
 *
 * SSE projection lane proxy. Forwards the EventSource connection to the backend
 * SSE endpoint (DEMO_BACKEND_URL/sse) and streams the response.
 * Returns 501 (SSE_BACKEND_NOT_CONFIGURED) when DEMO_BACKEND_URL is not set.
 *
 * Backend route: backend/Program.cs GET /sse → SseEndpoint.cs.
 * Lane: sse_projection_lane in pipeline-continuity-ssot.yaml.
 */
export const handler: Handlers = {
  async GET(req) {
    const backendUrl = Deno.env.get("DEMO_BACKEND_URL");

    if (!backendUrl) {
      return Response.json(
        {
          success: false,
          errors: [{
            code: "SSE_BACKEND_NOT_CONFIGURED",
            message: "DEMO_BACKEND_URL is not set. Backend SSE endpoint not wired.",
          }],
        },
        { status: 501 },
      );
    }

    try {
      const authHeader = req.headers.get("Authorization");
      const headers: Record<string, string> = { Accept: "text/event-stream" };
      if (authHeader) headers["Authorization"] = authHeader;

      const upstream = await fetch(`${backendUrl}/sse`, {
        method: "GET",
        headers,
      });

      return new Response(upstream.body, {
        status: upstream.status,
        headers: {
          "Content-Type": "text/event-stream",
          "Cache-Control": "no-cache",
          "X-Accel-Buffering": "no",
        },
      });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return Response.json(
        {
          success: false,
          errors: [{ code: "SSE_BACKEND_UNREACHABLE", message }],
        },
        { status: 502 },
      );
    }
  },
};
