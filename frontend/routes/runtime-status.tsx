const FLOW_STEPS = [
  { key: "stored_topology_data", label: "格納トポロジーデータ" },
  { key: "user_operation", label: "ユーザー操作" },
  { key: "operation_vector", label: "操作ベクター" },
  { key: "attractor_resolve", label: "アトラクター解決" },
  { key: "structure_map_resolve", label: "構造マップ解決" },
  { key: "package_resolve", label: "パッケージ解決" },
  { key: "schema_resolve", label: "スキーマ解決" },
  { key: "component_expand", label: "コンポーネント展開" },
  { key: "emission_or_projection", label: "emission / 投影" },
];

export default function RuntimeStatus() {
  return (
    <main class="page-main font-mono">
      <h1 class="page-title">topolactor — ランタイム検証ステータス</h1>
      <p class="mb-6 text-muted">
        正規ランタイムフローの静的チェックリストです。
        ライブ検証は <a href="/demo" class="link">/demo</a> のディスパッチ経由で行います。
      </p>

      <section class="mb-8">
        <h2 class="section-title">正規フロー</h2>
        <ul class="space-y-2">
          {FLOW_STEPS.map((step, i) => (
            <li key={step.key} class="text-sm">
              <strong>{step.label}</strong>
              <code class="ml-2 text-muted-xs">({step.key})</code>
              {i < FLOW_STEPS.length - 1 && <span class="text-gray-400"> →</span>}
              <span class="ml-2 text-muted-xs">[静的チェックリスト]</span>
            </li>
          ))}
        </ul>
      </section>

      <section>
        <h2 class="section-title">境界チェック</h2>
        <div class="table-wrap">
          <table class="table">
            <thead>
              <tr>
                <th>境界</th>
                <th>役割</th>
                <th>ステータス</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>DB</td>
                <td>セマンティックトポロジー空間</td>
                <td>DB バックランタイムの正本</td>
              </tr>
              <tr>
                <td>バックエンド</td>
                <td>抽象ランタイム</td>
                <td>正規ランタイム + 明示的エラー（サイレントフォールバックなし）</td>
              </tr>
              <tr>
                <td>フロントエンド</td>
                <td>物理インタラクション投影</td>
                <td>API プロキシ（/api/dispatch → バックエンド）</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </main>
  );
}
