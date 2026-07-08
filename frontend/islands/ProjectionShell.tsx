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
import type { WiringNode } from "../lib/uiBuilderWiringProjection.ts";
import {
  confirmProjectionEntryEmission,
  parseProjectionEntrySelection,
  resolveProjectionEntryAxes,
} from "../runtime/projectionEntry.ts";
import type { Emission, LayoutNode } from "../api/dispatch.ts";
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
  const initialDispatchAxesRef = useRef<UserOperation | null>(null);
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
      // Guarded dispatcher created (and UI監視割当 / UI状態更新-target slots
      // predeclared) BEFORE the first renderEmission call, so the very first
      // rendered event bindings already write through the guarded, shared
      // mutation path — there is no separate un-guarded first render.
      const runnerNodes = toRunnerWiringNodes(nextEmission.layoutNodes);
      if (!stateDispatcherRef.current) {
        stateDispatcherRef.current = createProjectionStateDispatcher(
          runnerNodes,
          localStateStoreRef.current,
        );
      }
      const stateDispatcher = stateDispatcherRef.current;

      setEmission(nextEmission);
      emissionRef.current = nextEmission;
      setSpecs(renderEmission(nextEmission, defaultComponentRegistry, {
        localStateStore: stateDispatcher,
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

      // sse_projection_lane wiring per runtime-orchestration-ssot.yaml:
      //   sseReceiver → enqueueProjectionHookTrigger → sseDispatcher → projectionRuntime → onProjectionUpdate
      //
      // projectionRuntime is the projection_runtime node in the SSOT lane.
      // It processes the SSE event payload and fires onProjectionUpdate with all three
      // identity fields preserved: manifest_id, table_id, table_registry_id.
      // The onProjectionUpdate handler re-fetches the full layout emission via queueClientCommand,
      // forwarding identity fields (manifest_id → target, table_id / table_registry_id → payload).
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
        const gen = ++refreshGenRef.current;
        (async () => {
          try {
            if (gen !== refreshGenRef.current || !mounted) return;
            // Use projectionTokenRef (ref-backed) — closed-over state is stale in [] effect.
            const refreshToken = projectionTokenRef.current;
            if (!refreshToken) return;
            const storedAxes = initialDispatchAxesRef.current;
            if (!storedAxes) return;

            // Forward all non-absent identity fields from SSE payload without discard.
            const identityPayload: Record<string, unknown> = {};
            if (payload.table_id) identityPayload.table_id = payload.table_id;
            if (payload.table_registry_id) {
              identityPayload.table_registry_id = payload.table_registry_id;
            }

            // Merge identity fields INTO the stored entry payload (never replace):
            // a manifest-pinned entry keeps its payload.target_ref across SSE
            // refresh, so another manifest's event cannot silently retarget it.
            const mergedPayload = {
              ...(storedAxes.payload ?? {}),
              ...identityPayload,
            };
            const axes: UserOperation = {
              ...storedAxes,
              ...(payload.manifest_id ? { target: payload.manifest_id } : {}),
              ...(Object.keys(mergedPayload).length > 0
                ? { payload: mergedPayload }
                : {}),
            };

            const result = await queueClientCommand(axes, refreshToken);
            if (!result.success || gen !== refreshGenRef.current || !mounted) {
              return;
            }
            const updated = result.emission;
            if (!updated) return;
            setEmission(updated);
            emissionRef.current = updated;
            // SSE refresh reconciliation: same dispatcher / runner / fired-registry
            // (refs are never re-created), so existing state and fired lifecycle
            // interactions survive. The runner's node list is reconciled to the
            // refreshed projection (predeclaring any newly-appeared UI状態更新
            // target, recomputing the loop/policy guards), then lifecycle triggers
            // are re-emitted so a newly-appeared node's initial_mount executes
            // exactly once — the fired-registry (keyed by nodeId + interaction
            // index) guarantees an existing node's already-fired interaction does
            // not re-execute.
            const refreshedNodes = toRunnerWiringNodes(updated.layoutNodes);
            if (effectRunnerRef.current) {
              effectRunnerRef.current.updateNodes(refreshedNodes);
              for (
                const trigger of [
                  "initial_mount",
                  "route_enter",
                  "initial_display",
                ] as const
              ) {
                const result = effectRunnerRef.current.emitLifecycle(trigger);
                if (!result.ok) {
                  console.error(
                    `[ProjectionShell] LIFECYCLE_EFFECT_RUNNER_BLOCKED_ON_REFRESH (${trigger}):`,
                    result.errors,
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
            }));
          } catch (err) {
            if (gen !== refreshGenRef.current || !mounted) return;
            console.error(
              "[ProjectionShell] SSE_PROJECTION_REFRESH_ERROR:",
              err,
            );
          }
        })();
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

  return (
    <div
      data-projection-island="main_projection_island"
      data-projection-surface="product"
      data-primary-dom-projection
    >
      <LayoutProjectionTree
        specs={specs}
        layoutId={emission?.layoutId}
      />
      {recommendProjection && (
        <RecommendNavigationIsland
          spec={recommendProjection}
          token={projectionToken}
        />
      )}
    </div>
  );
}
