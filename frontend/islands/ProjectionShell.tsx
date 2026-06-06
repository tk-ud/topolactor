import { useEffect, useRef, useState } from "preact/hooks";
import { h, type JSX } from "preact";
import { probeSessionToken, refreshUserSession } from "../api/authApi.ts";
import { clearSessionToken, persistSessionToken, readClientSessionToken } from "../lib/demoSession.ts";
import { queueClientCommand, startComponentEventRuntime } from "../runtime/frontendScheduler.ts";
import { buildChildrenMap, renderEmission, type ComponentSpec } from "../runtime/renderEmission.ts";
import { renderRuntimeComponent } from "../runtime/runtimePrimitiveRenderer.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { createSseReceiver, type SseReceiver } from "../runtime/sseReceiver.ts";
import type { Emission } from "../api/dispatch.ts";
import { RecommendNavigationIsland } from "../components/RecommendNavigationIsland.tsx";

/** Recursively renders a single layout node as a DOM element with its children. */
function LayoutNode(
  { spec, childrenMap }: { spec: ComponentSpec; childrenMap: Map<string | undefined, ComponentSpec[]> },
): JSX.Element {
  const children = childrenMap.get(spec.nodeId) ?? [];

  const style: Record<string, string> = {};
  if (spec.x !== undefined || spec.y !== undefined || spec.width !== undefined || spec.height !== undefined) {
    style.position = "absolute";
    if (spec.x !== undefined) style.left = `${spec.x}px`;
    if (spec.y !== undefined) style.top = `${spec.y}px`;
    if (spec.width !== undefined) style.width = `${spec.width}px`;
    if (spec.height !== undefined) style.height = `${spec.height}px`;
  }

  const className = spec.layoutClassRefs?.join(" ") || undefined;

  const childElements = children.map((child) => (
    <LayoutNode key={child.nodeId ?? child.slotKey ?? child.componentId} spec={child} childrenMap={childrenMap} />
  ));

  if (spec.nodeKind === "structural_html" && spec.htmlTag) {
    return h(
      spec.htmlTag,
      { style: Object.keys(style).length > 0 ? style : undefined, class: className, "data-node-id": spec.nodeId },
      ...childElements,
    ) as JSX.Element;
  }

  // catalog_component primary path: render via runtimeComponentFactory when runtimeSpec is present.
  if (spec.runtimeSpec) {
    const rendered = renderRuntimeComponent(spec.runtimeSpec);
    if (rendered.ok) {
      return (
        <div style={Object.keys(style).length > 0 ? style : undefined} class={className} data-node-id={spec.nodeId}>
          {rendered.node}
          {childElements}
        </div>
      );
    }
    // renderRuntimeComponent failed — explicit error, not silent fallback.
    console.warn(`[ProjectionShell] catalog_component render failed for node '${spec.nodeId}': ${rendered.error}`);
    return (
      <div style={Object.keys(style).length > 0 ? style : undefined} class={className} data-node-id={spec.nodeId}>
        <SpecCard spec={{ ...spec, componentType: "error", def: { error: rendered.error } }} index={0} />
        {childElements}
      </div>
    );
  }

  // No runtimeSpec on catalog_component: explicit warning (not silent), fall back to SpecCard.
  if (spec.nodeKind === "catalog_component") {
    console.warn(`[ProjectionShell] catalog_component node '${spec.nodeId}' has no runtimeSpec — componentKind absent or adaptation failed.`);
  }

  return (
    <div style={Object.keys(style).length > 0 ? style : undefined} class={className} data-node-id={spec.nodeId}>
      <SpecCard spec={spec} index={0} />
      {childElements}
    </div>
  );
}

