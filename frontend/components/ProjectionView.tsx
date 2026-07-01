import { JSX } from "preact";
import type { Emission } from "../api/dispatch.ts";
import type { StructureMapEntry } from "../structure_map.ts";
import { projectionFromEmission } from "../runtime/renderEmission.ts";

type Props = {
  emission: Emission;
  structureMap?: StructureMapEntry;
};

/**
 * ProjectionView — emission_or_projection ステップの出力を表示する。
 *
 * 純粋な表示コンポーネント: Emission と任意の StructureMapEntry を受け取り、
 * パイプラインロジックは実行しない。
 */
export function ProjectionView({ emission, structureMap }: Props): JSX.Element {
  const hasErrors = Array.isArray(emission.errors) && emission.errors.length > 0;
  const projectionResult = emission.projectionDefinition
    ? projectionFromEmission(emission, emission.projectionDefinition)
    : null;

  return (
    <div class="space-y-4 text-sm">
      {hasErrors && (
        <div class="alert-error">
          <strong>Emission エラー:</strong>
          <ul class="mt-2 list-inside list-disc">
            {emission.errors!.map((err, i) => (
              <li key={i} class="text-red-700">
                {err}
              </li>
            ))}
          </ul>
        </div>
      )}

      {structureMap && (
        <div class="card">
          <h3 class="mb-3 font-semibold">解決済み StructureMap エントリ</h3>
          <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 font-mono text-xs">
            <dt class="font-semibold text-gray-600">attractorKey</dt>
            <dd><code>{structureMap.attractorKey}</code></dd>
            <dt class="font-semibold text-gray-600">packageId</dt>
            <dd><code>{structureMap.packageId}</code></dd>
            <dt class="font-semibold text-gray-600">schemaId</dt>
            <dd><code>{structureMap.schemaId}</code></dd>
            <dt class="font-semibold text-gray-600">componentIds</dt>
            <dd><code>{structureMap.componentIds.join(", ")}</code></dd>
          </dl>
        </div>
      )}

      <div class="card">
        <h3 class="mb-3 font-semibold">コンポーネント projection</h3>
        {(emission.componentIds ?? []).length === 0 ? (
          <p class="text-muted">
            <em>Emission に componentIds がありません。</em>
          </p>
        ) : (
          <ul class="list-inside list-disc font-mono text-xs">
            {(emission.componentIds ?? []).map((id) => (
              <li key={id}>
                <code>{id}</code>
              </li>
            ))}
          </ul>
        )}
      </div>


      {projectionResult?.projection && (
        <div class="card" data-projection-definition="consumed">
          <h3 class="mb-3 font-semibold">ProjectionDefinition projection</h3>
          <pre class="pre-box">{JSON.stringify(projectionResult.projection, null, 2)}</pre>
        </div>
      )}

      {projectionResult?.error && (
        <div class="alert-error" data-projection-definition="error">
          <strong>ProjectionDefinition エラー:</strong> {projectionResult.error}
        </div>
      )}

      {emission.data && Object.keys(emission.data).length > 0 && (
        <div class="card">
          <h3 class="mb-3 font-semibold">Emission data</h3>
          <pre class="pre-box">{JSON.stringify(emission.data, null, 2)}</pre>
        </div>
      )}
    </div>
  );
}
