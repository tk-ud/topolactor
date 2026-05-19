import { Handlers } from "$fresh/server.ts";

/**
 * POST /api/admin/registry-vector-validate
 *
 * Proxies to the backend POST /admin/registry-vector-validate endpoint.
 * Returns 501 (ADMIN_BACKEND_NOT_CONFIGURED) when DEMO_BACKEND_URL is not set.
 * Returns 502 when the backend is unreachable.
 * Returns 401 when no valid JWT is provided.
 *
 * Backend route: backend/endpoint/AdminEndpoint.cs → HandleValidateRegistryVectorAsync
 */

function getBackendUrl(): string | null {
  return Deno.env.get("DEMO_BACKEND_URL") ?? null;
}

function notConfigured(): Response {
  return Response.json(
    {
      ok: false,
      code: "ADMIN_BACKEND_NOT_CONFIGURED",
      message: "DEMO_BACKEND_URL is not set. Registry vector validation endpoint not wired.",
    },
    { status: 501 },
  );
}

export const handler: Handlers = {
  async POST(req) {
    const backendUrl = getBackendUrl();
    if (!backendUrl) return notConfigured();

    try {
      const authHeader = req.headers.get("Authorization");
      const headers: Record<string, string> = { "Content-Type": "application/json" };
      if (authHeader) headers["Authorization"] = authHeader;

      const body = await req.text();
      const response = await fetch(`${backendUrl}/admin/registry-vector-validate`, {
        method: "POST",
        headers,
        body,
      });
      const json: unknown = await response.json();
      return Response.json(json, { status: response.status });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return Response.json(
        { ok: false, code: "ADMIN_BACKEND_UNREACHABLE", message },
        { status: 502 },
      );
    }
  },
};
