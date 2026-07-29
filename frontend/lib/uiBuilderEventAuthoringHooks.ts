import { useEffect, useState } from "preact/hooks";
import { dispatchAdminOp } from "../api/adminApi.ts";
import { parsePayloadFromSource } from "../runtime/payloadFromResolver.ts";

export type ExternalPortAuthoringCandidate = {
  portId: string;
  portKind: string;
  providerKind: string;
  credentialKind: string;
  referenceKey?: string | null;
  requiredByBundle: string;
  consumerBundleBinding?: string | null;
  urlOrEnvReference?: string | null;
  hookPath?: string | null;
  routeKey?: string | null;
  targetRef: string;
};

export type InstanceOperationAuthoringCandidate = {
  portKind: string;
  instancePortId: string;
  operationBindingKey: string;
  operationKey: string;
  approvalStatus: string;
  targetRef: string;
};

export type AdminRuntimeTargetRefAuthoringCandidate = {
  manifestId: string;
  manifestKey?: string | null;
  layer: string;
  action: string;
  targetRef: string;
};

/** SSOT: external-port-substrate-ssot.yaml admin_setting_projection */
export function useExternalPortAuthoringCandidates(active: boolean): {
  candidates: ExternalPortAuthoringCandidate[];
  error: string | null;
} {
  const [candidates, setCandidates] = useState<ExternalPortAuthoringCandidate[]>([]);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    if (!active) {
      setCandidates([]);
      setError(null);
      return;
    }
    let cancelled = false;
    (async () => {
      const body = await dispatchAdminOp("ui_topology", "list_external_port_authoring_candidates");
      if (cancelled) return;
      const data = body?.emission?.data as { candidates?: ExternalPortAuthoringCandidate[] } | undefined;
      if (Array.isArray(data?.candidates)) {
        setCandidates(data.candidates);
        setError(null);
      } else {
        setCandidates([]);
        setError(body?.errors?.[0]?.message ?? "external port 候補を取得できませんでした。");
      }
    })();
    return () => { cancelled = true; };
  }, [active]);
  return { candidates, error };
}

/** SSOT: instance-port-substrate-ssot.yaml admin_event_authoring_boundary */
export function useInstanceOperationAuthoringCandidates(active: boolean): {
  candidates: InstanceOperationAuthoringCandidate[];
  error: string | null;
} {
  const [candidates, setCandidates] = useState<InstanceOperationAuthoringCandidate[]>([]);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    if (!active) {
      setCandidates([]);
      setError(null);
      return;
    }
    let cancelled = false;
    (async () => {
      const body = await dispatchAdminOp("ui_topology", "list_instance_operation_authoring_candidates");
      if (cancelled) return;
      const data = body?.emission?.data as { candidates?: InstanceOperationAuthoringCandidate[] } | undefined;
      if (Array.isArray(data?.candidates)) {
        setCandidates(data.candidates);
        setError(null);
      } else {
        setCandidates([]);
        setError(body?.errors?.[0]?.message ?? "instance operation 候補を取得できませんでした。");
      }
    })();
    return () => { cancelled = true; };
  }, [active]);
  return { candidates, error };
}

/**
 * Round 18: the CURRENT layout's package-wiring wiringKind, re-fetched by packageId (the same
 * ui_topology:get_package_wiring action PackageWiringEditor itself uses, so both surfaces read the
 * SAME persisted authority — never a second, drifting source of truth). Used to gate the
 * admin_runtime dispatch-override authoring section: it must only be shown/editable when the
 * layout actually carries wiring_kind="admin_runtime", never as a frontend-only cosmetic guess
 * (the backend save-time fail-close from round 16 already enforces this independently — this hook
 * only prevents the UI from inviting an author to build an invalid draft in the first place).
 * SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml admin_runtime_target_ref_override_contract
 * applicability.
 */
export function useEffectivePackageWiringKind(packageId: string | undefined): {
  wiringKind: string | null;
  loading: boolean;
} {
  const [wiringKind, setWiringKind] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  useEffect(() => {
    if (!packageId) {
      setWiringKind(null);
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    (async () => {
      const body = await dispatchAdminOp("ui_topology", "get_package_wiring", { packageId });
      if (cancelled) return;
      const data = body?.emission?.data as { wiringKind?: string } | undefined;
      setWiringKind(typeof data?.wiringKind === "string" ? data.wiringKind : null);
      setLoading(false);
    })();
    return () => { cancelled = true; };
  }, [packageId]);
  return { wiringKind, loading };
}

/**
 * Closed candidate list for dispatchTargetRefByTrigger authoring — every (active admin_runtime
 * manifest, dispatcher_mapping layer/action) pair, derived entirely from manifest/dispatcher
 * authority already in the DB (backend/repository/NpgsqlUiTopologyRepository.cs
 * ListAdminRuntimeTargetRefAuthoringCandidatesAsync). Never a frontend-hardcoded per-surface
 * (a single surface's own action list) — reusable by any admin_runtime action across any subBundle.
 * SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml admin_runtime_target_ref_override_contract
 */
export function useAdminRuntimeTargetRefAuthoringCandidates(active: boolean): {
  candidates: AdminRuntimeTargetRefAuthoringCandidate[];
  error: string | null;
} {
  const [candidates, setCandidates] = useState<AdminRuntimeTargetRefAuthoringCandidate[]>([]);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    if (!active) {
      setCandidates([]);
      setError(null);
      return;
    }
    let cancelled = false;
    (async () => {
      const body = await dispatchAdminOp("ui_topology", "list_admin_runtime_target_ref_authoring_candidates");
      if (cancelled) return;
      const data = body?.emission?.data as { candidates?: AdminRuntimeTargetRefAuthoringCandidate[] } | undefined;
      if (Array.isArray(data?.candidates)) {
        setCandidates(data.candidates);
        setError(null);
      } else {
        setCandidates([]);
        setError(body?.errors?.[0]?.message ?? "admin_runtime 操作候補を取得できませんでした。");
      }
    })();
    return () => { cancelled = true; };
  }, [active]);
  return { candidates, error };
}

/** SSOT: ui-builder-preset-ecosystem-ssot.yaml payloadFrom_resolver_contract */
export function validatePayloadFromEntries(
  entries: Array<{ field: string; source: string }>,
): string[] {
  const errors: string[] = [];
  for (const { field, source } of entries) {
    if (!field.trim()) continue;
    const parsed = parsePayloadFromSource(source.trim());
    if (parsed.kind === "unresolved_ref") {
      errors.push(
        `"${field}": PAYLOAD_FROM_UNRESOLVED_REF — "${source}" は認識されたパターンではありません (node:<nodeId>.value | event.<path> | literal:<value>)`,
      );
    }
  }
  return errors;
}
