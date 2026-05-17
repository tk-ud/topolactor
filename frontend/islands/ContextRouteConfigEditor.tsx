import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  type ContextRouteConfig,
  fetchContextRouteConfig,
  saveContextRouteConfig,
} from "../api/adminApi.ts";

/**
 * ContextRouteConfigEditor — admin island for editing context_route_config.
 *
 * All tuning parameters of the recommendation engine are stored in the
 * context_route_config registry table (SSOT).  This island lets operators
 * inspect and update those parameters without a code deployment.
 *
 * When the backend registry endpoint is not yet bound (501), this island
 * shows a "レジストリ未接続" notice rather than displaying hardcoded values.
 */
export default function ContextRouteConfigEditor(): JSX.Element {
  const [config, setConfig] = useState<ContextRouteConfig | null>(null);
  const [notBound, setNotBound] = useState(false);
  const [status, setStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetchContextRouteConfig().then((result) => {
      if (result === null) {
        setNotBound(true);
      } else {
        setConfig(result);
      }
    }).catch((err: unknown) => {
      setStatus(err instanceof Error ? err.message : String(err));
    });
  }, []);

  if (notBound) {
    return (
      <p style={{ fontFamily: "monospace", color: "#888" }}>
        レジストリ未接続 — context_route_config エンドポイントは未実装です (501)。
      </p>
    );
  }

  if (config === null) {
    return <p style={{ color: "#888" }}>読み込み中…</p>;
  }

  async function handleSubmit(e: Event): Promise<void> {
    e.preventDefault();
    if (config === null) return;
    setLoading(true);
    setStatus(null);
    const result = await saveContextRouteConfig(config);
    setStatus(result.message);
    setLoading(false);
  }

  const inputStyle = {
    display: "block",
    width: "180px",
    marginBottom: "12px",
    padding: "4px 6px",
    fontFamily: "monospace",
  };
  const labelStyle = { display: "block", marginBottom: "4px", fontWeight: "bold" as const };

  const field = (
    key: keyof ContextRouteConfig,
    label: string,
    step: number,
    description: string,
  ) => (
    <div key={key} style={{ marginBottom: "16px" }}>
      <label style={labelStyle}>
        {label}
        <input
          style={inputStyle}
          type="number"
          step={step}
          value={config[key]}
          onInput={(e) =>
            setConfig((prev) =>
              prev
                ? { ...prev, [key]: parseFloat((e.target as HTMLInputElement).value) }
                : prev
            )}
        />
      </label>
      <small style={{ color: "#666" }}>{description}</small>
    </div>
  );

  return (
    <div style={{ maxWidth: "640px" }}>
      <form onSubmit={handleSubmit}>
        <fieldset style={{ border: "1px solid #ccc", padding: "16px", marginBottom: "16px" }}>
          <legend style={{ fontWeight: "bold" }}>近傍検索パラメータ</legend>
          {field("minSimilarity", "min_similarity", 0.01,
            "近傍候補の最小コサイン類似度")}
          {field("topK", "top_k", 1,
            "コサイン検索で取得するプレフィックス候補の上限")}
          {field("minNeighbors", "min_neighbors", 1,
            "推薦を出力するために必要な最小近傍数")}
          {field("recentDays", "recent_days", 1,
            "プレフィックス候補の履歴ウィンドウ（日数）")}
        </fieldset>

        <fieldset style={{ border: "1px solid #ccc", padding: "16px", marginBottom: "16px" }}>
          <legend style={{ fontWeight: "bold" }}>スコアリングパラメータ</legend>
          {field("maxCandidatesShown", "max_candidates_shown", 1,
            "出力する推薦候補の最大件数")}
          {field("baselineWeight", "baseline_weight", 0.1,
            "遷移確率ベースラインのスコアリング重み")}
          {field("neighborWeight", "neighbor_weight", 0.1,
            "近傍投票のスコアリング重み")}
        </fieldset>

        <button type="submit" disabled={loading}>
          {loading ? "保存中…" : "設定を保存"}
        </button>
      </form>

      {status && (
        <p
          style={{
            marginTop: "12px",
            padding: "8px",
            background: "#f0f0f0",
            fontFamily: "monospace",
            fontSize: "0.85rem",
          }}
        >
          {status}
        </p>
      )}
    </div>
  );
}