function SpecCard({ spec, index }: { spec: ComponentSpec; index: number }): JSX.Element {
  const isError = spec.componentType === "error";
  return (
    <div
      class={`rounded-lg border p-4 ${
        isError ? "border-red-200 bg-red-50" : "border-gray-200 bg-white"
      }`}
    >
      {spec.slotKey && (
        <p class="mb-1 text-xs font-mono text-blue-500">
          slot: {spec.slotKey}
          {spec.orderIndex !== undefined ? ` (order: ${spec.orderIndex})` : ""}
        </p>
      )}
      <p class="font-semibold text-sm text-gray-700">
        {isError ? "投影エラー" : spec.componentType}
      </p>
      {spec.componentId && (
        <p class="mt-1 text-xs font-mono text-gray-400">{spec.componentId}</p>
      )}
      {isError && (
        <p class="mt-2 text-xs text-red-600">{String(spec.def.error)}</p>
      )}
      {!isError && !spec.slotKey && (
        <p class="mt-1 text-xs text-gray-400">コンポーネント {index + 1}</p>
      )}
    </div>
  );
}

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
      let token = readClientSessionToken();
      if (!token || !(await probeSessionToken(token, "user"))) {
        const refreshed = await refreshUserSession();
        if (refreshed.success && refreshed.token) {
          persistSessionToken(refreshed.token);
          token = refreshed.token;
        } else {
          clearSessionToken();
          if (mounted) {
            setAuthFallback(true);
            setLoading(false);
          }
          return;
        }
      }

      if (mounted) setProjectionToken(token ?? undefined);

      const response = await queueClientCommand(
        { operationType: "Search", target: "default", layer: "entity", action: "Search" },
        token ?? undefined,
        {},
      );

      if (!mounted) return;

      if (response.emission) {
        const em = response.emission;
        setEmission(em);
        setSpecs(renderEmission(em, defaultComponentRegistry));
      } else {
        const firstError = response.errors?.[0];
        const code = firstError?.Code ?? firstError?.code ?? "";
        if (code.startsWith("AUTH_")) {
          clearSessionToken();
          setAuthFallback(true);
        } else {
          const msg =
            firstError?.Message ??
            firstError?.message ??
            "投影の取得に失敗しました";
          setError(msg);
        }
      }
      setLoading(false);

      if (!mounted) return;

      // Connect SSE after auth succeeds. The receiver reads the token from sessionStorage.
      // SSE projection events are treated as invalidation/refresh triggers only —
      // the payload is not interpreted for topology or layout judgment.
      const capturedToken = token;
      const receiver = createSseReceiver({
        onProjectionHookTrigger: (_trigger) => {
          if (!mounted) return;
          const gen = ++refreshGenRef.current;
          void (async () => {
            try {
              const refreshResponse = await queueClientCommand(
                { operationType: "Search", target: "default", layer: "entity", action: "Search" },
                capturedToken ?? undefined,
                {},
              );
              // Discard stale refresh: if a newer SSE event triggered another refresh,
              // only the latest result applies (explicit race control, not silent failure).
              if (gen !== refreshGenRef.current || !mounted) return;

              if (refreshResponse.emission) {
                const em = refreshResponse.emission;
                setEmission(em);
                setSpecs(renderEmission(em, defaultComponentRegistry));
              } else {
                const firstError = refreshResponse.errors?.[0];
                const code = firstError?.Code ?? firstError?.code ?? "";
                if (code.startsWith("AUTH_")) {
                  clearSessionToken();
                  setAuthFallback(true);
                } else {
                  // Refresh failed: log explicitly and retain old DOM. Never silent failure.
                  const msg =
                    firstError?.Message ??
                    firstError?.message ??
                    "SSE projection refresh failed";
                  console.error("[ProjectionShell] SSE_PROJECTION_REFRESH_FAILED:", msg);
                }
              }
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

  const hasLayout = Boolean(emission?.layoutId);
  const hasErrors = specs.some((s) => s.componentType === "error");
  const hasTreeNodes = hasLayout && specs.some((s) => s.nodeId !== undefined);

  const recommendProjection = emission?.recommendNavigationProjection;

  if (hasLayout && hasTreeNodes && !hasErrors) {
    const childrenMap = buildChildrenMap(specs);
    const rootSpecs = childrenMap.get(undefined) ?? [];

    return (
      <div data-projection-island="main_projection_island">
        <p class="mb-3 text-xs font-mono text-blue-500">
          layout: {emission!.layoutId}
        </p>
        <div class="relative" data-primary-dom-projection="true">
          {rootSpecs.map((spec) => (
            <LayoutNode
              key={spec.nodeId ?? spec.slotKey ?? spec.componentId}
              spec={spec}
              childrenMap={childrenMap}
            />
          ))}
        </div>
        {recommendProjection && (
          <RecommendNavigationIsland spec={recommendProjection} token={projectionToken} />
        )}
      </div>
    );
  }

  return (
    <div data-projection-island="main_projection_island">
      {hasLayout && (
        <p class="mb-3 text-xs font-mono text-blue-500">
          layout: {emission!.layoutId}
        </p>
      )}

      {specs.length > 0 ? (
        <div class="space-y-3">
          {specs.map((spec, i) => (
            <SpecCard key={spec.componentId ?? `spec-${i}`} spec={spec} index={i} />
          ))}
        </div>
      ) : (
        <p class="text-sm text-gray-400">投影コンポーネントがありません</p>
      )}

      {hasErrors && (
        <p class="mt-3 text-xs text-red-500">
          一部のコンポーネントで投影エラーが発生しています。
        </p>
      )}

      {recommendProjection && (
        <RecommendNavigationIsland spec={recommendProjection} token={projectionToken} />
      )}
    </div>
  );
}
