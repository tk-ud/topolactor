import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import type { FreshContext } from "$fresh/server.ts";
import { handler } from "../routes/admin/_middleware.ts";
import { SESSION_TOKEN_KEY } from "../lib/demoSession.ts";

function mockRouteContext(nextBody = "ok"): FreshContext {
  return {
    destination: "route",
    next: async () => new Response(nextBody, { status: 200 }),
  } as FreshContext;
}

function mockNonRouteContext(): FreshContext {
  return {
    destination: "static",
    next: async () => new Response("static", { status: 200 }),
  } as FreshContext;
}

Deno.test("admin middleware: missing cookie redirects to /auth with redirect param", async () => {
  const req = new Request("https://example.com/admin/import?x=1");
  const res = await handler(req, mockRouteContext());
  assertEquals(res.status, 302);
  assertEquals(
    res.headers.get("location"),
    "https://example.com/auth?redirect=%2Fadmin%2Fimport%3Fx%3D1",
  );
});

Deno.test("admin middleware: empty demo_jwt_token cookie redirects (fail-close)", async () => {
  const req = new Request("https://example.com/admin", {
    headers: { cookie: `${SESSION_TOKEN_KEY}=` },
  });
  const res = await handler(req, mockRouteContext());
  assertEquals(res.status, 302);
  assertEquals(res.headers.get("location")?.includes("/auth?redirect="), true);
});

Deno.test("admin middleware: arbitrary cookie passes presence gate (not auth validation)", async () => {
  const req = new Request("https://example.com/admin", {
    headers: { cookie: `${SESSION_TOKEN_KEY}=not-validated-by-middleware` },
  });
  const res = await handler(req, mockRouteContext("route-ok"));
  assertEquals(res.status, 200);
  assertEquals(await res.text(), "route-ok");
});

Deno.test("admin middleware: non-empty cookie from login-shaped value passes presence gate", async () => {
  const req = new Request("https://example.com/admin/runtime", {
    headers: { cookie: `${SESSION_TOKEN_KEY}=${encodeURIComponent("eyJ.demo.token")}` },
  });
  const res = await handler(req, mockRouteContext());
  assertEquals(res.status, 200);
});

Deno.test("admin middleware: non-route destination skips presence gate", async () => {
  const req = new Request("https://example.com/admin");
  const res = await handler(req, mockNonRouteContext());
  assertEquals(res.status, 200);
  assertEquals(await res.text(), "static");
});
