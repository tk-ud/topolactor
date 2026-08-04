import { useEffect, useRef, useState } from "preact/hooks";
import type { JSX } from "preact";
import { probeSessionToken, refreshProjectionSession } from "../api/authApi.ts";
import {
  clearSessionToken,
  persistSessionToken,
  readClientSessionToken,
} from "../lib/demoSession.ts";
import {
  enqueueProjectionHookTrigger,
  queueClientCommand,
  startComponentEventRuntime,
} from "../runtime/frontendScheduler.ts";
import type { UserOperation } from "../runtime/resolveOperationVector.ts";
import {
  type ComponentSpec,
  renderEmission,
} from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import {
  createSseReceiver,
  type ProjectionHookTrigger,
  type SseReceiver,
} from "../runtime/sseReceiver.ts";
import { createSseDispatcherWithProjectionRuntime } from "../runtime/sseDispatcher.ts";
import { createProjectionRuntime } from "../runtime/projectionRuntime.ts";
import {
  createProjectionStateDispatcher,
  createRuntimeLocalStateStore,
  createUiEventEffectRunner,
  type NotifyingRuntimeLocalStateStore,
  predeclareProjectionState,
  type RuntimeStateDispatcher,
  type UiEventEffectRunner,
} from "../runtime/uiEventEffectRunner.ts";
import {
  createLiveNodeValueTracker,
  type LiveNodeValueTracker,
  seedTrackerFromPropBindingsValue,
} from "../runtime/liveNodeValueTracker.ts";
import { resolveRuntimeDataPath } from "../runtime/propBindingResolver.ts";
import type { WiringNode } from "../lib/uiBuilderWiringProjection.ts";
import {
  adoptResolvedManifestIdentity,
  confirmProjectionEntryEmission,
  parseProjectionEntrySelection,
  resolveHubNavigationLinks,
  resolveProjectionEntryAxes,
} from "../runtime/projectionEntry.ts";
import type { DispatchResponse, Emission, LayoutNode } from "../api/dispatch.ts";
import { RecommendNavigationIsland } from "../components/RecommendNavigationIsland.tsx";
import { LayoutProjectionTree } from "../components/LayoutProjectionTree.tsx";

/** Narrow an emission's layoutNodes into the WiringNode shape the runtime state/effect runner consumes. */
function toRunnerWiringNodes(
  layoutNodes: readonly LayoutNode[] | undefined,
): WiringNode[] {
  return (layoutNodes ?? [])
    .filter((n): n is LayoutNode & { nodeId: string } =>
      typeof n.nodeId === "string" && n.nodeId.length > 0
    )
    .map((n) => ({
      nodeId: n.nodeId,
      componentKey: n.componentKey,
      componentKind: n.componentKind,
      stateJson: n.stateJson ?? undefined,
      runtimeInteractions: n.runtimeInteractions ?? undefined,
    }));
}

/**
 * Production application projection shell.
 * Route/package/manifest-aware projection entry: the initial dispatch axes are
 * resolved from the entry URL selection (?route= / ?manifest= / ?package= via
 * projectionEntry.ts) so any UI Builder applied topology is selectable through
 * this production surface; without a selection, the default entry axes are
 * dispatched on mount. The emission is rendered with layout-aware
 * renderEmission(). Subscribes to SSE projection events
 * via the full sse_projection_lane per runtime-orchestration-ssot.yaml:
 *   sseReceiver → enqueueProjectionHookTrigger → sseDispatcher → projectionRuntime → onProjectionUpdate
 *
 * Identity preservation: all three identity fields from the SSE payload
 * (manifest_id, table_id, table_registry_id) are forwarded by projectionRuntime
 * to the onProjectionUpdate handler, which re-dispatches via queueClientCommand
 * with identity-preserving axes. No field is silently discarded.
 */
