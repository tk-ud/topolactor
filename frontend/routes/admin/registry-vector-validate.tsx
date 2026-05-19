import RegistryVectorValidator from "../../islands/RegistryVectorValidator.tsx";

/**
 * Admin page: Registry vector validation
 *
 * Validates a candidate registry ID array against existing registry rows
 * using multi-hot cosine similarity. Policy is driven by function_parameters
 * (topology_vector_runtime.registry_validation). Policy missing → explicit error.
 *
 * Route: GET /admin/registry-vector-validate
 */
export default function RegistryVectorValidatePage() {
  return (
    <main style={{ fontFamily: "monospace", padding: "2rem" }}>
      <h1>レジストリベクター検証</h1>
      <p style={{ color: "#555", marginBottom: "1.5rem" }}>
        候補 Registry ID 配列をマルチホットコサイン類似度で既存レジストリと比較し、
        重複・近似重複・関連レジストリを検出します。
        ポリシーは <code>function_parameters</code>（<code>topology_vector_runtime.registry_validation</code>）から読み込まれます。
      </p>

      <section style={{ marginBottom: "16px" }}>
        <a href="/admin" style={{ fontSize: "0.85rem", color: "#555" }}>
          ← admin トップ
        </a>
      </section>

      <RegistryVectorValidator />
    </main>
  );
}
