import type { Emission } from "../api/dispatch.ts";
import { validationErrorText } from "../api/dispatch.ts";

export type EmissionSummary = {
  ok: boolean;
  structureMapId?: string;
  packageId?: string;
  schemaId?: string;
  componentCount: number;
  recommendationStatus?: string;
  recommendationDetail?: string;
  errorMessages: string[];
};

export function summarizeEmission(emission: Emission): EmissionSummary {
  const errorMessages = (emission.errors ?? []).map(validationErrorText);
  const rec = emission.contextRouteRecommendation;

  return {
    ok: errorMessages.length === 0,
    structureMapId: emission.structureMapId,
    packageId: emission.packageId,
    schemaId: emission.schemaId,
    componentCount: emission.componentIds?.length ?? 0,
    recommendationStatus: rec?.status,
    recommendationDetail: rec?.statusDetail,
    errorMessages,
  };
}
