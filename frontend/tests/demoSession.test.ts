import { assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildAuthRedirectResponse,
  buildAuthRedirectUrl,
  DEMO_ADMIN_FINAL_AUTH_BOUNDARY_SUMMARY,
  DEMO_ADMIN_SSR_PRESENCE_GATE_SUMMARY,
  hasDemoSessionPresenceFromRequest,
  isDemoSessionPresent,
  parseSessionTokenFromCookieHeader,
  sessionTokenClearCookieHeader,
  sessionTokenSetCookieHeader,
  SESSION_TOKEN_KEY,
} from "../lib/demoSession.ts";

Deno.test("parseSessionTokenFromCookieHeader: reads demo_jwt_token", () => {
  const token = "eyJhbGci.test";
  assertEquals(
    parseSessionTokenFromCookieHeader(`other=1; ${SESSION_TOKEN_KEY}=${encodeURIComponent(token)}`),
    token,
  );
});

Deno.test("parseSessionTokenFromCookieHeader: missing cookie returns null", () => {
  assertEquals(parseSessionTokenFromCookieHeader(null), null);
  assertEquals(parseSessionTokenFromCookieHeader("foo=bar"), null);
});

Deno.test("parseSessionTokenFromCookieHeader: empty value returns null", () => {
  assertEquals(parseSessionTokenFromCookieHeader(`${SESSION_TOKEN_KEY}=`), null);
});

Deno.test("isDemoSessionPresent: arbitrary non-empty string is presence-only (not validated)", () => {
  assertEquals(isDemoSessionPresent("not-a-valid-jwt"), true);
  assertEquals(isDemoSessionPresent("   "), false);
  assertEquals(isDemoSessionPresent(null), false);
});

Deno.test("hasDemoSessionPresenceFromRequest: arbitrary cookie satisfies presence gate only", () => {
  const req = new Request("https://example.com/admin", {
    headers: { cookie: `${SESSION_TOKEN_KEY}=arbitrary-unvalidated` },
  });
  assertEquals(hasDemoSessionPresenceFromRequest(req), true);
});

Deno.test("hasDemoSessionPresenceFromRequest: missing cookie is absent", () => {
  const req = new Request("https://example.com/admin");
  assertFalse(hasDemoSessionPresenceFromRequest(req));
});

Deno.test("boundary summaries document session probe vs backend auth", () => {
  assertEquals(DEMO_ADMIN_SSR_PRESENCE_GATE_SUMMARY.includes("/auth/session"), true);
  assertEquals(DEMO_ADMIN_SSR_PRESENCE_GATE_SUMMARY.includes("検証"), true);
  assertEquals(DEMO_ADMIN_FINAL_AUTH_BOUNDARY_SUMMARY.includes("backend"), true);
  assertEquals(DEMO_ADMIN_FINAL_AUTH_BOUNDARY_SUMMARY.includes("AUTH_TOKEN_MISSING"), true);
});

Deno.test("sessionTokenSetCookieHeader includes Path and SameSite", () => {
  const header = sessionTokenSetCookieHeader("abc");
  assertEquals(header.includes(`${SESSION_TOKEN_KEY}=`), true);
  assertEquals(header.includes("Path=/"), true);
  assertEquals(header.includes("SameSite=Lax"), true);
});

Deno.test("sessionTokenClearCookieHeader expires cookie", () => {
  assertEquals(sessionTokenClearCookieHeader().includes("Max-Age=0"), true);
});

Deno.test("buildAuthRedirectUrl: preserves admin path in redirect param", () => {
  const req = new Request("https://example.com/admin/contents?x=1");
  const url = buildAuthRedirectUrl(req);
  assertEquals(url, "https://example.com/auth?redirect=%2Fadmin%2Fcontents%3Fx%3D1");
});

Deno.test("buildAuthRedirectResponse: clearSession sets Set-Cookie on mutable headers", () => {
  const req = new Request("https://example.com/admin");
  const res = buildAuthRedirectResponse(req, { clearSession: true });
  assertEquals(res.status, 302);
  assertEquals(res.headers.get("Location"), "https://example.com/auth?redirect=%2Fadmin");
  assertEquals(res.headers.get("Set-Cookie")?.includes("Max-Age=0"), true);
});
