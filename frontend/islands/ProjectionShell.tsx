import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import { queueClientCommand, startComponentEventRuntime } from "../runtime/frontendScheduler.ts";
import { renderEmission, type ComponentSpec } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import type { Emission } from "../api/dispatch.ts";

const SESSION_TOKEN_KEY = "demo_jwt_token";

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

  useEffect(() => {
    startComponentEventRuntime();
    const token = sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined;

    (async () => {
      const response = await queueClientCommand(
        { operationType: "Search", target: "default", layer: "entity", action: "Search" },
        token,
        {},
      );

      if (response.emission) {
        const em = response.emission;
        setEmission(em);
        setSpecs(renderEmission(em, defaultComponentRegistry));
      } else {
        const msg =
          response.errors?.[0]?.Message ??
          response.errors?.[0]?.message ??
          "投影の取得に失敗しました";
        setError(msg);
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
