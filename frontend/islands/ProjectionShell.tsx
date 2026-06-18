import { useEffect, useRef, useState } from "preact/hooks";
import type { JSX } from "preact";
import { probeSessionToken, refreshProjectionSession } from "../api/authApi.ts";
import { clearSessionToken, persistSessionToken, readClientSessionToken } from "../lib/demoSession.ts";
import { enqueueProjectionHookTrigger, queueClientCommand, startComponentEventRuntime } from "../runtime/frontendScheduler.ts";
import type { UserOperation } from "../runtime/resolveOperationVector.ts";
import { renderEmission, type ComponentSpec } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { createSseReceiver, type ProjectionHookTrigger, type SseReceiver } from "../runtime/sseReceiver.ts";
import { createSseDispatcher } from "../runtime/sseDispatcher.ts";
import type { Emission } from "../api/dispatch.ts";
import { RecommendNavigationIsland } from "../components/RecommendNavigationIsland.tsx";
import { LayoutProjectionTree } from "../components/LayoutProjectionTree.tsx";

/**
 * Production application projection shell.
 * Dispatches default:entity:search on mount and renders the emission
 * using layout-aware renderEmission(). Subscribes to SSE projection events
 * via the sse_projection_lane:
 *   sseReceiver → enqueueProjectionHookTrigger → sseDispatcher → projection handler
 *
 * Identity preservation: all three identity fields from ProjectionHookTrigger
 * (manifestId, tableId, tableRegistryId) are extracted from the SSE event payload
 * and forwarded in the re-dispatch request as target (manifestId) and payload
 * (table_id, table_registry_id). No field is silently discarded.
 */
export default function ProjectionShell(): JSX.Element {
  const [loading, setLoading] = useState(true);
  const [emission, setEmission] = useState<Emission | null>(null);
  const [specs, setSpecs] = useState<ComponentSpec[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [authFallback, setAuthFallback] = useState(false);
  const [projectionToken, setProjectionToken] = useState<string | undefined>(undefined);

  // Generation counter prevents stale SSE refresh responses from overwriting newer results.
  const refreshGenRef = useRef(0);
  // Holds the SSE receiver so unmount cleanup can disconnect it.
  const sseReceiverRef = useRef<SseReceiver | null>(null);
  // Stores the initial dispatch axes for SSE identity-preserving refresh.
  // SSE triggers must re-dispatch using the same axes as the initial load, not hardcoded defaults.
  const initialDispatchAxesRef = useRef<UserOperation | null>(null);
  // Ref-backed projection token for SSE closure access — state updates don't update closed-over values.
  const projectionTokenRef = useRef<string | undefined>(undefined);

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

      const initialAxes: UserOperation = {
        operationType: "Search",
        target: "default",
        layer: "screen_list",
        action: "Search",
      };
      initialDispatchAxesRef.current = initialAxes;
      const dispatchResult = await queueClientCommand(initialAxes, currentToken);

      if (!mounted) return;

      if (!dispatchResult.success) {
        setError(dispatchResult.errors?.[0]?.message ?? "投影の取得に失敗しました");
        setLoading(false);
        return;
      }

      const nextEmission = dispatchResult.emission;
      if (!nextEmission) {
        setError("投影データを取得できませんでした");
        setLoading(false);
        return;
      }
      setEmission(nextEmission);
      setSpecs(renderEmission(nextEmission, defaultComponentRegistry));
      setLoading(false);

      // sse_projection_lane wiring:
      //   sseReceiver.onProjectionHookTrigger → enqueueProjectionHookTrigger → sseDispatcher → handler
      //
      // The dispatcher decouples SSE event routing from the dispatch call.
      // The "projection" handler below preserves all three identity fields from the SSE payload:
      //   manifest_id → target override (routes to specific manifest)
      //   table_id    → forwarded in payload (identity for backend row routing)
      //   table_registry_id → forwarded in payload (registry identity)
      const dispatcher = createSseDispatcher({ unhandledEventPolicy: "log" });

      dispatcher.register("projection", (rawData: string) => {
        const gen = ++refreshGenRef.current;
        (async () => {
          try {
            if (gen !== refreshGenRef.current || !mounted) return;
            // Use projectionTokenRef (ref-backed) — closed-over state is stale in [] effect.
            const refreshToken = projectionTokenRef.current;
            if (!refreshToken) return;
            const storedAxes = initialDispatchAxesRef.current;
            if (!storedAxes) return;

            // Parse SSE event payload — extract all three identity fields without discard.
            // Parse failure falls through: identity fields remain undefined, storedAxes used as-is.
            let manifestId: string | undefined;
            let tableId: string | undefined;
            let tableRegistryId: string | undefined;
            try {
              const parsed = JSON.parse(rawData) as Record<string, unknown>;
              manifestId = typeof parsed.manifest_id === "string" ? parsed.manifest_id : undefined;
              tableId = typeof parsed.table_id === "string" ? parsed.table_id : undefined;
              tableRegistryId = typeof parsed.table_registry_id === "string" ? parsed.table_registry_id : undefined;
            } catch { /* proceed with storedAxes identity — parse error is not a dispatch error */ }

            // Forward all non-absent identity fields.
            const identityPayload: Record<string, unknown> = {};
            if (tableId) identityPayload.table_id = tableId;
            if (tableRegistryId) identityPayload.table_registry_id = tableRegistryId;

            const axes: UserOperation = {
              ...storedAxes,
              ...(manifestId ? { target: manifestId } : {}),
              ...(Object.keys(identityPayload).length > 0 ? { payload: identityPayload } : {}),
            };

            const result = await queueClientCommand(axes, refreshToken);
            if (!result.success || gen !== refreshGenRef.current || !mounted) return;
            const updated = result.emission;
            if (!updated) return;
            setEmission(updated);
            setSpecs(renderEmission(updated, defaultComponentRegistry));
          } catch (err) {
            if (gen !== refreshGenRef.current || !mounted) return;
            console.error("[ProjectionShell] SSE_PROJECTION_REFRESH_ERROR:", err);
          }
        })();
      });

      const receiver = createSseReceiver({
        token: projectionTokenRef.current,
        onProjectionHookTrigger: (trigger: ProjectionHookTrigger) => {
          // sse_projection_lane bridge — routes through sseDispatcher, not direct dispatch.
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
      sseReceiverRef.current?.disconnect();
      sseReceiverRef.current = null;
    };
  }, []);

  if (loading) {
    return (
      <div class="py-8 text-center text-gray-400" aria-busy="true" aria-live="polite">
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
        <RecommendNavigationIsland spec={recommendProjection} token={projectionToken} />
      )}
    </div>
  );
}
