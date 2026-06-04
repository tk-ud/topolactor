import { useState } from "preact/hooks";
import { JSX } from "preact";
import {
  type RegistryVectorValidationResult,
  validateRegistryVector,
} from "../api/adminApi.ts";
import { AdminActionHint } from "../components/AdminHelpPanel.tsx";

const CLASS_BADGE: Record<string, string> = {
  pass: "badge-ok",
  related_existing_registry: "badge-warn",
  near_duplicate_vector: "badge-warn",
  duplicate_vector: "badge-error",
  zero_vector: "badge-info",
  explicit_error: "badge-error",
};

const CLASS_LABEL: Record<string, string> = {
  pass: "合格",
  related_existing_registry: "関連既存レジストリ",
  near_duplicate_vector: "近似重複ベクター",
  duplicate_vector: "重複ベクター",
  zero_vector: "ゼロベクター",
  explicit_error: "明示的エラー",
};

export default function RegistryVectorValidator(): JSX.Element {
  const [registryTable, setRegistryTable] = useState("relation_registry");
  const [queryIdsText, setQueryIdsText] = useState("");
  const [result, setResult] = useState<RegistryVectorValidationResult | null>(null);
  const [notBound, setNotBound] = useState(false);
  const [notAuthed, setNotAuthed] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (notBound) {
    return (
      <p class="text-muted">
        サーバーへの接続が確立されていません。環境の設定を確認し、
        <a href="/super_auth" class="link">ログイン</a> してから再度お試しください。
      </p>
    );
  }

  if (notAuthed) {
    return (
      <p class="text-muted">
        ログインが必要です — <a href="/super_auth" class="link">ログインページ</a> で認証してから再度アクセスしてください。
      </p>
    );
  }

  async function handleValidate(e: Event): Promise<void> {
    e.preventDefault();
    setError(null);
    setResult(null);

    const rawIds = queryIdsText
      .split(/[\n,\s]+/)
      .map((s) => s.trim())
      .filter((s) => s.length > 0);

    if (!registryTable.trim()) {
      setError("registryTable は必須です。");
      return;
    }

    setLoading(true);
    try {
      const res = await validateRegistryVector(registryTable.trim(), rawIds);
      if (res === null) {
        setNotBound(true);
        return;
      }
      setResult(res);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      if (msg.includes("HTTP 401")) {
        setNotAuthed(true);
      } else {
        setError(msg);
      }
    }
    setLoading(false);
  }

  return (
    <div class="max-w-3xl">
      <form onSubmit={handleValidate} class="flex flex-col gap-4">
        <div>
          <label class="mb-1 block text-sm font-medium">
            確認対象のテーブル <span class="text-red-600">*</span>
          </label>
          <input
            class="input-mono max-w-xs"
            type="text"
            placeholder="例: relation_registry"
            value={registryTable}
            onInput={(e) => setRegistryTable((e.target as HTMLInputElement).value)}
            required
          />
        </div>
        <div>
          <label class="mb-1 block text-sm font-medium">
            確認するID — 改行・カンマ・スペース区切りで入力
          </label>
          <textarea
            class="input-mono h-24 resize-y"
            placeholder={"例:\na1b2c3d4-...\ne5f6a7b8-..."}
            value={queryIdsText}
            onInput={(e) => setQueryIdsText((e.target as HTMLTextAreaElement).value)}
          />
        </div>
        <div>
          <button type="submit" disabled={loading} class="btn-primary">
            {loading ? "確認中..." : "重複チェックを実行"}
          </button>
          <AdminActionHint>
            データベースへの書き込みはしません。問題がなければ登録画面へ進んでください。
          </AdminActionHint>
        </div>
      </form>

      {error && (
        <div class="alert-error mt-4 text-sm">
          <p>{error}</p>
          <AdminActionHint>確認対象のテーブルは必須です。IDを正しい形式で入力してください。</AdminActionHint>
        </div>
      )}

      {result && <ValidationResultView result={result} />}
    </div>
  );
}

function ValidationResultView({ result }: { result: RegistryVectorValidationResult }): JSX.Element {
  const badgeClass = CLASS_BADGE[result.validationClass] ?? "badge-info";
  const label = CLASS_LABEL[result.validationClass] ?? result.validationClass;
  const blockingLabel = result.isBlocking ? "ブロッキング" : "非ブロッキング";

  return (
    <div class="mt-5">
      <div class="card border-2">
        <div class={`mb-1 inline-block ${badgeClass}`}>{label}</div>
        <div class="text-sm text-gray-600">
          {blockingLabel}
          {result.statusDetail ? ` — ${result.statusDetail}` : ""}
        </div>
        {result.isBlocking && (
          <AdminActionHint>
            duplicate / near_duplicate — 登録前に query IDs を統合・差別化。pass なら次の登録画面へ。
          </AdminActionHint>
        )}
      </div>

      {result.neighbors.length > 0 && (
        <>
          <h4 class="section-title mt-4">類似する既存登録 ({result.neighbors.length} 件)</h4>
          <div class="table-wrap">
            <table class="table text-xs">
              <thead>
                <tr>
                  {["名前", "類似度", "一致 ID", "理由"].map((h) => (
                    <th key={h}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {result.neighbors.map((n) => (
                  <tr key={n.registryId}>
                    <td>{n.name}</td>
                    <td class="font-mono">{n.cosineScore.toFixed(4)}</td>
                    <td class="text-muted-xs">{n.matchedIds.join(", ") || "—"}</td>
                    <td>{n.reason}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
