import { JSX } from "preact";
import type { Emission } from "../api/dispatch.ts";
import type { StructureMapEntry } from "../structure_map.ts";

type Props = {
  emission: Emission;
  structureMap?: StructureMapEntry;
};

/**
 * ProjectionView renders the output of the emission_or_projection step.
 *
 * It is a pure display component: it accepts an Emission and an optional
 * resolved StructureMapEntry and renders them without performing any pipeline
 * logic itself.
 */
export function ProjectionView({ emission, structureMap }: Props): JSX.Element {
  const hasErrors = Array.isArray(emission.errors) && emission.errors.length > 0;

  return (
    <div class="projection-view">
      {hasErrors && (
        <div class="projection-errors">
          <strong>Emission errors:</strong>
          <ul>
            {emission.errors!.map((err, i) => (
              <li key={i} style={{ color: "crimson" }}>
                {err}
              </li>
            ))}
          </ul>
        </div>
      )}

      {structureMap && (
        <div class="projection-structure-map">
          <h3>Resolved StructureMap entry</h3>
          <dl>
            <dt>attractorKey</dt>
            <dd>
              <code>{structureMap.attractorKey}</code>
            </dd>
            <dt>packageId</dt>
            <dd>
              <code>{structureMap.packageId}</code>
            </dd>
            <dt>schemaId</dt>
            <dd>
              <code>{structureMap.schemaId}</code>
            </dd>
            <dt>componentIds</dt>
            <dd>
              <code>{structureMap.componentIds.join(", ")}</code>
            </dd>
          </dl>
        </div>
      )}

      <div class="projection-components">
        <h3>Component projections</h3>
        {(emission.componentIds ?? []).length === 0 ? (
          <p>
            <em>No componentIds in emission.</em>
          </p>
        ) : (
          <ul>
            {(emission.componentIds ?? []).map((id) => (
              <li key={id}>
                <code>{id}</code>
              </li>
            ))}
          </ul>
        )}
      </div>

      {emission.data && Object.keys(emission.data).length > 0 && (
        <div class="projection-data">
          <h3>Emission data</h3>
          <pre>{JSON.stringify(emission.data, null, 2)}</pre>
        </div>
      )}
    </div>
  );
}
