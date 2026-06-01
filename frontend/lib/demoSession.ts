/**
 * Demo session carriers for Registrar admin UI (client + SSR).
 *
 * Boundary (SSOT: Registrar admin = intent submission / projection; authz = backend):
 * - `/admin` SSR middleware and client gates probe `GET /auth/session` (via backend or /api/auth/session).
 * - Invalid, expired, or unverifiable tokens are cleared from sessionStorage + cookie (fail-close).
 * - Operation authz remains backend `Authorization: Bearer` on each API call — explicit, no silent fallback.
 */
export const SESSION_TOKEN_KEY = "demo_jwt_token";

/** Shared copy for UI technical-details — keep middleware/tests aligned with this boundary. */
export const DEMO_ADMIN_SSR_PRESENCE_GATE_SUMMARY =
  "/admin の SSR は backend の /auth/session で JWT を検証します。検証不能・無効トークンは cookie を削除して /auth へ戻します。";

export const DEMO_ADMIN_FINAL_AUTH_BOUNDARY_SUMMARY =
  "API 操作の最終認証境界は backend の Authorization 検証です（無効トークンは AUTH_TOKEN_MISSING 等で明示失敗）。";

const DEFAULT_MAX_AGE_SEC = 60 * 60 * 24;

/** Parse cookie value only — no validity check. */
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

/** True when a non-empty demo session token string was parsed (presence only, not validated). */
export function isDemoSessionPresent(token: string | null | undefined): boolean {
  return typeof token === "string" && token.trim().length > 0;
}

/** Presence gate input for SSR /admin middleware — not an auth verification result. */
export function hasDemoSessionPresenceFromRequest(req: Request): boolean {
  return isDemoSessionPresent(parseSessionTokenFromCookieHeader(req.headers.get("cookie")));
}

/**
 * Read demo session token from request cookie (parse only).
 * Callers must not treat a non-null return value as verified authentication.
 */
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

/** Persist token for client API calls (sessionStorage) and SSR presence gate (cookie). */
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

/** Sync sessionStorage ↔ cookie carriers when only one side has a value (presence sync, not validation). */
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

/** 302 redirect to /auth; optional session cookie clear (Response.redirect headers are immutable). */
export function buildAuthRedirectResponse(
  req: Request,
  options?: { clearSession?: boolean },
): Response {
  const headers = new Headers({ Location: buildAuthRedirectUrl(req) });
  if (options?.clearSession) {
    headers.append("Set-Cookie", sessionTokenClearCookieHeader());
  }
  return new Response(null, { status: 302, headers });
}

/**
 * Sync carriers, probe backend session, clear storage when invalid or unverifiable.
 * Use before treating the user as logged in on /auth or inside AdminAuthGate.
 */
export async function ensureValidClientSession(
  probe: (token: string) => Promise<boolean>,
): Promise<string | null> {
  const token = syncClientSessionToken();
  if (!isDemoSessionPresent(token)) return null;
  const valid = await probe(token!);
  if (!valid) {
    clearSessionToken();
    return null;
  }
  return token;
}
