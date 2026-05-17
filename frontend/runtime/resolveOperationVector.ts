export type OperationType = "Search" | "Create" | "diffUpdate" | "logicalDelete";

export type UserOperation = {
  operationType: OperationType;
  target: string;
  layer: string;
  action: string;
  currentHub?: string;
  currentEntity?: string;
  relationContext?: string[];
  userRole?: string;
  payload?: Record<string, unknown>;
  requestedProjection?: string;
};

export type OperationVector = {
  target: string;
  layer: string;
  action: string;
  attractorKey: string;
  userRole?: string;
  payload?: Record<string, unknown>;
  requestedProjection?: string;
};

/**
 * Converts a UserOperation into an OperationVector.
 *
 * The attractorKey is the canonical address used to resolve the structure_map
 * entry for this operation: `${target}:${layer}:${action}`.
 *
 * Context fields (currentHub, currentEntity, relationContext) are resolved at
 * the UserOperation level and do not propagate into the vector; they are
 * consumed here as needed before the vector enters the attractor pipeline.
 */
export function resolveOperationVector(op: UserOperation): OperationVector {
  const attractorKey = `${op.target}:${op.layer}:${op.action}`;

  return {
    target: op.target,
    layer: op.layer,
    action: op.action,
    attractorKey,
    userRole: op.userRole,
    payload: op.payload,
    requestedProjection: op.requestedProjection,
  };
}
