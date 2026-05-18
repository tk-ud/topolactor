import { Handlers } from "$fresh/server.ts";

/**
 * GET  /api/admin/context-token-registry  — list all context tokens
 * POST /api/admin/context-token-registry  — create a new context token
 *
 * Both routes proxy to the backend admin endpoint.
 * Returns 501 (ADMIN_BACKEND_NOT_CONFIGURED) when DEMO_BACKEND_URL is not set.
 * Returns 502 when the backend is unreachable.
 * Returns 401 when no valid JWT is provided.
 *
 * Backend route: backend/endpoint/AdminEndpoint.cs
 */

function getBackendUrl(): string | null {
  return Deno.env.get("DEMO_BACKEND_URL") ?? null;
}

function notConfigured(): Response {
  return Response.json(
    {
      ok: false,
      code: "ADMIN_BACKEND_NOT_CONFIGURED",
      message: "DEMO_BACKEND_URL is not set. Admin registry endpoint not wired.",
    },
    { status: 501 },
  );
}

async function proxyRequest(req: Request, backendUrl: string, path: string): Promise<Response> {
  try {
    const authHeader = req.headers.get("Authorization");
    const headers: Record<string, string> = { "Content-Type": "application/json" };
    if (authHeader) headers["Authorization"] = authHeader;

    const method = req.method;
    const body = method === "POST" ? await req.text() : undefined;

    const response = await fetch(`${backendUrl}${path}`, { method, headers, body });
    const json: unknown = await response.json();
    return Response.json(json, { status: response.status });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return Response.json(
      { ok: false, code: "ADMIN_BACKEND_UNREACHABLE", message },
      { status: 502 },
    );
  }
}

export const handler: Handlers = {
  async GET(req) {
    const backendUrl = getBackendUrl();
    if (!backendUrl) return notConfigured();
    return await proxyRequest(req, backendUrl, "/admin/context-token-registry");
  },

  async POST(req) {
    const backendUrl = getBackendUrl();
    if (!backendUrl) return notConfigured();
    return await proxyRequest(req, backendUrl, "/admin/context-token-registry");
  },
};
