import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { probeDemoSessionOnBackend } from "../lib/demoSessionValidate.ts";

const originalFetch = globalThis.fetch;
const originalBackendUrl = Deno.env.get("DEMO_BACKEND_URL");

function restoreFetch(): void {
  globalThis.fetch = originalFetch;
}

Deno.test("probeDemoSessionOnBackend: returns false when DEMO_BACKEND_URL unset", async () => {
  Deno.env.delete("DEMO_BACKEND_URL");
  assertEquals(await probeDemoSessionOnBackend("any-token"), false);
  if (originalBackendUrl !== undefined) Deno.env.set("DEMO_BACKEND_URL", originalBackendUrl);
});

Deno.test("probeDemoSessionOnBackend: returns true on backend success", async () => {
  Deno.env.set("DEMO_BACKEND_URL", "http://backend.test");
  globalThis.fetch = () =>
    Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));

  try {
    assertEquals(await probeDemoSessionOnBackend("valid-token"), true);
  } finally {
    restoreFetch();
    if (originalBackendUrl === undefined) Deno.env.delete("DEMO_BACKEND_URL");
    else Deno.env.set("DEMO_BACKEND_URL", originalBackendUrl);
  }
});

Deno.test("probeDemoSessionOnBackend: returns false on backend 401", async () => {
  Deno.env.set("DEMO_BACKEND_URL", "http://backend.test");
  globalThis.fetch = () =>
    Promise.resolve(new Response(JSON.stringify({ success: false }), { status: 401 }));

  try {
    assertEquals(await probeDemoSessionOnBackend("expired-token"), false);
  } finally {
    restoreFetch();
    if (originalBackendUrl === undefined) Deno.env.delete("DEMO_BACKEND_URL");
    else Deno.env.set("DEMO_BACKEND_URL", originalBackendUrl);
  }
});
