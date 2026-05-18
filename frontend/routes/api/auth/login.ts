import { Handlers } from "$fresh/server.ts";

/**
 * POST /api/auth/login
 *
 * Demo auth proxy. Forwards the request body to DEMO_BACKEND_URL/auth/login.
 * Returns 501 (AUTH_BACKEND_NOT_CONFIGURED) when DEMO_BACKEND_URL is not set — explicit, not silent.
 *
 * Backend route is implemented: backend/Program.cs POST /auth/login → AuthEndpoint.cs.
 * Demo credentials are stored as bcrypt hashes in function_parameters (demo_auth/demo_users).
 *
 * Request body: { "username": "...", "password": "..." }
 * Response:     { "success": bool, "token"?: "...", "errors"?: [...] }
 *
 * In Docker Compose: DEMO_BACKEND_URL is set to http://backend:5000 by default.
 * For local dev without Docker: set DEMO_BACKEND_URL=http://localhost:5000.
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
