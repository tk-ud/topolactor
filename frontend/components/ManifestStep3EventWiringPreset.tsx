import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import { dispatchAdminOp } from "../api/adminApi.ts";
import {
  UX_MANIFEST_API_EVENT_NONE,
  UX_MANIFEST_API_EVENT_PRESET_LABEL,
  UX_MANIFEST_API_EVENT_SAVE_LABEL,
  UX_MANIFEST_API_EVENT_TOPOLOGY_CONTEXT,
  UX_MANIFEST_API_EVENT_NOT_CONTENTS_SCREEN,
  UX_MANIFEST_API_EVENT_NO_STEP3_WIRING,
  UX_MANIFEST_API_EVENT_TOPOLOGY_MISSING,
  UX_MANIFEST_API_EVENT_WIRING_SELECT,
  UX_TOPOLOGY_API_SECTION_HINT,
} from "../content/adminUxTerms.ts";
import { extractScreenDataShapeFromTopology } from "../lib/manifestTopologyExtensions.ts";
import {
  encodeManifestPackageTargetRef,
  manifestIdFromTargetRef,
  manifestWiringKeyFromTargetRef,
} from "../lib/packageWiringPicker.ts";
import {
  type ManifestRouteOption,
  resolveManifestRouteOptionForRouteKey,
} from "../lib/manifestRouteResolution.ts";
import {
  buildScreenReadQueryWiringCandidates,
  formatScreenReadQueryWiringCandidateLabel,
  type ScreenReadQueryWiringCandidate,
} from "../lib/screenReadQueryWiring.ts";

type PackageWiringRow = {
  wiringId: string;
  wiringKind: string;
  targetSurface: string;
  targetRef?: string | null;
};

/**
 * Normal-view preset: wire package dispatch to Contents Step 3 screenReadQueryWiring.
 * Scoped to the committed UI Builder topology (routeKey) only — not global manifest list.
 * SSOT: admin-console-workflow-ssot.yaml manifest_screenReadQueryWiring / contents_dispatch_ui_wiring_configurable
 */
