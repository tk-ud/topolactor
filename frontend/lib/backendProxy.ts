/**
 * Shared thin-proxy helper for Fresh API routes that forward directly to a backend HTTP endpoint
 * (auth/self-credential, admin credential/session, team-markdown read) rather than the manifest
 * dispatch pipeline. Mirrors the existing pattern in routes/api/auth/session.ts — kept as one
 * reusable helper instead of duplicating the same fetch/error-shape boilerplate per route file.
 */
export type ProxyOptions = {
  method?: string;
  /** Default true: missing Authorization header short-circuits with 401 before reaching the backend. */
  requireAuth?: boolean;
};

export async function proxyToBackend(
  req: Request,
  backendPath: string,
  opts: ProxyOptions = {},
): Promise<Response> {
  const backendUrl = Deno.env.get("DEMO_BACKEND_URL");
  if (!backendUrl) {
    return Response.json(
      {
        success: false,
        errors: [{
          code: "AUTH_BACKEND_NOT_CONFIGURED",
          message: "DEMO_BACKEND_URL is not set. Backend proxy not wired.",
        }],
      },
      { status: 501 },
    );
  }

  const method = opts.method ?? req.method;
  const requireAuth = opts.requireAuth ?? true;
  const authHeader = req.headers.get("Authorization");
  if (requireAuth && !authHeader) {
    return Response.json(
      {
        success: false,
        errors: [{ code: "AUTH_TOKEN_MISSING", message: "Authorization token is required." }],
      },
      { status: 401 },
    );
  }

  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (authHeader) headers["Authorization"] = authHeader;
  const cookieHeader = req.headers.get("Cookie");
  if (cookieHeader) headers["Cookie"] = cookieHeader;

  const init: RequestInit = { method, headers };
  if (method !== "GET" && method !== "HEAD") {
    init.body = await req.text();
  }

  try {
    const response = await fetch(`${backendUrl}${backendPath}`, init);
    const json: unknown = await response.json();
    return Response.json(json, { status: response.status });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return Response.json(
      { success: false, errors: [{ code: "AUTH_BACKEND_UNREACHABLE", message }] },
      { status: 502 },
    );
  }
}
