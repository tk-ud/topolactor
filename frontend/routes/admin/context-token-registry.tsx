import ContextTokenRegistryEditor from "../../islands/ContextTokenRegistryEditor.tsx";

/**
 * /admin/context-token-registry
 *
 * Admin page for managing context_token_registry — the hub Registry for
 * discrete tokens used as sparse vector components in the recommendation engine.
 *
 * Each token has:
 *   - label: human-readable name
 *   - group: UI grouping tag (not used in computation)
 *   - value: meaning direction component [-1.0, 1.0]
 *   - status: active | deprecated
 *
 * Auto-learning (cache rebuild) and recommendation both read from this registry.
 * Token values are set by human discrete ordering; no neural training required.
 */
export default function ContextTokenRegistryPage() {
  return (
    <main style={{ fontFamily: "monospace", padding: "2rem" }}>
      <h1>context_token_registry — ハブ Registry (トークン管理)</h1>
      <p>
        推薦エンジンが使用する離散トークン辞書です。
        各トークンの <code>value</code> がコサイン類似度ベクトルの意味方向成分になります。
        <br />
        推奨範囲: <code>[-1.0, 1.0]</code>。
        値の割り当ては人間による離散順序付けです（ニューラル訓練不要）。
      </p>
      <p style={{ color: "#555" }}>
        自動学習（キャッシュ再構築）とレコメンドはこのレジストリを参照します。
        操作には <a href="/auth">ログイン</a> が必要です（JWT Bearer）。
        DEMO_BACKEND_URL と DATABASE_URL が設定された環境では DB に永続化されます。
      </p>

      <hr style={{ margin: "16px 0" }} />

      <ContextTokenRegistryEditor />

      <hr style={{ margin: "24px 0" }} />
      <p>
        <a href="/admin">&larr; Admin トップへ</a>
      </p>
    </main>
  );
}
