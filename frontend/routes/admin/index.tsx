import { defaultPackage } from "../../package/defaultPackage.ts";
import { defaultSchema } from "../../schema/defaultSchema.ts";
import { defaultStructureMap } from "../../structure_map.ts";

export default function AdminIndex() {
  const structureMapEntries = Object.values(defaultStructureMap);

  return (
    <main style={{ fontFamily: "monospace", padding: "2rem" }}>
      <h1>topolactor — admin topology inspection</h1>
      <p>
        This page shows the current default topology skeleton. Real admin UI is
        out of scope for this skeleton.
      </p>

      <section>
        <h2>Structure Map</h2>
        <table border={1} cellPadding={6}>
          <thead>
            <tr>
              <th>attractorKey</th>
              <th>packageId</th>
              <th>schemaId</th>
              <th>componentIds</th>
            </tr>
          </thead>
          <tbody>
            {structureMapEntries.map((entry) => (
              <tr key={entry.attractorKey}>
                <td>{entry.attractorKey}</td>
                <td>{entry.packageId}</td>
                <td>{entry.schemaId}</td>
                <td>{entry.componentIds.join(", ")}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section>
        <h2>Default Package</h2>
        <dl>
          <dt>packageId</dt>
          <dd>{defaultPackage.packageId}</dd>
          <dt>name</dt>
          <dd>{defaultPackage.name}</dd>
          <dt>componentIds</dt>
          <dd>{defaultPackage.componentIds.join(", ")}</dd>
        </dl>
      </section>

      <section>
        <h2>Default Schema</h2>
        <dl>
          <dt>schemaId</dt>
          <dd>{defaultSchema.schemaId}</dd>
          <dt>name</dt>
          <dd>{defaultSchema.name}</dd>
          <dt>fields</dt>
          <dd>
            <ul>
              {defaultSchema.fields.map((f) => (
                <li key={f.key}>
                  {f.key} ({f.type}) — {f.label}
                  {f.required ? " *" : ""}
                </li>
              ))}
            </ul>
          </dd>
        </dl>
      </section>
    </main>
  );
}
