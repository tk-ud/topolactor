import { useEffect, useState } from "preact/hooks";
import { h, type JSX } from "preact";
import { probeSessionToken, refreshUserSession } from "../api/authApi.ts";
import { clearSessionToken, persistSessionToken, readClientSessionToken } from "../lib/demoSession.ts";
import { queueClientCommand, startComponentEventRuntime } from "../runtime/frontendScheduler.ts";
import { renderEmission, type ComponentSpec } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import type { Emission } from "../api/dispatch.ts";

/** Builds a map from nodeId → children (sorted by orderIndex), for tree rendering. */
function buildChildrenMap(specs: ComponentSpec[]): Map<string | undefined, ComponentSpec[]> {
  const map = new Map<string | undefined, ComponentSpec[]>();
  for (const spec of specs) {
    const key = spec.parentNodeId ?? undefined;
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(spec);
  }
  for (const children of map.values()) {
    children.sort((a, b) => (a.orderIndex ?? 0) - (b.orderIndex ?? 0));
  }
  return map;
}

/** Recursively renders a single layout node as a DOM element with its children. */
function LayoutNode(
  { spec, childrenMap }: { spec: ComponentSpec; childrenMap: Map<string | undefined, ComponentSpec[]> },
): JSX.Element {
  const children = childrenMap.get(spec.nodeId) ?? [];

  const style: Record<string, string> = {};
  if (spec.x !== undefined || spec.y !== undefined || spec.width !== undefined || spec.height !== undefined) {
    style.position = "relative";
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
 * using layout-aware renderEmission(). No static preset wizard.
 */
export default function ProjectionShell(): JSX.Element {
  const [loading, setLoading] = useState(true);
  const [emission, setEmission] = useState<Emission | null>(null);
  const [specs, setSpecs] = useState<ComponentSpec[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [authFallback, setAuthFallback] = useState(false);

  useEffect(() => {
    startComponentEventRuntime();

    (async () => {
      let token = readClientSessionToken();
      if (!token || !(await probeSessionToken(token, "user"))) {
        const refreshed = await refreshUserSession();
        if (refreshed.success && refreshed.token) {
          persistSessionToken(refreshed.token);
          token = refreshed.token;
        } else {
          clearSessionToken();
          setAuthFallback(true);
          setLoading(false);
          return;
        }
      }

      const response = await queueClientCommand(
        { operationType: "Search", target: "default", layer: "entity", action: "Search" },
        token ?? undefined,
        {},
      );

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
    })();
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

  if (hasLayout && hasTreeNodes && !hasErrors) {
    const childrenMap = buildChildrenMap(specs);
    const rootSpecs = childrenMap.get(undefined) ?? [];

    return (
      <div>
        <p class="mb-3 text-xs font-mono text-blue-500">
          layout: {emission!.layoutId}
        </p>
        <div class="relative">
          {rootSpecs.map((spec) => (
            <LayoutNode
              key={spec.nodeId ?? spec.slotKey ?? spec.componentId}
              spec={spec}
              childrenMap={childrenMap}
            />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div>
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
    </div>
  );
}
