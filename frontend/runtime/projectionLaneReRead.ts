import type { Emission } from "../api/dispatch.ts";
import type { ProjectionRuntime } from "./projectionRuntime.ts";

export type ProjectionLaneIdentity = {
  manifestId: string;
  tableId: string;
  tableRegistryId?: string;
};

export type ProjectionLaneReReadHandlerOptions = {
  projectionRuntime: ProjectionRuntime;
  readEmission: (identity: ProjectionLaneIdentity) => Promise<Emission>;
  onError?: (message: string) => void;
};

function parseIdentity(rawData: string): ProjectionLaneIdentity | null {
  try {
    const payload = JSON.parse(rawData) as Record<string, unknown>;
    const manifestId = typeof payload.manifest_id === "string" ? payload.manifest_id : null;
    const tableId = typeof payload.table_id === "string" ? payload.table_id : null;
    if (!manifestId || !tableId) return null;
    return {
      manifestId,
      tableId,
      tableRegistryId: typeof payload.table_registry_id === "string" ? payload.table_registry_id : undefined,
    };
  } catch {
    return null;
  }
}

function emissionManifestId(emission: Emission): string | undefined {
  const data = emission.data as Record<string, unknown> | undefined;
  return typeof data?.manifestId === "string" ? data.manifestId : undefined;
}

/**
 * SSE db_notify payload is an identity-only re-read trigger. This handler fixes
 * that contract at the frontend boundary: read the manifest-scoped emission,
 * load its projectionDefinition, then feed the same identity plus emission data
 * into projectionRuntime.
 */
export function createProjectionLaneReReadHandler(options: ProjectionLaneReReadHandlerOptions) {
  return async (rawSseData: string): Promise<void> => {
    const identity = parseIdentity(rawSseData);
    if (!identity) {
      options.onError?.(`PROJECTION_LANE_IDENTITY_INVALID raw=${rawSseData}`);
      return;
    }

    const emission = await options.readEmission(identity);
    if (!emission.projectionDefinition) {
      options.onError?.(`PROJECTION_LANE_PROJECTION_DEFINITION_MISSING manifest=${identity.manifestId} tableId=${identity.tableId}`);
      return;
    }

    const dataManifestId = emissionManifestId(emission);
    if (dataManifestId && dataManifestId !== identity.manifestId) {
      options.onError?.(`PROJECTION_LANE_MANIFEST_MISMATCH sseManifest=${identity.manifestId} emissionManifest=${dataManifestId} tableId=${identity.tableId}`);
      return;
    }

    options.projectionRuntime.setProjectionDefinition(emission.projectionDefinition);
    options.projectionRuntime.handleProjectionEvent(JSON.stringify({
      manifest_id: identity.manifestId,
      table_id: identity.tableId,
      table_registry_id: identity.tableRegistryId,
      data: emission.data ?? {},
    }));
  };
}
