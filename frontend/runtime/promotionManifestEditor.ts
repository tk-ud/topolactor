export type PromotionActivationPolicyType = "manual" | "scheduled" | "conditional";

export type PromotionTargetRefDraft = {
  packageId: string;
  schemaId: string;
  componentId: string;
};

export type PromotionManifestMetadata = {
  manifestKey: string;
  versionLabel: string;
  disclosureText: string;
  disclosureCategoryLabel: string | null;
  placementKey: string;
  projectionSurfaceType: string;
  activationPolicyType: string;
  activationConditionExpression: string | null;
  targetTopologyRefs: PromotionTargetRefDraft[];
};

export type PromotionManifestDraft = {
  manifestKey: string;
  versionLabel: string;
  disclosureText: string;
  disclosureCategoryLabel: string;
  placementKey: string;
  projectionSurfaceType: string;
  activationPolicyType: PromotionActivationPolicyType;
  activationConditionExpression: string;
  targetRefs: PromotionTargetRefDraft[];
};

export const PROMOTION_ACTIVATION_POLICY_OPTIONS: PromotionActivationPolicyType[] = [
  "manual",
  "scheduled",
  "conditional",
];

export const DEFAULT_ADMIN_PACKAGE_ID = "00000000-0000-0000-0000-000000000020";
export const DEFAULT_ADMIN_SCHEMA_ID = "00000000-0000-0000-0000-000000000021";
export const DEFAULT_ADMIN_COMPONENT_ID = "00000000-0000-0000-0000-000000000022";
export const DEFAULT_ADMIN_PLACEMENT_KEY = "admin:context_token_registry:list";

export function emptyPromotionManifestDraft(): PromotionManifestDraft {
  return {
    manifestKey: "",
    versionLabel: "",
    disclosureText: "",
    disclosureCategoryLabel: "",
    placementKey: DEFAULT_ADMIN_PLACEMENT_KEY,
    projectionSurfaceType: "banner_surface",
    activationPolicyType: "manual",
    activationConditionExpression: "",
    targetRefs: [{
      packageId: DEFAULT_ADMIN_PACKAGE_ID,
      schemaId: DEFAULT_ADMIN_SCHEMA_ID,
      componentId: DEFAULT_ADMIN_COMPONENT_ID,
    }],
  };
}

export function parsePromotionMetadataToDraft(
  metadata: PromotionManifestMetadata | null | undefined,
): PromotionManifestDraft {
  if (!metadata) return emptyPromotionManifestDraft();

  const policy = metadata.activationPolicyType;
  const draft = emptyPromotionManifestDraft();
  draft.manifestKey = metadata.manifestKey ?? "";
  draft.versionLabel = metadata.versionLabel ?? "";
  draft.disclosureText = metadata.disclosureText ?? "";
  draft.disclosureCategoryLabel = metadata.disclosureCategoryLabel ?? "";
  draft.placementKey = metadata.placementKey ?? DEFAULT_ADMIN_PLACEMENT_KEY;
  draft.projectionSurfaceType = metadata.projectionSurfaceType ?? "banner_surface";
  draft.activationPolicyType = PROMOTION_ACTIVATION_POLICY_OPTIONS.includes(
    policy as PromotionActivationPolicyType,
  )
    ? (policy as PromotionActivationPolicyType)
    : "manual";
  draft.activationConditionExpression = metadata.activationConditionExpression ?? "";
  draft.targetRefs = metadata.targetTopologyRefs?.length
    ? metadata.targetTopologyRefs.map((r) => ({
      packageId: r.packageId,
      schemaId: r.schemaId,
      componentId: r.componentId,
    }))
    : emptyPromotionManifestDraft().targetRefs;
  return draft;
}

export function buildPromotionUpdatePayload(
  manifestId: string,
  draft: PromotionManifestDraft,
): Record<string, unknown> {
  const refs = draft.targetRefs
    .map((r) => ({
      packageId: r.packageId.trim(),
      schemaId: r.schemaId.trim(),
      componentId: r.componentId.trim(),
    }))
    .filter((r) => r.packageId && r.schemaId && r.componentId);

  return {
    manifestId,
    manifestKey: draft.manifestKey.trim(),
    versionLabel: draft.versionLabel.trim(),
    disclosureText: draft.disclosureText.trim(),
    disclosureCategoryLabel: draft.disclosureCategoryLabel.trim() || null,
    placementKey: draft.placementKey.trim(),
    projectionSurfaceType: draft.projectionSurfaceType.trim(),
    activationPolicyType: draft.activationPolicyType,
    activationConditionExpression: draft.activationConditionExpression.trim() || null,
    targetTopologyRefs: refs,
  };
}

export function formatPromotionSummary(metadata: PromotionManifestMetadata | null | undefined): string {
  if (!metadata) return "none";
  return `${metadata.manifestKey}@${metadata.versionLabel} — ${metadata.placementKey} (${metadata.targetTopologyRefs.length} ref(s))`;
}
