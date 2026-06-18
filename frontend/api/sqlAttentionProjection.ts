import { dispatchOperation } from "./dispatch.ts";
import type { TopologyProjectionCandidate, TopologyProjectionStatus } from "../components/SqlAttentionProjectionPanel.tsx";

export type SqlAttentionProjectionResult = {
  status: TopologyProjectionStatus;
  statusDetail?: string;
  candidates: TopologyProjectionCandidate[];
  evaluatedAt: string;
  lane: "sql_attention_projection";
};

export type SqlAttentionProjectionResponse = {
  success: boolean;
  result?: SqlAttentionProjectionResult;
  errors?: Array<{ code?: string; message?: string }>;
};

/**
 * Fetches SQL Attention topology projection via the admin dispatch pipeline.
 * Route: POST /api/dispatch { layer: "sql_attention", action: "list_projection", payload: { sourceSetId } }
 * → AdminRuntime.ExecuteDataAsync("sql_attention:list_projection")
 * → SqlAttentionTopologyProjectionRuntime.ProjectAsync
 *
 * Frontend is a projection surface only. No promotion authority from this payload.
 */
export async function fetchSqlAttentionProjection(sourceSetId: string, token?: string): Promise<SqlAttentionProjectionResponse> {
  const response = await dispatchOperation(
    {
      operationType: "admin",
      target: "admin",
      layer: "sql_attention",
      action: "list_projection",
      payload: { sourceSetId },
      triggerKind: "client",
    },
    token,
  );

  if (!response.success) {
    return {
      success: false,
      errors: response.errors?.map((e) => ({ code: e.Code ?? e.code, message: e.Message ?? e.message })) ?? [
        { message: "sql_attention:list_projection dispatch failed" },
      ],
    };
  }

  const data = response.emission?.data;
  if (data == null || typeof data !== "object" || Array.isArray(data)) {
    return { success: false, errors: [{ message: "sql_attention:list_projection: unexpected emission.data shape" }] };
  }

  const d = data as Record<string, unknown>;
  const result: SqlAttentionProjectionResult = {
    status: (typeof d.status === "string" ? d.status : "db_unavailable") as TopologyProjectionStatus,
    statusDetail: typeof d.statusDetail === "string" ? d.statusDetail : undefined,
    candidates: Array.isArray(d.candidates) ? (d.candidates as TopologyProjectionCandidate[]) : [],
    evaluatedAt: typeof d.evaluatedAt === "string" ? d.evaluatedAt : new Date().toISOString(),
    lane: "sql_attention_projection",
  };

  return { success: true, result };
}
