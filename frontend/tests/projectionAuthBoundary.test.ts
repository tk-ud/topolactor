import { assert } from "https://deno.land/std@0.208.0/assert/mod.ts";

Deno.test("projection auth boundary: ProjectionShell has login/register fallback and user refresh reuse", async () => {
  const src = await Deno.readTextFile(new URL("../islands/ProjectionShell.tsx", import.meta.url));
  assert(src.includes("refreshUserSession"), "projection fallback must reuse user refresh endpoint");
  assert(src.includes('probeSessionToken(token, "user")'), "projection fallback must probe user realm session");
  assert(src.includes('href="/auth"'), "projection fallback must link login projection");
  assert(src.includes('href="/auth#register"'), "projection fallback must link registration projection");
  assert(src.includes("clearSessionToken()"), "auth failure must clear client token carriers");
});

Deno.test("normal registration projection: /auth exposes distinct user registration surface", async () => {
  const src = await Deno.readTextFile(new URL("../islands/LoginManifestPanel.tsx", import.meta.url));
  assert(src.includes("registerUser"), "normal registration must call user auth API");
  assert(src.includes('id="register"'), "registration projection anchor must exist");
  assert(src.includes("承認待ち"), "registration projection must state pending approval");
  assert(!src.includes("super_auth/register"), "normal registration must not mix super_auth route");
});

Deno.test("admin projection boundary: middleware and client gate require admin realm probe", async () => {
  const middleware = await Deno.readTextFile(new URL("../routes/admin/_middleware.ts", import.meta.url));
  const gate = await Deno.readTextFile(new URL("../islands/AdminAuthGate.tsx", import.meta.url));
  assert(middleware.includes("probeDemoSessionOnBackend(token!)"), "SSR admin gate must probe backend admin session default");
  assert(gate.includes("probeAdminSessionToken"), "client admin gate must require admin session token");
});
