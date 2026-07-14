/** Login request sent to auth login endpoints. */
export type LoginRequest = {
  username: string;
  password: string;
};

/** Structured error matching backend ValidationError { Code, Message }. */
export type AuthError = {
  code?: string;
  Code?: string;
  message?: string;
  Message?: string;
};

/** Response from login endpoints. */
export type LoginResponse = {
  success: boolean;
  token?: string;
  errors?: AuthError[];
};

export type RegisterResponse = {
  success: boolean;
  username?: string;
  approve?: boolean;
  status?: string;
  errors?: AuthError[];
};

export type RefreshResponse = LoginResponse;

export type LogoutResponse = {
  success: boolean;
  errors?: AuthError[];
};

/** Response from GET /api/auth/session. */
export type SessionResponse = {
  success: boolean;
  subject?: string;
  role?: string;
  realm?: string;
  audience?: string;
  errors?: AuthError[];
};

export type LoginManifestResponse = {
  success: boolean;
  authActionBinding?: Record<string, unknown>;
  uiProjection?: {
    surface?: string;
    fields?: string[];
    labels?: Record<string, string>;
    redirect_success?: string;
    redirect_failure?: string | null;
  };
  errors?: AuthError[];
};

export const USER_LOGIN_MANIFEST_ID = "00000000-0000-0000-0000-000000000090";

/** Active manifest exposing auth.user for Step 2.5 remote relationship targets. */
export const AUTH_RELATIONSHIP_BOUNDARY_MANIFEST_ID =
  "00000000-0000-0000-0000-000000000091";

/** Extract display string from AuthError regardless of casing. */
export function authErrorText(e: AuthError): string {
  const msg = e.message ?? e.Message;
  const code = e.code ?? e.Code;
  if (msg && code) return `[${code}] ${msg}`;
  return msg ?? code ?? "unknown auth error";
}

async function postLogin(path: string, req: LoginRequest): Promise<LoginResponse> {
  try {
    const response = await fetch(path, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify(req),
    });
    const json: unknown = await response.json();
    if (
      typeof json === "object" &&
      json !== null &&
      !Array.isArray(json) &&
      "success" in json
    ) {
      return json as LoginResponse;
    }
    return {
      success: false,
      errors: [{ message: `auth: unexpected response shape from ${path}` }],
    };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}

/** User realm login — POST /api/auth/login */
export async function loginUser(req: LoginRequest): Promise<LoginResponse> {
  return await postLogin("/api/auth/login", req);
}

export async function registerUser(req: LoginRequest): Promise<RegisterResponse> {
  try {
    const response = await fetch("/api/auth/register", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify(req),
    });
    const json: unknown = await response.json();
    if (
      typeof json === "object" &&
      json !== null &&
      !Array.isArray(json) &&
      "success" in json
    ) {
      return json as RegisterResponse;
    }
    return { success: false, errors: [{ message: "auth: unexpected register response shape" }] };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}

export async function refreshUserSession(): Promise<RefreshResponse> {
  try {
    const response = await fetch("/api/auth/refresh", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: "{}",
    });
    const json: unknown = await response.json();
    if (
      response.ok &&
      typeof json === "object" &&
      json !== null &&
      !Array.isArray(json) &&
      "success" in json
    ) {
      return json as RefreshResponse;
    }
    return { success: false, errors: [{ message: "auth: refresh failed" }] };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}

/** User logout — POST /api/auth/logout */
export async function logoutUser(): Promise<LogoutResponse> {
  try {
    const response = await fetch("/api/auth/logout", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: "{}",
    });
    const json: unknown = await response.json();
    if (
      typeof json === "object" &&
      json !== null &&
      !Array.isArray(json) &&
      "success" in json
    ) {
      return json as LogoutResponse;
    }
    return { success: false, errors: [{ message: "auth: unexpected logout response shape" }] };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}

/** Admin session refresh — POST /api/super_auth/refresh */
export async function refreshAdminSession(): Promise<RefreshResponse> {
  try {
    const response = await fetch("/api/super_auth/refresh", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: "{}",
    });
    const json: unknown = await response.json();
    if (
      response.ok &&
      typeof json === "object" &&
      json !== null &&
      !Array.isArray(json) &&
      "success" in json
    ) {
      return json as RefreshResponse;
    }
    return { success: false, errors: [{ message: "auth: admin refresh failed" }] };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}

/** Admin realm login — POST /api/super_auth/login */
export async function loginSuper(req: LoginRequest): Promise<LoginResponse> {
  return await postLogin("/api/super_auth/login", req);
}

/** @deprecated Use loginUser or loginSuper */
export const loginDemo = loginUser;

