import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildAuthRedirectUrl,
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
  const req = new Request("https://example.com/admin/import?x=1");
  const url = buildAuthRedirectUrl(req);
  assertEquals(url, "https://example.com/auth?redirect=%2Fadmin%2Fimport%3Fx%3D1");
});
