import type { TopologyProjectionCandidate, TopologyProjectionStatus } from "../components/SqlAttentionProjectionPanel.tsx";

export type SqlAttentionProjectionResult = {
  status: TopologyProjectionStatus;
  statusDetail?: string;
  candidates: TopologyProjectionCandidate[];
  evaluatedAt: string;
};

export type SqlAttentionProjectionResponse = {
  success: boolean;
  result?: SqlAttentionProjectionResult;
  errors?: Array<{ code?: string; message?: string }>;
};

export async function fetchSqlAttentionProjection(sourceSetId: string, token?: string): Promise<SqlAttentionProjectionResponse> {
  try {
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const response = await fetch(`/api/sql-attention/topology-projection?sourceSetId=${encodeURIComponent(sourceSetId)}`, { headers });
    const json: unknown = await response.json();
    if (typeof json === "object" && json !== null && !Array.isArray(json) && "success" in json) {
      return json as SqlAttentionProjectionResponse;
    }
    return { success: false, errors: [{ message: "sql_attention_projection: unexpected response shape" }] };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ code: "SQL_ATTENTION_PROJECTION_FETCH_FAILED", message }] };
  }
}
