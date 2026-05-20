import { assertEquals, assertRejects } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  fetchContextTokens,
  createContextToken,
  deprecateContextToken,
  validateRegistryVector,
  type ContextToken,
} from "../api/adminApi.ts";

function makeFetch(status: number, body: unknown, capture?: (u: string | URL | Request, i?: RequestInit)=>void): typeof globalThis.fetch {
  return (input: string | URL | Request, init?: RequestInit) => {
    capture?.(input, init);
    return Promise.resolve(new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } }));
  };
}

Deno.test("fetchContextTokens uses /api/dispatch and returns token list", async () => {
  const original = globalThis.fetch;
  let reqBody = "";
  const tokens: ContextToken[] = [{ tokenId: "1", label: "a", group: null, value: 0.1, status: "active" }];
  globalThis.fetch = makeFetch(200, { success: true, emission: { data: tokens } }, (_u, i)=> reqBody=String(i?.body ?? ""));
  try {
    const result = await fetchContextTokens();
    assertEquals(result, tokens);
    assertEquals(reqBody.includes('"layer":"context_token_registry"'), true);
  } finally { globalThis.fetch = original; }
});

Deno.test("create/deprecate/validate throw on dispatch error", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(422, { success: false, errors: [{ message: "bad" }] });
  try {
    await assertRejects(() => createContextToken({ label: "x", group: null, value: 0.3 }), Error, "bad");
    await assertRejects(() => deprecateContextToken("id"), Error);
    await assertRejects(() => validateRegistryVector("relation_registry", ["x"]), Error);
  } finally { globalThis.fetch = original; }
});

Deno.test("fetchContextTokens: dispatch 501 not configured -> null", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(501, { success: false, errors: [{ code: "DISPATCH_BACKEND_NOT_CONFIGURED", message: "not configured" }] });
  try {
    const result = await fetchContextTokens();
    assertEquals(result, null);
  } finally { globalThis.fetch = original; }
});

Deno.test("validateRegistryVector: dispatch 501 not configured -> null", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(501, { success: false, errors: [{ code: "DISPATCH_BACKEND_NOT_CONFIGURED", message: "not configured" }] });
  try {
    const result = await validateRegistryVector("relation_registry", []);
    assertEquals(result, null);
  } finally { globalThis.fetch = original; }
});

Deno.test("create/deprecate: dispatch 501 not configured -> explicit throw", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(501, { success: false, errors: [{ code: "DISPATCH_BACKEND_NOT_CONFIGURED", message: "not configured" }] });
  try {
    await assertRejects(() => createContextToken({ label: "x", group: null, value: 0.3 }), Error, "DISPATCH_BACKEND_NOT_CONFIGURED");
    await assertRejects(() => deprecateContextToken("id"), Error, "DISPATCH_BACKEND_NOT_CONFIGURED");
  } finally { globalThis.fetch = original; }
});
