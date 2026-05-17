import type { OperationVector } from "./resolveOperationVector.ts";

export type ResumeContext = {
  operationVector?: OperationVector;
  attractorKey?: string;
  hubId?: string;
  entityId?: string;
  packageId?: string;
  schemaId?: string;
  relationContext?: string[];
};

/**
 * Parses and validates a stored resume context (e.g. from sessionStorage or a
 * server-side serialisation).
 *
 * Returns an empty ResumeContext when the stored value is absent, cannot be
 * parsed, or fails basic shape validation.  Individual optional fields that
 * fail type checks are silently dropped so that a partially-valid context can
 * still seed the pipeline.
 */
export function restoreResume(stored: unknown): ResumeContext {
  if (stored === null || stored === undefined) {
    return {};
  }

  let raw: Record<string, unknown>;

  if (typeof stored === "string") {
    try {
      const parsed = JSON.parse(stored);
      if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) {
        return {};
      }
      raw = parsed as Record<string, unknown>;
    } catch {
      return {};
    }
  } else if (typeof stored === "object" && !Array.isArray(stored)) {
    raw = stored as Record<string, unknown>;
  } else {
    return {};
  }

  const ctx: ResumeContext = {};

  if (typeof raw.attractorKey === "string") {
    ctx.attractorKey = raw.attractorKey;
  }
  if (typeof raw.hubId === "string") {
    ctx.hubId = raw.hubId;
  }
  if (typeof raw.entityId === "string") {
    ctx.entityId = raw.entityId;
  }
  if (typeof raw.packageId === "string") {
    ctx.packageId = raw.packageId;
  }
  if (typeof raw.schemaId === "string") {
    ctx.schemaId = raw.schemaId;
  }
  if (
    Array.isArray(raw.relationContext) &&
    raw.relationContext.every((v) => typeof v === "string")
  ) {
    ctx.relationContext = raw.relationContext as string[];
  }

  // Validate the nested operationVector shape minimally
  if (
    raw.operationVector !== null &&
    typeof raw.operationVector === "object" &&
    !Array.isArray(raw.operationVector)
  ) {
    const ov = raw.operationVector as Record<string, unknown>;
    if (
      typeof ov.target === "string" &&
      typeof ov.layer === "string" &&
      typeof ov.action === "string" &&
      typeof ov.attractorKey === "string"
    ) {
      ctx.operationVector = {
        target: ov.target,
        layer: ov.layer,
        action: ov.action,
        attractorKey: ov.attractorKey,
        userRole: typeof ov.userRole === "string" ? ov.userRole : undefined,
        payload:
          typeof ov.payload === "object" && ov.payload !== null && !Array.isArray(ov.payload)
            ? (ov.payload as Record<string, unknown>)
            : undefined,
        requestedProjection:
          typeof ov.requestedProjection === "string" ? ov.requestedProjection : undefined,
      };
    }
  }

  return ctx;
}
