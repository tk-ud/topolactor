import { Handlers } from "$fresh/server.ts";

/**
 * GET /api/auth/session
 *
 * Proxies Authorization Bearer to DEMO_BACKEND_URL/auth/session.
 * 200 only when backend JwtGuard accepts the token (signature, exp, sub, role).
 */
export const handler: Handlers = {
  async GET(req) {
    const backendUrl = Deno.env.get("DEMO_BACKEND_URL");

    if (!backendUrl) {
      return Response.json(
        {
          success: false,
          errors: [{
            code: "AUTH_BACKEND_NOT_CONFIGURED",
            message: "DEMO_BACKEND_URL is not set. Backend auth session probe not wired.",
          }],
        },
        { status: 501 },
      );
    }

    const authHeader = req.headers.get("Authorization");
    if (!authHeader) {
      return Response.json(
        {
          success: false,
          errors: [{ code: "AUTH_TOKEN_MISSING", message: "Authorization token is required." }],
        },
        { status: 401 },
      );
    }

    const url = new URL(req.url);
    const expected = url.searchParams.get("expected");
    const qs = expected ? `?expected=${encodeURIComponent(expected)}` : "";

    try {
      const response = await fetch(`${backendUrl}/auth/session${qs}`, {
        headers: { Authorization: authHeader },
      });
      const json: unknown = await response.json();
      return Response.json(json, { status: response.status });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return Response.json(
        {
          success: false,
          errors: [{ code: "AUTH_BACKEND_UNREACHABLE", message }],
        },
        { status: 502 },
      );
    }
  },
};
