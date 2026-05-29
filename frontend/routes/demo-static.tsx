import { JSX } from "preact";
import { resolveOperationVector } from "../runtime/resolveOperationVector.ts";
import { lookupStructureMap, defaultStructureMap } from "../structure_map.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { ProjectionView } from "../components/ProjectionView.tsx";
import { ContextTokenBadgeList, type ContextToken } from "../components/ContextTokenBadgeList.tsx";
import type { Emission } from "../api/dispatch.ts";

const demoTokens: ContextToken[] = [
  { tokenId: "00000000-0000-0000-0000-000000000021", label: "active", group: "status", value: 1.0, status: "active" },
  { tokenId: "00000000-0000-0000-0000-000000000022", label: "warning", group: "status", value: 0.0, status: "active" },
  { tokenId: "00000000-0000-0000-0000-000000000023", label: "critical", group: "status", value: -1.0, status: "active" },
];

/**
 * /demo-static — フロントエンド側正規フローの静的構造図。
 */
export default function DemoStatic(): JSX.Element {
  const demoOperation = {
    operationType: "Search" as const,
    target: "demo",
    layer: "hub",
    action: "overview",
  };

  const demoVector = resolveOperationVector(demoOperation);
  const demoMapEntry = lookupStructureMap(defaultStructureMap, demoVector.attractorKey);

  const demoEmission: Emission = {
    packageId: demoMapEntry?.packageId,
    schemaId: demoMapEntry?.schemaId,
    componentIds: demoMapEntry?.componentIds ?? [],
    data: { note: "静的構造図 — フロントエンド側解決のみ。ランタイム結果ではありません。" },
  };

  const componentSpecs = renderEmission(demoEmission, defaultComponentRegistry);

  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">topolactor — 静的構造図</h1>

      <div class="alert-warn mb-6">
        <strong>静的図（ランタイムではない）:</strong> フロントエンド側の正規フローのみを実行します。
        バックエンド API は呼び出されません。DB データも読みません。
        出力は <code class="rounded bg-yellow-100 px-1">defaultStructureMap</code> と{" "}
        <code class="rounded bg-yellow-100 px-1">defaultComponentRegistry</code> の内容を反映します。
        バックエンドランタイム emission は <a href="/demo" class="link">/demo</a> を使用してください。
      </div>

      <h2 class="section-title">フロントエンド側正規フロー</h2>
      <p class="mb-3 text-muted">
        <code class="rounded bg-gray-100 px-1">
          UserOperation → resolveOperationVector → attractorKey → lookupStructureMap → Emission → renderEmission → ComponentSpec[]
        </code>
      </p>

      <details open class="mb-4">
        <summary class="cursor-pointer font-semibold">OperationVector</summary>
        <pre class="pre-box mt-2">{JSON.stringify(demoVector, null, 2)}</pre>
      </details>

      <ProjectionView emission={demoEmission} structureMap={demoMapEntry ?? undefined} />

      <h3 class="mt-6 mb-2 font-semibold">展開済み ComponentSpecs</h3>
      {componentSpecs.length === 0 ? (
        <p class="text-muted">コンポーネントは解決されませんでした</p>
      ) : (
        <ul class="list-inside list-disc text-sm">
          {componentSpecs.map((spec) => (
            <li key={spec.componentId}>
              <code>{spec.componentId}</code> — <em>{spec.componentType}</em>
              {spec.componentType === "error" && (
                <span class="text-red-600"> — {String(spec.def.error)}</span>
              )}
            </li>
          ))}
        </ul>
      )}

      <hr class="my-6 border-gray-200" />

      <h2 class="section-title">コンテキストトークンレジストリ（シード参照値）</h2>
      <p class="mb-4 text-muted">
        これらの値は <code class="rounded bg-gray-100 px-1">db/demo_seed.sql</code> を反映した静的表示です（ランタイム解決ではありません）。
      </p>
      <ContextTokenBadgeList tokens={demoTokens} activeOnly />

      <div class="nav-footer">
        <a href="/demo" class="link">ランタイムデモ</a>
        {" · "}
        <a href="/admin" class="link">管理画面</a>
        {" · "}
        <a href="/runtime-status" class="link">ランタイムステータス</a>
        {" · "}
        <a href="/" class="link">トップ</a>
      </div>
    </main>
  );
}
