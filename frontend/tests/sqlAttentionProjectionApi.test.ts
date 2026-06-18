import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { fetchSqlAttentionProjection } from "../api/sqlAttentionProjection.ts";

Deno.test("fetchSqlAttentionProjection returns parsed status without fallback", async () => {
  const oldFetch = globalThis.fetch;
  globalThis.fetch = (() => Promise.resolve(new Response(JSON.stringify({
    success: true,
    emission: {
      data: {
        status: "missing_policy",
        statusDetail: "No policy",
        candidates: [],
        evaluatedAt: "2026-01-01T00:00:00Z",
      },
    },
  }), { status: 200 }))) as typeof fetch;

  try {
    const result = await fetchSqlAttentionProjection("src", "token");
    assertEquals(result.success, true);
    assertEquals(result.result?.status, "missing_policy");
    assertEquals(result.result?.lane, "sql_attention_projection");
  } finally {
    globalThis.fetch = oldFetch;
  }
});
