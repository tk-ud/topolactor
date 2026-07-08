const FLOW_STEPS = [
  { key: "stored_topology_data", label: "保存済みの設定を読み込む" },
  { key: "user_operation", label: "ユーザー操作を受け付ける" },
  { key: "operation_vector", label: "操作内容を整理する" },
  { key: "attractor_resolve", label: "表示先を決める" },
  { key: "structure_map_resolve", label: "画面構成を決める" },
  { key: "package_resolve", label: "必要な部品セットを選ぶ" },
  { key: "schema_resolve", label: "表示項目を選ぶ" },
  { key: "component_expand", label: "画面部品を展開する" },
  { key: "emission_or_projection", label: "画面に表示する" },
];

export default function RuntimeStatus() {
  return (
    <main class="page-main font-sans">
      <h1 class="page-title">topolactor — 接続状態</h1>
      <p class="mb-6 text-muted">
        操作してから画面が表示されるまでの流れを確認できます。
        実際の表示と操作は <a href="/" class="link">トップ</a>{" "}
        で確認してください。
      </p>
      <section class="mb-8">
        <h2 class="section-title">画面が表示されるまで</h2>
        <ol class="list-decimal space-y-2 pl-5">
          {FLOW_STEPS.map((step) => (
            <li key={step.key} class="text-sm">{step.label}</li>
          ))}
        </ol>
      </section>
      <details class="rounded border border-gray-200 bg-gray-50 p-4 text-sm">
        <summary class="cursor-pointer font-semibold">
          開発者向け情報 — 内部境界と識別子
        </summary>
        <ul class="mt-3 space-y-2 font-mono text-xs">
          {FLOW_STEPS.map((step) => (
            <li key={step.key}>
              <strong>{step.label}</strong> <code>({step.key})</code>
            </li>
          ))}
        </ul>
        <div class="table-wrap mt-4">
          <table class="table">
            <thead>
              <tr>
                <th>境界</th>
                <th>役割</th>
                <th>状態</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>DB</td>
                <td>semantic topology space</td>
                <td>DB-backed runtime SSOT</td>
              </tr>
              <tr>
                <td>backend</td>
                <td>abstract runtime</td>
                <td>canonical runtime + explicit errors</td>
              </tr>
              <tr>
                <td>frontend</td>
                <td>physical interaction projection</td>
                <td>API proxy (/api/dispatch → backend)</td>
              </tr>
            </tbody>
          </table>
        </div>
      </details>
    </main>
  );
}
