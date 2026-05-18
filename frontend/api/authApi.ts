/** Login request sent to /api/auth/login. */
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

/** Response from /api/auth/login. */
export type LoginResponse = {
  success: boolean;
  token?: string;
  errors?: AuthError[];
};

/** Extract display string from AuthError regardless of casing. */
export function authErrorText(e: AuthError): string {
  const msg = e.message ?? e.Message;
  const code = e.code ?? e.Code;
  if (msg && code) return `[${code}] ${msg}`;
  return msg ?? code ?? "unknown auth error";
}

/**
 * Sends a LoginRequest to /api/auth/login and returns the parsed LoginResponse.
 * On fetch-level error, returns a failed LoginResponse with the error message.
 */
export async function loginDemo(req: LoginRequest): Promise<LoginResponse> {
  try {
    const response = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
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
      errors: [{ message: "auth: unexpected response shape from /api/auth/login" }],
    };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}
