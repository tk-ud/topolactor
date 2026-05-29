import { JSX } from "preact";
import type { ContextRouteRecommendation, Emission } from "../api/dispatch.ts";
import { validationErrorText } from "../api/dispatch.ts";

type Props = {
  emission: Emission;
};

/**
 * EmissionView — バックエンド Emission の全フィールドを表示する。
 * 意図的にプレーンで、ビジネスロジックは持たない。
 */
export function EmissionView({ emission }: Props): JSX.Element {
  const errorLines = emission.errors?.map(validationErrorText).join("\n");

  return (
    <div class="font-mono text-sm">
      <h4 class="mb-2 font-semibold">Emission</h4>
      <div class="table-wrap">
        <table class="table">
          <tbody>
            <Row label="structureMapId" value={emission.structureMapId} />
            <Row label="packageId" value={emission.packageId} />
            <Row label="schemaId" value={emission.schemaId} />
            <Row
              label="componentIds"
              value={
                emission.componentIds ? emission.componentIds.join(", ") : undefined
              }
            />
            <Row
              label="data"
              value={
                emission.data ? JSON.stringify(emission.data, null, 2) : undefined
              }
              pre
            />
            <Row
              label="errors"
              value={errorLines}
              error={Boolean(emission.errors && emission.errors.length > 0)}
            />
            {emission.contextRouteRecommendation && (
              <RecommendationSection rec={emission.contextRouteRecommendation} />
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

type RowProps = {
  label: string;
  value: string | undefined;
  pre?: boolean;
  error?: boolean;
};

type RecommendationSectionProps = { rec: ContextRouteRecommendation };

function RecommendationSection({ rec }: RecommendationSectionProps): JSX.Element {
  return (
    <>
      <tr>
        <td class="font-bold" colSpan={2}>
          context_route_recommendation
        </td>
      </tr>
      <tr>
        <td class="whitespace-nowrap font-bold">status</td>
        <td>
          {rec.status}{rec.statusDetail ? ` — ${rec.statusDetail}` : ""}
        </td>
      </tr>
      <tr>
        <td class="whitespace-nowrap align-top font-bold">next_operations</td>
        <td>
          {rec.nextOperations.length === 0
            ? <em class="text-gray-400">—</em>
            : <pre class="m-0 whitespace-pre-wrap">{JSON.stringify(rec.nextOperations, null, 2)}</pre>}
        </td>
      </tr>
      <tr>
        <td class="whitespace-nowrap align-top font-bold">next_tokens</td>
        <td>
          {rec.nextTokens.length === 0
            ? <em class="text-gray-400">—</em>
            : <pre class="m-0 whitespace-pre-wrap">{JSON.stringify(rec.nextTokens, null, 2)}</pre>}
        </td>
      </tr>
    </>
  );
}

function Row({ label, value, pre, error }: RowProps): JSX.Element {
  return (
    <tr>
      <td class={`whitespace-nowrap font-bold align-top ${error ? "text-red-700" : ""}`}>
        {label}
      </td>
      <td class={error ? "text-red-700" : ""}>
        {value === undefined ? (
          <em class="text-gray-400">—</em>
        ) : pre ? (
          <pre class="m-0 whitespace-pre-wrap">{value}</pre>
        ) : (
          value
        )}
      </td>
    </tr>
  );
}
