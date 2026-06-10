import { useEffect, useRef, useState } from "preact/hooks";
import type { JSX } from "preact";
import { probeSessionToken, refreshUserSession } from "../api/authApi.ts";
import { clearSessionToken, persistSessionToken, readClientSessionToken } from "../lib/demoSession.ts";
import { queueClientCommand, startComponentEventRuntime } from "../runtime/frontendScheduler.ts";
import { renderEmission, type ComponentSpec } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { createSseReceiver, type ProjectionHookTrigger, type SseReceiver } from "../runtime/sseReceiver.ts";
import type { Emission } from "../api/dispatch.ts";
import { RecommendNavigationIsland } from "../components/RecommendNavigationIsland.tsx";
import { LayoutProjectionTree } from "../components/LayoutProjectionTree.tsx";

/**
 * Production application projection shell.
 * Dispatches default:entity:search on mount and renders the emission
 * using layout-aware renderEmission(). Subscribes to SSE projection events
 * to refresh the DOM tree when the backend emits a projection event.
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

      const probe = await probeSessionToken(token, "user");
      if (!probe) {
        if (mounted) {
          setAuthFallback(true);
          setLoading(false);
        }
        return;
      }

      setProjectionToken(token);

      const refreshResult = await refreshUserSession();
      if (!refreshResult.success) {
        clearSessionToken();
        if (mounted) {
          setAuthFallback(true);
          setLoading(false);
        }
        return;
      }
      if (refreshResult.token) {
        persistSessionToken(refreshResult.token);
        setProjectionToken(refreshResult.token);
      }

      const dispatchResult = await queueClientCommand({
        operationType: "Search",
        target: "default",
        layer: "screen_list",
        action: "Search",
      });

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

      const receiver = createSseReceiver({
        onProjectionHookTrigger: (_trigger: ProjectionHookTrigger) => {
          const gen = ++refreshGenRef.current;
          (async () => {
            try {
              if (gen !== refreshGenRef.current || !mounted) return;
              const result = await queueClientCommand({
                operationType: "Search",
                target: "default",
                layer: "screen_list",
                action: "Search",
              });
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
