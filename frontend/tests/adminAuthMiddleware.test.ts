import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import type { FreshContext } from "$fresh/server.ts";
import { handler } from "../routes/admin/_middleware.ts";
import { SESSION_TOKEN_KEY } from "../lib/demoSession.ts";

const originalFetch = globalThis.fetch;
const originalBackendUrl = Deno.env.get("DEMO_BACKEND_URL");

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

function restoreFetch(): void {
  globalThis.fetch = originalFetch;
}

Deno.test("admin middleware: missing cookie redirects to /super_auth with redirect param", async () => {
  const req = new Request("https://example.com/admin/contents?x=1");
  const res = await handler(req, mockRouteContext());
  assertEquals(res.status, 302);
  assertEquals(
    res.headers.get("location"),
    "https://example.com/super_auth?redirect=%2Fadmin%2Fcontents%3Fx%3D1",
  );
});

Deno.test("admin middleware: empty demo_jwt_token cookie redirects (fail-close)", async () => {
  const req = new Request("https://example.com/admin", {
    headers: { cookie: `${SESSION_TOKEN_KEY}=` },
  });
  const res = await handler(req, mockRouteContext());
  assertEquals(res.status, 302);
  assertEquals(res.headers.get("location")?.includes("/super_auth?redirect="), true);
});

Deno.test("admin middleware: invalid cookie clears session and redirects", async () => {
  Deno.env.set("DEMO_BACKEND_URL", "http://backend.test");
  globalThis.fetch = () =>
    Promise.resolve(
      new Response(JSON.stringify({ success: false, errors: [] }), { status: 401 }),
    );

  try {
    const req = new Request("https://example.com/admin", {
      headers: { cookie: `${SESSION_TOKEN_KEY}=not-validated` },
    });
    const res = await handler(req, mockRouteContext("route-ok"));
    assertEquals(res.status, 302);
    assertEquals(res.headers.get("set-cookie")?.includes("Max-Age=0"), true);
  } finally {
    restoreFetch();
    if (originalBackendUrl === undefined) Deno.env.delete("DEMO_BACKEND_URL");
    else Deno.env.set("DEMO_BACKEND_URL", originalBackendUrl);
  }
});

Deno.test("admin middleware: valid backend session probe passes through", async () => {
  Deno.env.set("DEMO_BACKEND_URL", "http://backend.test");
  globalThis.fetch = (input) => {
    const url = typeof input === "string" ? input : input instanceof Request ? input.url : input.href;
    if (url === "http://backend.test/auth/session?expected=admin") {
      return Promise.resolve(
        new Response(JSON.stringify({ success: true, subject: "demo", role: "admin" }), {
          status: 200,
        }),
      );
    }
    return originalFetch(input);
  };

  try {
    const req = new Request("https://example.com/admin/manifests", {
      headers: { cookie: `${SESSION_TOKEN_KEY}=${encodeURIComponent("eyJ.demo.token")}` },
    });
    const res = await handler(req, mockRouteContext("route-ok"));
    assertEquals(res.status, 200);
    assertEquals(await res.text(), "route-ok");
  } finally {
    restoreFetch();
    if (originalBackendUrl === undefined) Deno.env.delete("DEMO_BACKEND_URL");
    else Deno.env.set("DEMO_BACKEND_URL", originalBackendUrl);
  }
});

Deno.test("admin middleware: cookie present but backend unconfigured redirects and clears", async () => {
  Deno.env.delete("DEMO_BACKEND_URL");
  const req = new Request("https://example.com/admin", {
    headers: { cookie: `${SESSION_TOKEN_KEY}=any-token` },
  });
  const res = await handler(req, mockRouteContext());
  assertEquals(res.status, 302);
  assertEquals(res.headers.get("set-cookie")?.includes("Max-Age=0"), true);
  if (originalBackendUrl !== undefined) Deno.env.set("DEMO_BACKEND_URL", originalBackendUrl);
});

Deno.test("admin middleware: non-route destination skips session gate", async () => {
  const req = new Request("https://example.com/admin");
  const res = await handler(req, mockNonRouteContext());
  assertEquals(res.status, 200);
  assertEquals(await res.text(), "static");
});
