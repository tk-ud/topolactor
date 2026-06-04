/** Shared Fresh → DEMO_BACKEND_URL proxy helpers for auth lanes. */

export function getBackendUrl(): string | null {
  const url = Deno.env.get("DEMO_BACKEND_URL");
  return url && url.trim().length > 0 ? url : null;
}

export function backendNotConfiguredResponse(): Response {
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

export async function proxyJson(
  backendPath: string,
  init: RequestInit,
): Promise<Response> {
  const backendUrl = getBackendUrl();
  if (!backendUrl) return backendNotConfiguredResponse();

  try {
    const response = await fetch(`${backendUrl}${backendPath}`, init);
    const json: unknown = await response.json();
    const headers = new Headers({ "Content-Type": "application/json" });
    const setCookie = response.headers.get("Set-Cookie");
    if (setCookie) headers.set("Set-Cookie", setCookie);
    return Response.json(json, { status: response.status, headers });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return Response.json(
      { success: false, errors: [{ code: "AUTH_BACKEND_UNREACHABLE", message }] },
      { status: 502 },
    );
  }
}
