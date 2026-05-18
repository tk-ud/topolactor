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

      <section style={{ marginBottom: "24px" }}>
        <h2>Context Route Registry (推薦エンジン)</h2>
        <ul>
          <li>
            <a href="/admin/context-token-registry">
              context_token_registry — ハブ Registry（トークン管理）
            </a>
            <br />
            <small style={{ color: "#666" }}>
              自動学習・レコメンドが参照する離散トークン辞書（value: [-1.0, 1.0]）
            </small>
          </li>
          <li style={{ marginTop: "8px" }}>
            <a href="/admin/registry-vector-validate">
              registry-vector-validate — Registrar ベクター検証
            </a>
            <br />
            <small style={{ color: "#666" }}>
              候補 Registry ID 配列のマルチホットコサイン類似度検証（重複・近似重複・関連レジストリ検出）
            </small>
          </li>
          <li style={{ marginTop: "8px" }}>
            <small style={{ color: "#888" }}>
              推薦エンジン設定（min_similarity / top_k 等）は
              function_parameters（topology データストア）に格納されます。
              独立した設定UIはありません。
            </small>
          </li>
        </ul>
      </section>

      <section>
        <h2>Structure Map</h2>
        <table cellPadding="6">
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
