import { Handlers } from "$fresh/server.ts";

/**
 * POST /api/auth/login
 *
 * Demo auth scaffold proxy. Forwards to DEMO_BACKEND_URL/auth/login when set.
 * Returns 501 (AUTH_BACKEND_NOT_CONFIGURED) when DEMO_BACKEND_URL is not set — explicit, not silent.
 *
 * NOTE: The backend /auth/login HTTP route is not yet implemented.
 * AuthEndpoint.cs (backend/endpoint/AuthEndpoint.cs) contains the auth logic class,
 * but the backend does not have an HTTP host or route-binding layer yet.
 * This proxy is scaffolded for when the backend HTTP server is wired.
 * See .agent/tasks/todo.md — "backend HTTP host and /auth/login route".
 *
 * Request body: { "username": "...", "password": "..." }
 * Response:     { "success": bool, "token"?: "...", "errors"?: [...] }
 *
 * To enable: set DEMO_BACKEND_URL to the running backend base URL, e.g.
 *   DEMO_BACKEND_URL=http://localhost:5000
 */
export const handler: Handlers = {
  async POST(req) {
    const backendUrl = Deno.env.get("DEMO_BACKEND_URL");

    if (!backendUrl) {
      return Response.json(
        {
          success: false,
          errors: [{
            code: "AUTH_BACKEND_NOT_CONFIGURED",
            message: "DEMO_BACKEND_URL is not set. Backend auth endpoint not wired.",
          }],
        },
        { status: 501 },
      );
    }

    try {
      const body = await req.text();
      const response = await fetch(`${backendUrl}/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body,
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
