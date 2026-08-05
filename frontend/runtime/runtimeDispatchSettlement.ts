/**
 * Round-3 preview-gap audit: single settlement-acceptance authority for a settled admin_runtime
 * dispatch result, shared by every consumer of that result (ProjectionShell.tsx's error display /
 * canonical-reread trigger AND runtimeComponentFactory.ts's deferred local-state mutation gate —
 * see applyDeferredLocalStateMutationOnSuccess). Before this module existed those two consumers
 * computed acceptance independently: ProjectionShell checked result.success + Emission presence +
 * authored target_ref identity match, while the Modal open/close gate checked ONLY result.success —
 * so a success response carrying a manifestId that did not match what was actually dispatched (or
 * carrying no Emission at all) still opened/closed a preview/confirm Modal, even though
 * ProjectionShell's own handler would have logged it as an anomaly. This function is the ONE place
 * that decides "did this settled dispatch actually land the way it claims to have" — every caller
 * gates on its verdict instead of re-deriving one.
 *
 * docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
 * round_27_28_settled_child_dispatch_result_authority names the same three failure shapes for its
 * OWN canonical-reread redispatch ("backend success:false, a missing Emission, a queue/network
 * rejection, or the redispatch's own response failing confirmProjectionEntryEmission"); this module
 * applies the identical fail-close vocabulary to the ORIGINAL admin_runtime write/preview dispatch's
 * own settled result, not the canonical reread's own redispatch (that stays in
 * ProjectionShell.tsx's refreshCurrentManifestAsync, unchanged).
 */

import type { DispatchResponse, Emission } from "../api/dispatch.ts";
import type { RuntimeDispatchResultContext } from "./runtimeComponentAdapter.ts";

/**
 * Extracts the manifest id an authored target_ref actually pointed at
 * ("manifest:<uuid>:<wiringKey>" — see projectionEntry.ts's own target_ref construction and
 * RuntimeDispatchSpec.targetRef's doc comment for the shape). Generic string parsing, never a
 * lookup table keyed by operation/nodeId/UUID.
 */
export function extractManifestIdFromTargetRef(
  targetRef: string | undefined,
): string | undefined {
  if (!targetRef) return undefined;
  const match = /^manifest:([^:]+):/.exec(targetRef);
  return match ? match[1] : undefined;
}

export type RuntimeDispatchSettlement =
  | {
    accepted: true;
    emission: Emission;
    /** The manifest id context.targetRef actually resolved to, when an override was authored. */
    expectedManifestId?: string;
  }
  | {
    accepted: false;
    kind: "backend_failure" | "missing_emission" | "identity_mismatch";
    reason: string;
  };

const DEFAULT_FAILURE_MESSAGE = "操作を完了できませんでした。";
const MISSING_EMISSION_MESSAGE = "投影データを取得できませんでした（応答に投影データがありません）。";

/**
 * Resolves whether a settled admin_runtime dispatch result is acceptable, and if so, its Emission
 * and the manifest id its own authored target_ref (context.targetRef) resolved to. Never inspects
 * operation name, nodeId, or manifest UUID — only result.success, result.emission presence, and
 * (when context.targetRef was authored) whether result.emission.manifestId matches it.
 */
export function resolveRuntimeDispatchSettlement(
  result: DispatchResponse,
  context: RuntimeDispatchResultContext,
): RuntimeDispatchSettlement {
  if (!result.success) {
    return {
      accepted: false,
      kind: "backend_failure",
      reason: result.errors?.[0]?.message ?? DEFAULT_FAILURE_MESSAGE,
    };
  }
  if (!result.emission) {
    return { accepted: false, kind: "missing_emission", reason: MISSING_EMISSION_MESSAGE };
  }
  const expectedManifestId = extractManifestIdFromTargetRef(context.targetRef);
  if (expectedManifestId && result.emission.manifestId !== expectedManifestId) {
    return {
      accepted: false,
      kind: "identity_mismatch",
      reason:
        `書き込み結果の投影先が想定と一致しませんでした（想定: ${expectedManifestId}、` +
        `実際: ${result.emission.manifestId ?? "(absent)"}）。`,
    };
  }
  return { accepted: true, emission: result.emission, expectedManifestId };
}
