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

export async function probeDemoSessionToken(token: string): Promise<boolean> {
  return await probeSessionToken(token);
}

export async function probeAdminSessionToken(token: string): Promise<boolean> {
  return await probeSessionToken(token, "admin");
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
