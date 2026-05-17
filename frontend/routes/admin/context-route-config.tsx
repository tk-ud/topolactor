import ContextRouteConfigEditor from "../../islands/ContextRouteConfigEditor.tsx";

/**
 * /admin/context-route-config
 *
 * Admin page for editing the context_route_config registry — the SSOT for all
 * tuning parameters of the context route recommendation engine.
 *
 * Values are loaded from and saved to context_route_config via the API route
 * /api/admin/context-route-config.  In-memory skeleton: not persisted to DB.
 */
export default function ContextRouteConfigPage() {
  return (
    <main style={{ fontFamily: "monospace", padding: "2rem" }}>
      <h1>context_route_config — 推薦エンジン設定レジストリ</h1>
      <p>
        コンテキストルート推薦エンジンのすべてのチューニングパラメータは{" "}
        <code>context_route_config</code> テーブル（DB Registry）に保存されます。
        Runtime コードへの定数直書きはありません。
      </p>
      <p style={{ color: "#888" }}>
        変更は <code>/api/admin/context-route-config</code> 経由で{" "}
        <code>ContextRouteConfigRepository</code> に保存されます。
        スケルトンモード: 受付のみ、DB への永続化なし。
      </p>

      <hr style={{ margin: "16px 0" }} />

      <ContextRouteConfigEditor />

      <hr style={{ margin: "24px 0" }} />
      <p>
        <a href="/admin">&larr; Admin トップへ</a>
        {" | "}
        <a href="/admin/context-token-registry">context_token_registry (トークン管理) &rarr;</a>
      </p>
    </main>
  );
}
