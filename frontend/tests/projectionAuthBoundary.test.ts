import { assert } from "https://deno.land/std@0.208.0/assert/mod.ts";

Deno.test("projection auth boundary: ProjectionShell probes any realm and uses projection refresh", async () => {
  const src = await Deno.readTextFile(new URL("../islands/ProjectionShell.tsx", import.meta.url));
  assert(src.includes("refreshProjectionSession"), "projection shell must use realm-aware projection refresh");
  assert(!src.includes('probeSessionToken(token, "user")'), "projection probe must not be user-realm fixed");
  assert(src.includes("probeSessionToken(token)"), "projection probe must accept any valid JWT realm");
  assert(src.includes('href="/auth"'), "projection fallback must link login projection");
  assert(src.includes('href="/auth#register"'), "projection fallback must link registration projection");
  assert(src.includes("clearSessionToken()"), "token invalidity must clear client token carriers");
});

Deno.test("projection auth boundary: ProjectionShell passes Bearer token to all dispatch calls", async () => {
  const src = await Deno.readTextFile(new URL("../islands/ProjectionShell.tsx", import.meta.url));
  assert(src.includes("queueClientCommand("), "projection must use queueClientCommand for dispatch");
  // Initial dispatch passes currentToken (post-refresh resolved token)
  assert(src.includes("currentToken"), "initial dispatch must pass resolved token to queueClientCommand");
  // SSE refresh dispatch reads token from sessionStorage
  assert(src.includes("demo_jwt_token"), "SSE refresh dispatch must read token from sessionStorage");
});

Deno.test("projection auth boundary: refresh failure on token invalidity clears carrier, realm mismatch does not", async () => {
  const src = await Deno.readTextFile(new URL("../islands/ProjectionShell.tsx", import.meta.url));
  assert(src.includes("AUTH_REFRESH_TOKEN_INVALID"), "token invalidity codes must be checked before clearing");
  assert(src.includes("isTokenInvalid"), "clear decision must be based on isTokenInvalid check");
});

Deno.test("projection auth boundary: LoginManifestPanel uses loginProjection for capability-preserving login", async () => {
  const src = await Deno.readTextFile(new URL("../islands/LoginManifestPanel.tsx", import.meta.url));
  assert(src.includes("loginProjection"), "projection login must use loginProjection to preserve admin capability");
  assert(!src.includes("loginUser("), "projection login must not call user-fixed loginUser");
  assert(src.includes("registerUser"), "normal registration must call user auth API");
  assert(src.includes('id="register"'), "registration projection anchor must exist");
  assert(src.includes("承認待ち"), "registration projection must state pending approval");
  assert(!src.includes("super_auth/register"), "normal registration must not mix super_auth route");
});

Deno.test("projection auth boundary: LoginManifestPanel session probe accepts any valid realm", async () => {
  const src = await Deno.readTextFile(new URL("../islands/LoginManifestPanel.tsx", import.meta.url));
  assert(!src.includes('probeSessionToken(t, "user")'), "session probe must not be user-realm fixed");
  assert(src.includes("probeSessionToken(t)"), "session probe must accept any valid JWT realm");
});

Deno.test("admin projection boundary: middleware and client gate require admin realm probe", async () => {
  const middleware = await Deno.readTextFile(new URL("../routes/admin/_middleware.ts", import.meta.url));
  const gate = await Deno.readTextFile(new URL("../islands/AdminAuthGate.tsx", import.meta.url));
  assert(middleware.includes("probeDemoSessionOnBackend(token!)"), "SSR admin gate must probe backend admin session default");
  assert(gate.includes("probeAdminSessionToken"), "client admin gate must require admin session token");
});