export async function probeSessionToken(
  token: string,
  expected?: "user" | "admin",
): Promise<boolean> {
  if (!token.trim()) return false;
  const qs = expected ? `?expected=${expected}` : "";
  try {
    const response = await fetch(`/api/auth/session${qs}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!response.ok) return false;
    const json: unknown = await response.json();
    if (
      typeof json === "object" &&
      json !== null &&
      !Array.isArray(json) &&
      "success" in json
    ) {
      return (json as SessionResponse).success === true;
    }
    return false;
  } catch {
    return false;
  }
}

/** Projection surface login — issues admin JWT if the user has admin grants, user JWT otherwise. */
export async function loginProjection(req: LoginRequest): Promise<LoginResponse> {
  return await postLogin("/api/auth/projection-login", req);
}

/**
 * Decode JWT payload claims client-side (presence inspection only — no signature verification).
 * Returns null when the token is malformed or cannot be decoded.
 */
export function getTokenClaims(token: string): Record<string, unknown> | null {
  try {
    const parts = token.split(".");
    if (parts.length !== 3) return null;
    const padded = parts[1].replace(/-/g, "+").replace(/_/g, "/").padEnd(
      parts[1].length + (4 - parts[1].length % 4) % 4,
      "=",
    );
    return JSON.parse(atob(padded)) as Record<string, unknown>;
  } catch {
    return null;
  }
}

/**
 * Refresh projection session: detects realm from the current access token and calls
 * the matching refresh endpoint. Admin tokens use the admin refresh endpoint;
 * user (or unknown realm) tokens use the user refresh endpoint.
 */
export async function refreshProjectionSession(token: string): Promise<RefreshResponse> {
  const claims = getTokenClaims(token);
  const realm = typeof claims?.realm === "string" ? claims.realm : null;
  if (realm === "admin/system") {
    return refreshAdminSession();
  }
  return refreshUserSession();
}

export async function probeDemoSessionToken(token: string): Promise<boolean> {
  return await probeSessionToken(token);
}

export async function probeAdminSessionToken(token: string): Promise<boolean> {
  return await probeSessionToken(token, "admin");
}

// ─── Self-service credential/session lifecycle ───────────────────────────────────────────────
// Every function below acts only on the caller's own account — target is resolved server-side
// from the validated JWT subject; none of these send a userId/username in the request body.

export type CurrentAccountResponse = {
  success: boolean;
  username?: string;
  role?: string;
  realm?: string;
  active?: boolean;
  approve?: boolean;
  status?: string;
  errors?: AuthError[];
};

export type SessionSummary = {
  sessionId: string;
  realm: string;
  audience: string;
  expiresAt: string;
  createdAt: string;
  isCurrent: boolean;
};

export type ListSessionsResponse = {
  success: boolean;
  sessions?: SessionSummary[];
  errors?: AuthError[];
};

async function authFetch<T>(
  path: string,
  init: RequestInit,
  token: string,
): Promise<T> {
  try {
    const response = await fetch(path, {
      ...init,
      credentials: "include",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
        ...(init.headers ?? {}),
      },
    });
    const json: unknown = await response.json();
    if (typeof json === "object" && json !== null && !Array.isArray(json) && "success" in json) {
      return json as T;
    }
    return { success: false, errors: [{ message: `unexpected response shape from ${path}` }] } as T;
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] } as T;
  }
}

/** GET /api/auth/me — current authenticated account. */
export async function getCurrentAccount(token: string): Promise<CurrentAccountResponse> {
  return authFetch<CurrentAccountResponse>("/api/auth/me", { method: "GET" }, token);
}

/** POST /api/auth/me/password — self password change. Revokes all sessions on success. */
export async function changeOwnPassword(
  token: string,
  currentPassword: string,
  newPassword: string,
): Promise<{ success: boolean; sessionsRevoked?: number; errors?: AuthError[] }> {
  return authFetch(
    "/api/auth/me/password",
    { method: "POST", body: JSON.stringify({ currentPassword, newPassword }) },
    token,
  );
}

/** GET /api/auth/me/sessions — self session list. */
export async function listOwnSessions(token: string): Promise<ListSessionsResponse> {
  return authFetch<ListSessionsResponse>("/api/auth/me/sessions", { method: "GET" }, token);
}

/** POST /api/auth/me/sessions/revoke — revoke one of the caller's own sessions. */
export async function revokeOwnSession(
  token: string,
  sessionId: string,
): Promise<{ success: boolean; errors?: AuthError[] }> {
  return authFetch(
    "/api/auth/me/sessions/revoke",
    { method: "POST", body: JSON.stringify({ sessionId }) },
    token,
  );
}

/** POST /api/auth/me/sessions/revoke-others — revoke every session except the caller's current one. */
export async function revokeOtherSessions(
  token: string,
): Promise<{ success: boolean; sessionsRevoked?: number; errors?: AuthError[] }> {
  return authFetch(
    "/api/auth/me/sessions/revoke-others",
    { method: "POST", body: "{}" },
    token,
  );
}

export async function fetchUserLoginManifest(): Promise<LoginManifestResponse> {
  try {
    const response = await fetch("/api/auth/login-manifest");
    const json: unknown = await response.json();
    if (typeof json !== "object" || json === null || Array.isArray(json)) {
      return { success: false, errors: [{ message: "Invalid login manifest response" }] };
    }
    const raw = json as Record<string, unknown>;
    return {
      success: raw.success === true,
      authActionBinding: raw.authActionBinding as Record<string, unknown> | undefined,
      uiProjection: raw.uiProjection as LoginManifestResponse["uiProjection"],
      errors: raw.errors as AuthError[] | undefined,
    };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}
