import { Handlers } from "$fresh/server.ts";

/**
 * POST /api/dispatch
 *
 * Demo dispatch proxy. Forwards the request body and Authorization header to
 * DEMO_BACKEND_URL/dispatch.
 * Returns 501 (DISPATCH_BACKEND_NOT_CONFIGURED) when DEMO_BACKEND_URL is not set — explicit, not silent.
 *
 * Backend route is implemented: backend/Program.cs POST /dispatch → DispatchEndpoint.cs.
 * The backend always requires a valid JWT Bearer token (JwtGuard.Validate).
 *
 * Request body: EndpointRequestDto shape — operationType, target, layer, action, ...
 * Response:     { success: bool, emission?: {...}, errors?: [...] }
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
            code: "DISPATCH_BACKEND_NOT_CONFIGURED",
            message: "DEMO_BACKEND_URL is not set. Backend dispatch endpoint not wired.",
          }],
        },
        { status: 501 },
      );
    }

    try {
      const body = await req.text();
      const authHeader = req.headers.get("Authorization");
      const headers: Record<string, string> = { "Content-Type": "application/json" };
      if (authHeader) headers["Authorization"] = authHeader;

      const response = await fetch(`${backendUrl}/dispatch`, {
        method: "POST",
        headers,
        body,
      });
      const text = await response.text();
      let json: unknown;
      try {
        json = text ? JSON.parse(text) : {};
      } catch {
        return Response.json(
          {
            success: false,
            errors: [{
              code: "DISPATCH_BACKEND_BAD_RESPONSE",
              message: `Backend returned non-JSON (HTTP ${response.status}): ${text.slice(0, 300)}`,
            }],
          },
          { status: 502 },
        );
      }
      return Response.json(json, { status: response.status });
    } catch (err: unknown) {
      const detail = err instanceof Error ? err.message : String(err);
      const hint = backendUrl.includes("://backend:")
        ? " DEMO_BACKEND_URL が Docker 内部ホスト名のままです。ホストで deno task dev する場合は frontend/.env で http://localhost:5000 にしてください。"
        : " backend が起動しているか（docker compose / ポート 5000）と frontend/.env の DEMO_BACKEND_URL を確認してください。";
      return Response.json(
        {
          success: false,
          errors: [{
            code: "DISPATCH_BACKEND_UNREACHABLE",
            message: `${detail} (${backendUrl})${hint}`,
          }],
        },
        { status: 502 },
      );
    }
  },
};