export default function ManifestStep3EventWiringPreset({
  selectedPackageId,
  routeKey,
}: {
  selectedPackageId: string;
  /** Committed topology routeKey (= topology.name). */
  routeKey: string;
}): JSX.Element {
  const [wiringId, setWiringId] = useState<string | null>(null);
  const [savedTargetRef, setSavedTargetRef] = useState<string | null>(null);
  const [topologyContext, setTopologyContext] = useState<ManifestRouteOption | null>(null);
  const [topologyResolveError, setTopologyResolveError] = useState<string | null>(null);
  const [selectedWiringKey, setSelectedWiringKey] = useState("");
  const [candidates, setCandidates] = useState<ScreenReadQueryWiringCandidate[]>([]);
  const [loadStatus, setLoadStatus] = useState<string | null>(null);
  const [candidateError, setCandidateError] = useState<string | null>(null);
  const [emptyCandidatesHint, setEmptyCandidatesHint] = useState<string | null>(null);
  const [saveStatus, setSaveStatus] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const manifestId = topologyContext?.manifestId ?? "";

  useEffect(() => {
    if (!selectedPackageId) {
      setWiringId(null);
      setSavedTargetRef(null);
      setSelectedWiringKey("");
      setLoadStatus(null);
      return;
    }
    (async () => {
      setLoadStatus("配線を読み込み中…");
      const body = await dispatchAdminOp("ui_topology", "get_package_wiring", {
        packageId: selectedPackageId,
      });
      const data = body?.emission?.data as PackageWiringRow | undefined;
      if (data?.wiringId) {
        setWiringId(data.wiringId);
        if (data.targetSurface === "manifest" && data.targetRef) {
          setSavedTargetRef(data.targetRef);
          setSelectedWiringKey(manifestWiringKeyFromTargetRef(data.targetRef, "manifest"));
        } else {
          setSavedTargetRef(null);
          setSelectedWiringKey("");
        }
        setLoadStatus(null);
      } else {
        setWiringId(null);
        setLoadStatus(
          body?.errors?.[0]?.message ??
            "パッケージ配線がありません。先にルートを確定してください。",
        );
      }
    })();
  }, [selectedPackageId]);

  useEffect(() => {
    const rk = routeKey.trim();
    if (!rk) {
      setTopologyContext(null);
      setTopologyResolveError(UX_MANIFEST_API_EVENT_TOPOLOGY_MISSING);
      return;
    }
    let cancelled = false;
    (async () => {
      setTopologyResolveError(null);
      const match = await resolveManifestRouteOptionForRouteKey(rk);
      if (cancelled) return;
      if (!match) {
        setTopologyContext(null);
        setTopologyResolveError(UX_MANIFEST_API_EVENT_TOPOLOGY_MISSING);
        return;
      }
      setTopologyContext(match);
    })();
    return () => { cancelled = true; };
  }, [routeKey]);

  useEffect(() => {
    if (!manifestId.trim()) {
      setCandidates([]);
      setCandidateError(null);
      setEmptyCandidatesHint(null);
      return;
    }
    (async () => {
      setCandidateError(null);
      setEmptyCandidatesHint(null);
      const body = await dispatchAdminOp(
        "manifest",
        "list_screen_read_query_wiring",
        { manifestId: manifestId.trim() },
      );
      const data = body?.emission?.data as {
        candidates?: ScreenReadQueryWiringCandidate[];
      } | undefined;
      if (Array.isArray(data?.candidates) && data.candidates.length > 0) {
        setCandidates(data.candidates);
        return;
      }
      const getBody = await dispatchAdminOp("manifest", "get", {
        manifestId: manifestId.trim(),
      });
      const detail = getBody?.emission?.data as { topologyRawJson?: string } | undefined;
      if (typeof detail?.topologyRawJson === "string") {
        const derived = buildScreenReadQueryWiringCandidates(detail.topologyRawJson);
        if (derived.length > 0) {
          setCandidates(derived);
          return;
        }
        let contentsType: string | null = null;
        try {
          const entries = JSON.parse(detail.topologyRawJson) as unknown;
          if (Array.isArray(entries)) {
            for (const entry of entries) {
              if (typeof entry === "object" && entry !== null &&
                (entry as { type?: string }).type === "screen_data_shape" &&
                typeof (entry as { contentsType?: string }).contentsType === "string") {
                contentsType = (entry as { contentsType: string }).contentsType;
                break;
              }
            }
          }
        } catch { /* ignore */ }
        const shape = extractScreenDataShapeFromTopology(detail.topologyRawJson);
        setCandidates([]);
        if (contentsType && contentsType !== "contents") {
          setEmptyCandidatesHint(UX_MANIFEST_API_EVENT_NOT_CONTENTS_SCREEN);
        } else if (!shape.screenOperationKinds?.length && !shape.screenOperationKind) {
          setEmptyCandidatesHint(UX_MANIFEST_API_EVENT_NO_STEP3_WIRING);
        } else {
          setEmptyCandidatesHint(UX_MANIFEST_API_EVENT_NO_STEP3_WIRING);
        }
        return;
      }
      setCandidates([]);
      setCandidateError(
        getBody?.errors?.[0]?.message ??
          body?.errors?.[0]?.message ??
          "Step 3 の配線候補を取得できませんでした。",
      );
    })();
  }, [manifestId]);

  const resolvedTargetRef = manifestId && selectedWiringKey
    ? encodeManifestPackageTargetRef(manifestId, selectedWiringKey)
    : null;
  const isDirty = resolvedTargetRef !== (savedTargetRef ?? null);

  const handleClear = () => {
    setSelectedWiringKey("");
    setSaveStatus(null);
  };

  const handleSave = async () => {
    if (!wiringId) {
      setSaveStatus("配線が未登録です。先にルートを確定してください。");
      return;
    }
    if (!manifestId.trim()) {
      setSaveStatus(UX_MANIFEST_API_EVENT_TOPOLOGY_MISSING);
      return;
    }
    if (candidateError) {
      setSaveStatus(`配線候補の取得に失敗しています: ${candidateError}`);
      return;
    }
    if (candidates.length === 0) {
      setSaveStatus(
        "Step 3 の API イベント候補がありません。Contents Step 3 で操作・検索・集計を設定してください。",
      );
      return;
    }
    if (!selectedWiringKey.trim()) {
      setSaveStatus("Step 3 で設定した API イベントを選択してください。");
      return;
    }
    setSaving(true);
    setSaveStatus(null);
    try {
      const body = await dispatchAdminOp("ui_topology", "update_package_wiring", {
        packageId: selectedPackageId,
        wiringId,
        wiringKind: selectedWiringKey,
        targetSurface: "manifest",
        targetRef: resolvedTargetRef,
      });
      const refreshed = body?.emission?.data?.wiring as PackageWiringRow | undefined;
      if (body?.success && refreshed?.targetRef) {
        setSavedTargetRef(refreshed.targetRef);
        setSaveStatus("Step 3 API イベントの配線を保存しました。");
      } else {
        setSaveStatus(body?.errors?.[0]?.message ?? "保存に失敗しました。");
      }
    } finally {
      setSaving(false);
    }
  };

  const savedWiringKey = savedTargetRef
    ? manifestWiringKeyFromTargetRef(savedTargetRef, "manifest")
    : "";
  const savedWiringLabel = savedWiringKey
    ? candidates.find((c) => c.wiringKey === savedWiringKey)
    : null;
  const savedManifestMismatch = savedTargetRef && topologyContext
    ? manifestIdFromTargetRef(savedTargetRef, "manifest") !== topologyContext.manifestId
    : false;

  return (
    <section class="rounded border border-indigo-100 bg-indigo-50/40 p-3 text-xs">
      <h4 class="mb-1 font-semibold text-indigo-900">
        {UX_MANIFEST_API_EVENT_PRESET_LABEL}
      </h4>
      <p class="mb-2 text-[0.65rem] text-indigo-800">
        {UX_TOPOLOGY_API_SECTION_HINT}
      </p>
      {loadStatus
        ? <p class="text-slate-600">{loadStatus}</p>
        : (
          <>
            <div class="rounded border border-indigo-200 bg-white px-2 py-1.5">
              <p class="text-[0.65rem] font-medium text-indigo-900">
                {UX_MANIFEST_API_EVENT_TOPOLOGY_CONTEXT}
              </p>
              {topologyContext
                ? (
                  <p class="mt-0.5 text-indigo-950">
                    <strong>{topologyContext.label}</strong>
                    <span class="ml-1 font-mono text-[0.6rem] text-slate-500">
                      ({topologyContext.topologySystemName})
                    </span>
                    <span class="ml-1 text-[0.6rem] text-slate-500">
                      [{topologyContext.status}]
                    </span>
                  </p>
                )
                : (
                  <p class="mt-0.5 text-amber-800">
                    {topologyResolveError ?? UX_MANIFEST_API_EVENT_TOPOLOGY_MISSING}
                  </p>
                )}
            </div>

            {topologyContext && (
              <fieldset class="mt-3">
                <legend class="mb-1 font-medium text-indigo-900">
                  {UX_MANIFEST_API_EVENT_WIRING_SELECT}
                </legend>
                {candidateError && (
                  <p class="rounded border border-red-300 bg-red-50 px-2 py-1.5 text-red-700">
                    {candidateError}
                  </p>
                )}
                {!candidateError && candidates.length === 0 && (
                  <p class="rounded border border-amber-300 bg-amber-50 px-2 py-1.5 text-amber-800">
                    {emptyCandidatesHint ?? UX_MANIFEST_API_EVENT_NO_STEP3_WIRING}
                  </p>
                )}
                <ul class="space-y-1">
                  <li>
                    <label class="flex cursor-pointer items-start gap-2 rounded border border-slate-200 bg-white px-2 py-1.5 hover:bg-slate-50">
                      <input
                        type="radio"
                        name="step3-api-event"
                        checked={!selectedWiringKey}
                        onChange={handleClear}
                      />
                      <span class="text-slate-700">{UX_MANIFEST_API_EVENT_NONE}</span>
                    </label>
                  </li>
                  {candidates.map((c) => (
                    <li key={c.wiringKey}>
                      <label class="flex cursor-pointer items-start gap-2 rounded border border-slate-200 bg-white px-2 py-1.5 hover:bg-slate-50">
                        <input
                          type="radio"
                          name="step3-api-event"
                          checked={selectedWiringKey === c.wiringKey}
                          onChange={() => setSelectedWiringKey(c.wiringKey)}
                        />
                        <span>
                          <span class="font-semibold text-indigo-900">
                            {formatScreenReadQueryWiringCandidateLabel(c)}
                          </span>
                          <span class="mt-0.5 block font-mono text-[0.55rem] text-slate-400">
                            {c.wiringKey}
                          </span>
                        </span>
                      </label>
                    </li>
                  ))}
                </ul>
              </fieldset>
            )}

            {savedTargetRef && savedWiringKey && (
              <p class="mt-2 text-[0.7rem] text-indigo-800">
                保存済み:{" "}
                {savedWiringLabel
                  ? formatScreenReadQueryWiringCandidateLabel(savedWiringLabel)
                  : savedWiringKey}
                {savedManifestMismatch && (
                  <span class="ml-1 text-amber-700">
                    （別トポロジーへの配線 — 再選択して保存してください）
                  </span>
                )}
              </p>
            )}

            <button
              type="button"
              class={`btn-primary mt-2 text-xs ${
                (!isDirty || saving || !wiringId || !topologyContext) ? "opacity-50 cursor-not-allowed" : ""
              }`}
              disabled={!isDirty || saving || !wiringId || !topologyContext}
              onClick={handleSave}
            >
              {saving ? "保存中…" : UX_MANIFEST_API_EVENT_SAVE_LABEL}
            </button>
          </>
        )}
      {saveStatus && (
        <p class="mt-2 text-xs font-semibold text-indigo-900">{saveStatus}</p>
      )}
    </section>
  );
}
