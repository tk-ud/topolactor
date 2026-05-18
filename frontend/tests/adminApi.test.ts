/**
 * Admin API client tests — verifies fetchContextTokens, createContextToken,
 * and deprecateContextToken response-to-behavior mapping.
 *
 * These tests mock globalThis.fetch to exercise the client logic without
 * requiring a running backend or Fresh server.
 *
 * Test scope:
 *   - 501 → null (not configured)
 *   - 401 → throw with "HTTP 401"
 *   - 200 → parsed response
 *   - 409 / 422 → error response body returned (not thrown)
 *   - 404 → error response body returned (not thrown)
 *
 * Authorization header forwarding is covered by getAuthHeaders()
 * which includes the Bearer token from sessionStorage when present.
 * (sessionStorage is not available in Deno; that path is exercised in browser.)
 */

import { assertEquals, assertRejects } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  fetchContextTokens,
  createContextToken,
  deprecateContextToken,
  type ContextToken,
} from "../api/adminApi.ts";

// ---------------------------------------------------------------------------
// fetch mock helpers
// ---------------------------------------------------------------------------

function makeFetch(status: number, body: unknown): typeof globalThis.fetch {
  return (_input: string | URL | Request, _init?: RequestInit) =>
    Promise.resolve(
      new Response(JSON.stringify(body), {
        status,
        headers: { "Content-Type": "application/json" },
      }),
    );
}

// ---------------------------------------------------------------------------
// fetchContextTokens
// ---------------------------------------------------------------------------

Deno.test("fetchContextTokens: 501 → returns null (not configured)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(501, { ok: false, code: "ADMIN_BACKEND_NOT_CONFIGURED" });
  try {
    const result = await fetchContextTokens();
    assertEquals(result, null);
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("fetchContextTokens: 401 → throws HTTP 401", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(401, { ok: false });
  try {
    await assertRejects(
      () => fetchContextTokens(),
      Error,
      "HTTP 401",
    );
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("fetchContextTokens: 200 → returns token array", async () => {
  const original = globalThis.fetch;
  const tokens: ContextToken[] = [
    { tokenId: "00000000-0000-0000-0000-000000000021", label: "active", group: "status", value: 1.0, status: "active" },
  ];
  globalThis.fetch = makeFetch(200, tokens);
  try {
    const result = await fetchContextTokens();
    assertEquals(result?.length, 1);
    assertEquals(result?.[0].label, "active");
  } finally {
    globalThis.fetch = original;
  }
});

// ---------------------------------------------------------------------------
// createContextToken
// ---------------------------------------------------------------------------

Deno.test("createContextToken: 401 → throws HTTP 401", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(401, { ok: false });
  try {
    await assertRejects(
      () => createContextToken({ label: "new_token", group: null, value: 0.5 }),
      Error,
      "HTTP 401",
    );
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("createContextToken: 200 → returns ok:true with tokenId", async () => {
  const original = globalThis.fetch;
  const tokenId = "aaaaaaaa-0000-0000-0000-000000000001";
  globalThis.fetch = makeFetch(200, { ok: true, tokenId, message: "Token created." });
  try {
    const result = await createContextToken({ label: "new_token", group: null, value: 0.5 });
    assertEquals(result.ok, true);
    assertEquals(result.tokenId, tokenId);
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("createContextToken: 409 → returns ok:false with conflict message (not thrown)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(409, {
    ok: false,
    message: "A token with this label and group already exists.",
    errorCode: "DUPLICATE_LABEL_GROUP",
  });
  try {
    const result = await createContextToken({ label: "dup_token", group: "status", value: 0.5 });
    assertEquals(result.ok, false);
    assertEquals((result as { errorCode?: string }).errorCode, "DUPLICATE_LABEL_GROUP");
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("createContextToken: 422 → returns ok:false (not thrown)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(422, { ok: false, message: "label is required.", errorCode: "LABEL_REQUIRED" });
  try {
    const result = await createContextToken({ label: "", group: null, value: 0.5 });
    assertEquals(result.ok, false);
  } finally {
    globalThis.fetch = original;
  }
});

// ---------------------------------------------------------------------------
// deprecateContextToken
// ---------------------------------------------------------------------------

Deno.test("deprecateContextToken: 401 → throws HTTP 401", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(401, { ok: false });
  try {
    await assertRejects(
      () => deprecateContextToken("00000000-0000-0000-0000-000000000021"),
      Error,
      "HTTP 401",
    );
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("deprecateContextToken: 200 → returns ok:true", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(200, { ok: true, message: "Token deprecated." });
  try {
    const result = await deprecateContextToken("00000000-0000-0000-0000-000000000021");
    assertEquals(result.ok, true);
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("deprecateContextToken: 404 → returns ok:false (not thrown)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(404, { ok: false, message: "Token not found." });
  try {
    const result = await deprecateContextToken("00000000-0000-0000-0000-000000000099");
    assertEquals(result.ok, false);
    assertEquals(result.message, "Token not found.");
  } finally {
    globalThis.fetch = original;
  }
});
