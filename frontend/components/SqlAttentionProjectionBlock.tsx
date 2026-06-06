import { JSX } from "preact";
import { useEffect, useState } from "preact/hooks";
import type { SqlAttentionProjectionChildSpec } from "../api/dispatch.ts";
import { fetchSqlAttentionProjection, type SqlAttentionProjectionResult } from "../api/sqlAttentionProjection.ts";
import { SqlAttentionProjectionPanel } from "./SqlAttentionProjectionPanel.tsx";

export function SqlAttentionProjectionBlock(
  { spec, token }: { spec: SqlAttentionProjectionChildSpec; token?: string },
): JSX.Element {
  const [result, setResult] = useState<SqlAttentionProjectionResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setResult(null);
    setError(null);
    if (!spec.sourceSetId || spec.status !== "ok") return;
    let cancelled = false;
    void (async () => {
      const response = await fetchSqlAttentionProjection(spec.sourceSetId!, token);
      if (cancelled) return;
      if (response.success && response.result) {
        setResult(response.result);
      } else {
        setError(response.errors?.map((e) => e.message ?? e.code ?? "unknown error").join("; ") ?? "sql_attention_projection unavailable");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [spec.sourceSetId, spec.status, token]);

  return (
    <section class="rounded border border-indigo-100 bg-indigo-50/40 p-3" data-recommend-lane="sql_attention_projection">
      <h4 class="m-0 text-sm font-semibold">SQL Attention projection</h4>
      <p class="mt-1 text-xs text-gray-500">
        lane: <code>{spec.lane}</code> / kind: <code>{spec.candidateKind}</code>
      </p>
      {spec.status !== "ok" || !spec.sourceSetId ? (
        <p class="mt-2 text-sm text-yellow-700">
          explicit unavailable: {spec.statusDetail ?? "sourceSetId is unresolved"}
        </p>
      ) : error ? (
        <p class="mt-2 text-sm text-red-700">explicit_error: {error}</p>
      ) : result ? (
        <SqlAttentionProjectionPanel
          status={result.status}
          statusDetail={result.statusDetail}
          candidates={result.candidates}
        />
      ) : (
        <p class="mt-2 text-sm text-gray-500">SQL Attention projection を取得中...</p>
      )}
    </section>
  );
}
