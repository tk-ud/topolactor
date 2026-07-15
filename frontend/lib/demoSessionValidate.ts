/**
 * Server-side demo session validation against DEMO_BACKEND_URL/auth/session.
 * Fail-close: unreachable backend or non-200 → invalid (caller should clear client carriers).
 */
export async function probeDemoSessionOnBackend(
  token: string,
  expected: "admin" | "user" = "admin",
): Promise<boolean> {
  const backendUrl = Deno.env.get("DEMO_BACKEND_URL");
  if (!backendUrl) return false;

  try {
    const response = await fetch(
      `${backendUrl}/auth/session?expected=${expected}`,
      { headers: { Authorization: `Bearer ${token}` } },
    );
    if (!response.ok) return false;
    const json: unknown = await response.json();
    return (
      typeof json === "object" &&
      json !== null &&
      !Array.isArray(json) &&
      "success" in json &&
      (json as { success: boolean }).success === true
    );
  } catch {
    return false;
  }
}

/**
 * Validates a JWT against GET /auth/session with no ?expected= filter — accepts any valid,
 * unexpired, correctly-signed token regardless of role (admin or user). Used by authenticated
 * (not admin-only) surfaces: Normal dashboard, Team Dashboard, Personal Page.
 */
export async function probeAuthenticatedSessionOnBackend(token: string): Promise<boolean> {
  const backendUrl = Deno.env.get("DEMO_BACKEND_URL");
  if (!backendUrl) return false;

  try {
    const response = await fetch(`${backendUrl}/auth/session`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!response.ok) return false;
    const json: unknown = await response.json();
    return (
      typeof json === "object" &&
      json !== null &&
      !Array.isArray(json) &&
      "success" in json &&
      (json as { success: boolean }).success === true
    );
  } catch {
    return false;
  }
}