export default function ProjectionShell(): JSX.Element {
  const [loading, setLoading] = useState(true);
  const [emission, setEmission] = useState<Emission | null>(null);
  const [specs, setSpecs] = useState<ComponentSpec[]>([]);
  const [error, setError] = useState<string | null>(null);
  // Round 28 (pipeline-continuity-ssot.yaml explicit_error_log_retain_old_dom): distinct from
  // `error` (which replaces the WHOLE screen — reserved for initial-load failure). A failed
  // canonical reread or passive refresh must retain the existing DOM rather than blank the
  // screen; this banner-only state surfaces that failure without discarding what's rendered.
  // Cleared on the next successful refresh.
  const [refreshWarning, setRefreshWarning] = useState<string | null>(null);
  const [authFallback, setAuthFallback] = useState(false);
  const [projectionToken, setProjectionToken] = useState<string | undefined>(
    undefined,
  );

  // Generation counter prevents stale SSE refresh responses from overwriting newer results.
  const refreshGenRef = useRef(0);
  // Holds the SSE receiver so unmount cleanup can disconnect it.
  const sseReceiverRef = useRef<SseReceiver | null>(null);
  // Stores the initial dispatch axes for SSE identity-preserving refresh.
  // SSE triggers must re-dispatch using the same axes as the initial load, not hardcoded defaults.
  // Updated (via adoptResolvedManifestIdentity, right after the initial dispatch resolves) to pin
  // payload.target_ref to the backend-resolved Emission.ManifestId when the entry had no explicit
  // manifest selection — so refresh re-dispatches the SAME manifest, not just the same
  // pre-resolution axes (which only coincidentally re-resolves the same manifest).
  const initialDispatchAxesRef = useRef<UserOperation | null>(null);
  // The manifest identity adopted as "current dispatch identity" after the initial dispatch —
  // backend holds no shared current-manifest state, so the frontend pins this for refresh
  // confirmation. Set once after the initial dispatch; a refresh resolving a different manifest
  // is an explicit PROJECTION_ENTRY_MANIFEST_MISMATCH error, never a silent re-render.
  const adoptedManifestIdRef = useRef<string | undefined>(undefined);
  // Ref-backed projection token for SSE closure access — state updates don't update closed-over values.
  const projectionTokenRef = useRef<string | undefined>(undefined);
  // Runtime state store + effect runner (component_runtime_state_effect_boundary):
  // the runner — not individual components — owns lifecycle emission and effect
  // execution. Refs persist across rerenders and SSE refreshes so the store,
  // fired-registry, and runner are never re-created (initial_mount stays once-per-mount).
  const localStateStoreRef = useRef<NotifyingRuntimeLocalStateStore>(
    createRuntimeLocalStateStore(),
  );
  // Guarded mutation dispatcher — the ONLY write path for UI状態更新, shared by
  // BOTH the lifecycle runner (below) and event-triggered mutations (passed into
  // renderEmission as localStateStore, consumed by emitBoundEvent). A single
  // dispatcher instance per mount keeps declared-slot state (and the guard) in
  // sync across both mutation paths.
  const stateDispatcherRef = useRef<RuntimeStateDispatcher | null>(null);
  const effectRunnerRef = useRef<UiEventEffectRunner | null>(null);
  // Live node value tracking (component_runtime_state_effect_boundary /
  // remaining_write_payload_capture_gap): stable-node-identity register/
  // update/deregister store backing renderEmission's payloadFromNodeValues —
  // the read side dispatchExternalPort/dispatchInstanceOperation/admin_runtime
  // Lane 2 payloadFrom resolution consumes via `node:<nodeId>.value`. A ref
  // (not state) — snapshot() returns the SAME object every render so a
  // runtimeSpec closure captured at an earlier render still observes later
  // value updates without requiring a new renderEmission() call.
  const nodeValueTrackerRef = useRef<LiveNodeValueTracker>(
    createLiveNodeValueTracker(),
  );
  // Latest emission for store-notification re-render (closed-over state is stale in [] effect).
  const emissionRef = useRef<Emission | null>(null);
  const storeUnsubscribeRef = useRef<(() => void) | null>(null);

  useEffect(() => {
    projectionTokenRef.current = projectionToken;
  }, [projectionToken]);

  useEffect(() => {
    startComponentEventRuntime();
    let mounted = true;

    (async () => {
      const token = readClientSessionToken();
      if (!token) {
        if (mounted) {
          setAuthFallback(true);
          setLoading(false);
        }
        return;
      }

      // Probe accepts any valid JWT regardless of realm — login surface and capability are orthogonal.
      const probe = await probeSessionToken(token);
      if (!probe) {
        clearSessionToken();
        if (mounted) {
          setAuthFallback(true);
          setLoading(false);
        }
        return;
      }

      setProjectionToken(token);
      projectionTokenRef.current = token;

      // Detect realm from token and call matching refresh endpoint.
      const refreshResult = await refreshProjectionSession(token);
      if (!refreshResult.success) {
        // Clear JWT only on token invalidity (revoked, user deleted, account suspended).
        // Realm/surface mismatch is not token invalidity — do not destroy a valid carrier.
        const invalidityCodes = new Set([
          "AUTH_REFRESH_TOKEN_INVALID",
          "AUTH_REFRESH_USER_NOT_FOUND",
          "AUTH_USER_INACTIVE",
          "AUTH_USER_NOT_APPROVED",
          "AUTH_USER_SUSPENDED",
        ]);
        const isTokenInvalid = refreshResult.errors?.some((e) => {
          const code = e.code ?? e.Code;
          return invalidityCodes.has(code ?? "");
        }) ?? false;
        if (isTokenInvalid) {
          clearSessionToken();
          if (mounted) {
            setAuthFallback(true);
            setLoading(false);
          }
          return;
        }
        // Non-invalidity failure (realm mismatch, backend unreachable, etc.):
        // JWT is still valid per probe; continue dispatch with existing token.
      }
      if (refreshResult.token) {
        persistSessionToken(refreshResult.token);
        setProjectionToken(refreshResult.token);
        projectionTokenRef.current = refreshResult.token;
      }

      const currentToken = refreshResult.token ?? token;

      // Route/package/manifest-aware entry selection. Malformed selection is a
      // fail-close explicit error — no silent fallback to the default axes.
      const entryParse = parseProjectionEntrySelection(
        globalThis.location?.search ?? "",
      );
      if (!entryParse.ok) {
        setError(entryParse.error);
        setLoading(false);
        return;
      }
      const entrySelection = entryParse.selection;

      const initialAxes: UserOperation = resolveProjectionEntryAxes(
        entrySelection,
      );
      initialDispatchAxesRef.current = initialAxes;
      const dispatchResult = await queueClientCommand(
        initialAxes,
        currentToken,
      );

      if (!mounted) return;

      if (!dispatchResult.success) {
        setError(
          dispatchResult.errors?.[0]?.message ?? "投影の取得に失敗しました",
        );
        setLoading(false);
        return;
      }

      const nextEmission = dispatchResult.emission;
      if (!nextEmission) {
        setError("投影データを取得できませんでした");
        setLoading(false);
        return;
      }
      // Explicit package selection is confirmed against the backend-resolved
      // package identity — mismatch must not render silently.
      const entryConfirmation = confirmProjectionEntryEmission(
        entrySelection,
        nextEmission,
      );
      if (!entryConfirmation.ok) {
        setError(entryConfirmation.error);
        setLoading(false);
        return;
      }
      // Adopt the backend-resolved manifest identity as the current dispatch identity for
      // subsequent (SSE-triggered) refreshes — pins payload.target_ref when the entry had no
      // explicit ?manifest= selection, so refresh re-resolves the SAME manifest rather than
      // just replaying the same pre-resolution axes.
      initialDispatchAxesRef.current = adoptResolvedManifestIdentity(
        initialAxes,
        nextEmission,
      );
      adoptedManifestIdRef.current = nextEmission.manifestId;
      // Guarded dispatcher created (and UI監視割当 / UI状態更新-target slots
      // predeclared) BEFORE the first renderEmission call, so the very first
      // rendered event bindings already write through the guarded, shared
      // mutation path — there is no separate un-guarded first render.
      const runnerNodes = toRunnerWiringNodes(nextEmission.layoutNodes);
      nodeValueTrackerRef.current.reconcile(
        runnerNodes.map((n) => n.nodeId),
      );
      seedTrackerFromPropBindingsValue(
        nodeValueTrackerRef.current,
        nextEmission.layoutNodes ?? [],
        nextEmission.data ?? {},
        resolveRuntimeDataPath,
      );
      if (!stateDispatcherRef.current) {
        stateDispatcherRef.current = createProjectionStateDispatcher(
          runnerNodes,
          localStateStoreRef.current,
        );
      }
      const stateDispatcher = stateDispatcherRef.current;

      // Adopts a node's own settled admin_runtime dispatch result into production
      // emission — connects the EXISTING command lane response (previously
      // void-discarded at emitBoundEvent's `void enqueueRuntimeComponentCommand(...)`,
      // see runtimeComponentFactory.ts) to the SAME confirmProjectionEntryEmission +
      // setEmission/setSpecs adoption boundary the SSE refresh path already uses
      // (PR #600 review round 12). Without this, a load_button's dryRun preview data
      // (e.g. update_group's preview.groupName) never reached emission.data, so
      // propBindings.value pre-fill could never actually render.
      // forceOverwrite:true on the tracker seed — unlike the passive-refresh seeding
      // above, an explicit re-Load of a DIFFERENT record must not leave the
      // PREVIOUS record's stale tracked value display/dispatch-divergent from the
      // field just (re)loaded (see seedTrackerFromPropBindingsValue's doc comment).
      // Round 27 owner decision (child manifest response is never adopted into ae200's own
      // projection state, regardless of dispatch success): a node's own admin_runtime dispatch
      // override always targets a DIFFERENT manifest (ae210/ae220/ae230/ae240/ae250/ae260/ae270 —
      // never ae200 itself), so confirmProjectionEntryEmission's adoptedManifestId guard always
      // rejects it here (confirmation.ok === false) — this callback's ONLY prior use for a
      // same-manifest result was ae200's own read-circuit dispatch (list_groups), which is never
      // routed through this handler in practice (Lane 2's admin_runtime target_ref always points
      // at a child manifest for every seeded write override). On a rejected (cross-manifest)
      // result that nonetheless SUCCEEDED, the write itself landed — re-dispatch ae200's OWN
      // current manifest identity (refreshCurrentManifestAsync, the SAME function and identity
      // boundary the SSE refresh path uses below) to re-read group list / selected group detail /
      // items / prefill from the DB, rather than ever merging the child's own Emission fields.
      // Failure is handled entirely by the caller's own settled-result gate (emitBoundEvent's
      // onSettled / deferLocalStateMutationToDispatchSuccess) — this callback does nothing on
      // failure, so a failed write's Modal correctly stays open with no stale merge.
      //
      // Round 28: the FIRST, narrow, purpose-built check is whether this result's manifestId
      // matches the CURRENTLY ADOPTED identity — never confirmProjectionEntryEmission's broader
      // package/explicit-?manifest=-selection checks, which exist to confirm "is this the right
      // thing to RENDER" (meaningful for the initial load and a redispatch's own response, which
      // ARE candidates for rendering) and are the WRONG tool here: a settled child-manifest write
      // result is by design NEVER going to be rendered regardless of whether it happens to satisfy
      // an explicit ?manifest= URL selection, so checking it against that selection would
      // misclassify the ordinary, expected case (a legitimate child write, whose manifestId simply
      // isn't ae200's) as a genuine anomaly whenever the URL selection happens to equal the
      // adopted identity (the common case) — collapsing round 27's redispatch behavior back into a
      // spurious warning. A mismatch here (manifestId differs from adopted) is ALWAYS the ordinary
      // cross-manifest child-write shape → canonical reread. Only when the manifestId DOES match
      // the adopted identity (a same-manifest result — theoretically possible in a future ae200-
      // native write) do the explicit package/manifest checks apply, since only THEN is adoption
      // itself in play; confirmation.reason then distinguishes a genuine explicit-selection
      // violation (surfaced as a warning) from the (here, structurally unreachable — manifestId
      // already matched) adopted-identity branch.
      const handleRuntimeDispatchResult = (
        _nodeId: string,
        result: DispatchResponse,
      ) => {
        if (!mounted) return;
        if (!result.success || !result.emission) return;
        const dispatched = result.emission;
        if (dispatched.manifestId !== adoptedManifestIdRef.current) {
          void refreshCurrentManifestAsync("canonical_reread");
          return;
        }
        const confirmation = confirmProjectionEntryEmission(
          entrySelection,
          dispatched,
          { adoptedManifestId: adoptedManifestIdRef.current },
        );
        if (!confirmation.ok) {
          console.error(
            "[ProjectionShell] EXPLICIT_SELECTION_VIOLATED_ON_RUNTIME_DISPATCH_RESULT:",
            confirmation.error,
          );
          setRefreshWarning(confirmation.error);
          return;
        }
        setRefreshWarning(null);
        setEmission(dispatched);
        emissionRef.current = dispatched;
        const dispatchedNodes = toRunnerWiringNodes(dispatched.layoutNodes);
        nodeValueTrackerRef.current.reconcile(
          dispatchedNodes.map((n) => n.nodeId),
        );
        seedTrackerFromPropBindingsValue(
          nodeValueTrackerRef.current,
          dispatched.layoutNodes ?? [],
          dispatched.data ?? {},
          resolveRuntimeDataPath,
          { forceOverwrite: true },
        );
        setSpecs(renderEmission(dispatched, defaultComponentRegistry, {
          localStateStore: stateDispatcherRef.current ?? undefined,
          payloadFromNodeValues: nodeValueTrackerRef.current.snapshot(),
          onNodeValueChange: nodeValueTrackerRef.current.set,
          onRuntimeDispatchResult: handleRuntimeDispatchResult,
        }));
      };

      setEmission(nextEmission);
      emissionRef.current = nextEmission;
      setSpecs(renderEmission(nextEmission, defaultComponentRegistry, {
        localStateStore: stateDispatcher,
        payloadFromNodeValues: nodeValueTrackerRef.current.snapshot(),
        onNodeValueChange: nodeValueTrackerRef.current.set,
        onRuntimeDispatchResult: handleRuntimeDispatchResult,
      }));
      setLoading(false);

      // uiEventEffectRunner: UI監視割当 slots are declared at runner creation
      // (before any mutation/effect), then runtime synthetic lifecycle triggers
      // are emitted once. The fired-registry inside the runner guarantees
      // initial_mount does not re-dispatch on rerender or SSE refresh.
      if (!effectRunnerRef.current) {
        // store update -> projection rerender hook: any state write through the
        // runtime dispatcher (runner lifecycle emission or event-lane
        // localStateMutation) re-renders the projection so UI状態更新 reflects
        // into rendered runtimeSpec props. Subscribed BEFORE lifecycle emission.
        storeUnsubscribeRef.current = localStateStoreRef.current.subscribe(
          () => {
            const current = emissionRef.current;
            if (!current || !mounted) return;
            setSpecs(renderEmission(current, defaultComponentRegistry, {
              localStateStore: stateDispatcherRef.current ?? stateDispatcher,
              payloadFromNodeValues: nodeValueTrackerRef.current.snapshot(),
              onNodeValueChange: nodeValueTrackerRef.current.set,
              onRuntimeDispatchResult: handleRuntimeDispatchResult,
            }));
          },
        );
        const runner = createUiEventEffectRunner({
          nodes: runnerNodes,
          dispatcher: stateDispatcher,
          layoutId: nextEmission.layoutId,
          packageId: nextEmission.packageId,
        });
        effectRunnerRef.current = runner;
        for (
          const trigger of [
            "initial_mount",
            "route_enter",
            "initial_display",
          ] as const
        ) {
          const result = runner.emitLifecycle(trigger);
          if (!result.ok) {
            console.error(
              `[ProjectionShell] LIFECYCLE_EFFECT_RUNNER_BLOCKED (${trigger}):`,
              result.errors,
            );
          }
        }
      }

      // Round 27: single shared re-dispatch function — the SAME manifest identity boundary
      // (initialDispatchAxesRef, adoptedManifestIdRef, refreshGenRef generation guard) used by
      // BOTH the SSE invalidation path below AND a settled cross-manifest write's success path
      // above (handleRuntimeDispatchResult). Never performs frontend topology/layout judgment;
      // it only re-dispatches the CURRENTLY adopted axes (optionally merging identity-preserving
      // payload fields the SSE path forwards) and adopts the response ONLY if
      // confirmProjectionEntryEmission still confirms the SAME manifest identity — a differently-
      // manifested response is rejected exactly the same way a stale one is. A higher
      // refreshGenRef generation started after this call began (a newer SSE event, or a second
      // write settling first) discards this call's result rather than let it overwrite newer
      // state — same discipline the SSE path already had, now shared instead of duplicated.
      //
      // Round 28: `intent` is a GENERIC (never operation/nodeId/manifest-UUID-keyed) distinction
      // between the two reasons this function is ever called — derived purely from WHICH caller
      // invoked it, never from inspecting response content:
      //   - "passive_invalidation": an SSE event only signals something MAY have changed; a value
      //     the user is actively editing in a still-open field must survive this refresh, so the
      //     tracker reconciliation below does NOT force-overwrite (matches the pre-round-28 SSE
      //     behavior, unchanged).
      //   - "canonical_reread": a write just settled — the DB is now the sole authority for
      //     whatever the write touched (a stale typed value, a stale prefill, a selection that no
      //     longer exists) and the tracker must be forced back to what the fresh read carries, the
      //     same forceOverwrite:true discipline handleRuntimeDispatchResult's OWN same-manifest
      //     adoption branch already used above.
      // A failure at any stage (dispatch failure, missing Emission, or a genuine identity mismatch
      // on the redispatch's OWN response) surfaces as an explicit, non-destructive refreshWarning
      // (pipeline-continuity-ssot.yaml explicit_error_log_retain_old_dom) — the existing DOM is
      // never blanked, the write is never re-sent, and no Modal is reopened; only a stale
      // (superseded-generation) or unmounted result is discarded silently, as before.
      const refreshCurrentManifestAsync = async (
        intent: "passive_invalidation" | "canonical_reread",
        identityPayload?: Record<string, unknown>,
      ) => {
        const gen = ++refreshGenRef.current;
        try {
          if (gen !== refreshGenRef.current || !mounted) return;
          const refreshToken = projectionTokenRef.current;
          if (!refreshToken) return;
          const storedAxes = initialDispatchAxesRef.current;
          if (!storedAxes) return;

          const mergedPayload = identityPayload && Object.keys(identityPayload).length > 0
            ? { ...(storedAxes.payload ?? {}), ...identityPayload }
            : storedAxes.payload;
          const axes: UserOperation = {
            ...storedAxes,
            ...(mergedPayload !== undefined ? { payload: mergedPayload } : {}),
          };

          const result = await queueClientCommand(axes, refreshToken);
          if (gen !== refreshGenRef.current || !mounted) return;
          if (!result.success) {
            console.error(
              "[ProjectionShell] MANIFEST_REFRESH_DISPATCH_FAILED:",
              result.errors,
            );
            setRefreshWarning(
              result.errors?.[0]?.message ?? "最新データの再取得に失敗しました",
            );
            return;
          }
          const updated = result.emission;
          if (!updated) {
            console.error("[ProjectionShell] MANIFEST_REFRESH_EMISSION_MISSING");
            setRefreshWarning("最新データの再取得に失敗しました（応答データなし）");
            return;
          }
          const refreshConfirmation = confirmProjectionEntryEmission(
            entrySelection,
            updated,
            { adoptedManifestId: adoptedManifestIdRef.current },
          );
          if (!refreshConfirmation.ok) {
            console.error(
              "[ProjectionShell] PROJECTION_ENTRY_MISMATCH_ON_REFRESH:",
              refreshConfirmation.error,
            );
            setRefreshWarning(refreshConfirmation.error);
            return;
          }
          setRefreshWarning(null);
          setEmission(updated);
          emissionRef.current = updated;
          const refreshedNodes = toRunnerWiringNodes(updated.layoutNodes);
          nodeValueTrackerRef.current.reconcile(
            refreshedNodes.map((n) => n.nodeId),
          );
          seedTrackerFromPropBindingsValue(
            nodeValueTrackerRef.current,
            updated.layoutNodes ?? [],
            updated.data ?? {},
            resolveRuntimeDataPath,
            { forceOverwrite: intent === "canonical_reread" },
          );
          if (effectRunnerRef.current) {
            effectRunnerRef.current.updateNodes(refreshedNodes);
            for (
              const trigger of [
                "initial_mount",
                "route_enter",
                "initial_display",
              ] as const
            ) {
              const lifecycleResult = effectRunnerRef.current.emitLifecycle(trigger);
              if (!lifecycleResult.ok) {
                console.error(
                  `[ProjectionShell] LIFECYCLE_EFFECT_RUNNER_BLOCKED_ON_REFRESH (${trigger}):`,
                  lifecycleResult.errors,
                );
              }
            }
          } else if (stateDispatcherRef.current) {
            predeclareProjectionState(
              refreshedNodes,
              stateDispatcherRef.current,
            );
          }
          setSpecs(renderEmission(updated, defaultComponentRegistry, {
            localStateStore: stateDispatcherRef.current ?? undefined,
            payloadFromNodeValues: nodeValueTrackerRef.current.snapshot(),
            onNodeValueChange: nodeValueTrackerRef.current.set,
            onRuntimeDispatchResult: handleRuntimeDispatchResult,
          }));
        } catch (err) {
          if (gen !== refreshGenRef.current || !mounted) return;
          console.error(
            "[ProjectionShell] MANIFEST_REFRESH_ERROR:",
            err,
          );
          setRefreshWarning("最新データの再取得中にエラーが発生しました");
        }
      };

      // sse_projection_lane wiring per runtime-orchestration-ssot.yaml:
      //   sseReceiver → enqueueProjectionHookTrigger → sseDispatcher → projectionRuntime → onProjectionUpdate
      //
      // projectionRuntime is the projection_runtime node in the SSOT lane.
      // It processes the SSE event payload and fires onProjectionUpdate with all three
      // identity fields preserved: manifest_id, table_id, table_registry_id.
      // The onProjectionUpdate handler re-fetches the full layout emission via
      // queueClientCommand, forwarding all three identity fields verbatim as
      // payload context (never discarded). It never writes manifest_id into
      // axes.target — the entry selection's route/default target axis (and any
      // manifest-pinned payload.target_ref) survive every refresh unchanged.
      const projectionRuntime = createProjectionRuntime();
      // projectionDefinition is a manifest-response data-defined surface (projection_constructor_mapping).
      // No frontend fallback — if absent, projectionRuntime emits PROJECTION_RUNTIME_DEFINITION_MISSING
      // on SSE events (definitionMissingPolicy:"error" default is the explicit fail-close).
      if (nextEmission.projectionDefinition) {
        projectionRuntime.setProjectionDefinition(
          nextEmission.projectionDefinition,
        );
      }

      projectionRuntime.onProjectionUpdate((_uiProjection, payload) => {
        // SSE payload is an invalidation/refresh trigger only (SSOT:
        // pipeline-continuity-ssot.yaml sse_projection_lane.refresh_boundary_policy
        // trigger_role: invalidation_refresh_trigger_only) — it must not perform frontend
        // topology/layout judgment or silently change WHICH entry is dispatched.
        // manifest_id / table_id / table_registry_id are forwarded verbatim as
        // identity-preserving payload context (never discarded) via refreshCurrentManifestAsync's
        // identityPayload parameter — merged INTO the stored entry payload, never replacing it,
        // and axes.target is never overwritten (see refreshCurrentManifestAsync above).
        const identityPayload: Record<string, unknown> = {};
        if (payload.manifest_id) identityPayload.manifest_id = payload.manifest_id;
        if (payload.table_id) identityPayload.table_id = payload.table_id;
        if (payload.table_registry_id) {
          identityPayload.table_registry_id = payload.table_registry_id;
        }
        void refreshCurrentManifestAsync("passive_invalidation", identityPayload);
      });

      const dispatcher = createSseDispatcherWithProjectionRuntime(
        projectionRuntime,
        { unhandledEventPolicy: "log" },
      );

      const receiver = createSseReceiver({
        token: projectionTokenRef.current,
        onProjectionHookTrigger: (trigger: ProjectionHookTrigger) => {
          // sse_projection_lane bridge — routes through projectionRuntime, not direct dispatch.
          enqueueProjectionHookTrigger(trigger, dispatcher);
        },
        onError: (state) => {
          console.error("[ProjectionShell] SSE connection error:", state);
        },
      });

      sseReceiverRef.current = receiver;
      receiver.connect();
    })();

    return () => {
      mounted = false;
      storeUnsubscribeRef.current?.();
      storeUnsubscribeRef.current = null;
      sseReceiverRef.current?.disconnect();
      sseReceiverRef.current = null;
    };
  }, []);

  if (loading) {
    return (
      <div
        class="py-8 text-center text-gray-400"
        aria-busy="true"
        aria-live="polite"
      >
        投影を取得中...
      </div>
    );
  }

  if (authFallback) {
    return (
      <div class="rounded-lg border border-blue-200 bg-blue-50 p-4">
        <p class="font-semibold text-blue-800">ログインが必要です</p>
        <p class="mt-1 text-sm text-blue-700">
          未ログイン、期限切れ、または更新失敗のため投影アプリを安全に停止しました。
          ログインまたは新規登録から再開してください。
        </p>
        <div class="mt-3 flex gap-3 text-sm">
          <a href="/auth" class="link font-semibold">ログインへ</a>
          <a href="/auth#register" class="link font-semibold">新規登録へ</a>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div class="rounded-lg border border-red-200 bg-red-50 p-4">
        <p class="font-semibold text-red-700">エラー</p>
        <p class="mt-1 text-sm text-red-600">{error}</p>
      </div>
    );
  }

  const recommendProjection = emission?.recommendNavigationProjection;
  const hubNavigationLinks = resolveHubNavigationLinks(
    emission?.navigationSequence,
  );

  return (
    <div
      data-projection-island="main_projection_island"
      data-projection-surface="product"
      data-primary-dom-projection
    >
      {refreshWarning && (
        <div
          data-projection-refresh-warning
          class="mb-3 rounded-lg border border-amber-200 bg-amber-50 p-3"
        >
          <p class="text-sm text-amber-700">{refreshWarning}</p>
        </div>
      )}
      <LayoutProjectionTree
        specs={specs}
        layoutId={emission?.layoutId}
      />
      {hubNavigationLinks.length > 0 && (
        <nav
          data-projection-hub-navigation
          class="mt-4 flex flex-wrap items-center gap-2 border-t border-gray-200 pt-3 text-xs"
        >
          {hubNavigationLinks.map((link) =>
            link.resolvable
              ? (
                <a
                  key={link.href}
                  href={link.href}
                  class="link"
                  data-hub-navigation-resolvable
                >
                  {link.label}
                </a>
              )
              : (
                <span
                  key={`unresolvable-${link.sequencePosition}-${link.label}`}
                  class="text-gray-400"
                  title="複数または未登録の画面設定のため直接移動できません"
                  data-hub-navigation-unresolvable
                >
                  {link.label}
                </span>
              )
          )}
          {emission?.manifestId && (
            <details class="ml-auto text-gray-400">
              <summary class="cursor-pointer">技術情報</summary>
              <code class="font-mono">{emission.manifestId}</code>
            </details>
          )}
        </nav>
      )}
      {recommendProjection && (
        <RecommendNavigationIsland
          spec={recommendProjection}
          token={projectionToken}
        />
      )}
    </div>
  );
}
