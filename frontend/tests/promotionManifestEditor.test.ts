import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import {
  buildPromotionUpdatePayload,
  emptyPromotionManifestDraft,
  parsePromotionMetadataToDraft,
} from "../runtime/promotionManifestEditor.ts";

Deno.test("parsePromotionMetadataToDraft maps backend metadata", () => {
  const draft = parsePromotionMetadataToDraft({
    manifestKey: "campaign-a",
    versionLabel: "v1",
    disclosureText: "Disclosure",
    disclosureCategoryLabel: "info",
    placementKey: "admin:context_token_registry:list",
    projectionSurfaceType: "banner_surface",
    activationPolicyType: "manual",
    activationConditionExpression: null,
    targetTopologyRefs: [{
      packageId: "00000000-0000-0000-0000-000000000020",
      schemaId: "00000000-0000-0000-0000-000000000021",
      componentId: "00000000-0000-0000-0000-000000000022",
    }],
  });

  assertEquals(draft.manifestKey, "campaign-a");
  assertEquals(draft.targetRefs.length, 1);
});

Deno.test("buildPromotionUpdatePayload trims and omits empty category", () => {
  const draft = emptyPromotionManifestDraft();
  draft.manifestKey = "  key  ";
  draft.versionLabel = "v1";
  draft.disclosureText = "text";
  draft.disclosureCategoryLabel = "   ";

  const payload = buildPromotionUpdatePayload("00000000-0000-0000-0000-000000000099", draft);
  assertEquals(payload.manifestKey, "key");
  assertEquals(payload.disclosureCategoryLabel, null);
});
