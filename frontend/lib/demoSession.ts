/** Demo auth token — sessionStorage key and HttpOnly-less cookie name (SSR middleware). */
export const SESSION_TOKEN_KEY = "demo_jwt_token";

const DEFAULT_MAX_AGE_SEC = 60 * 60 * 24;

export function parseSessionTokenFromCookieHeader(
  cookieHeader: string | null,
): string | null {
  if (!cookieHeader) return null;
  for (const part of cookieHeader.split(";")) {
    const trimmed = part.trim();
    const eq = trimmed.indexOf("=");
    if (eq < 0) continue;
    const name = trimmed.slice(0, eq);
    if (name !== SESSION_TOKEN_KEY) continue;
    const raw = trimmed.slice(eq + 1);
    if (!raw) return null;
    try {
      return decodeURIComponent(raw);
    } catch {
      return raw;
    }
  }
  return null;
}

export function getSessionTokenFromRequest(req: Request): string | null {
  return parseSessionTokenFromCookieHeader(req.headers.get("cookie"));
}

export function sessionTokenSetCookieHeader(
  token: string,
  maxAgeSec = DEFAULT_MAX_AGE_SEC,
): string {
  const encoded = encodeURIComponent(token);
  return `${SESSION_TOKEN_KEY}=${encoded}; Path=/; SameSite=Lax; Max-Age=${maxAgeSec}`;
}

export function sessionTokenClearCookieHeader(): string {
  return `${SESSION_TOKEN_KEY}=; Path=/; SameSite=Lax; Max-Age=0`;
}

/** Persist token for client API calls (sessionStorage) and SSR admin middleware (cookie). */
export function persistSessionToken(token: string): void {
  if (typeof globalThis.sessionStorage !== "undefined") {
    sessionStorage.setItem(SESSION_TOKEN_KEY, token);
  }
  if (typeof document !== "undefined") {
    document.cookie = sessionTokenSetCookieHeader(token);
  }
}

export function clearSessionToken(): void {
  if (typeof globalThis.sessionStorage !== "undefined") {
    sessionStorage.removeItem(SESSION_TOKEN_KEY);
  }
  if (typeof document !== "undefined") {
    document.cookie = sessionTokenClearCookieHeader();
  }
}

export function readClientSessionToken(): string | null {
  if (typeof globalThis.sessionStorage !== "undefined") {
    const stored = sessionStorage.getItem(SESSION_TOKEN_KEY);
    if (stored) return stored;
  }
  if (typeof document !== "undefined") {
    return parseSessionTokenFromCookieHeader(document.cookie);
  }
  return null;
}

/** Sync sessionStorage ↔ cookie when only one side has the token. */
export function syncClientSessionToken(): string | null {
  const fromStorage =
    typeof globalThis.sessionStorage !== "undefined"
      ? sessionStorage.getItem(SESSION_TOKEN_KEY)
      : null;
  const fromCookie =
    typeof document !== "undefined"
      ? parseSessionTokenFromCookieHeader(document.cookie)
      : null;

  if (fromStorage && !fromCookie) {
    if (typeof document !== "undefined") {
      document.cookie = sessionTokenSetCookieHeader(fromStorage);
    }
    return fromStorage;
  }
  if (fromCookie && !fromStorage) {
    if (typeof globalThis.sessionStorage !== "undefined") {
      sessionStorage.setItem(SESSION_TOKEN_KEY, fromCookie);
    }
    return fromCookie;
  }
  return fromStorage ?? fromCookie;
}

export function buildAuthRedirectUrl(req: Request): string {
  const requestUrl = new URL(req.url);
  const redirectPath = `${requestUrl.pathname}${requestUrl.search}`;
  const authUrl = new URL("/auth", requestUrl.origin);
  if (redirectPath.startsWith("/") && !redirectPath.startsWith("//")) {
    authUrl.searchParams.set("redirect", redirectPath);
  }
  return authUrl.toString();
}
