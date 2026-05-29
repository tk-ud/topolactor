import { useState } from "preact/hooks";
import { JSX } from "preact";
import AdminHowTo from "../components/AdminHowTo.tsx";
import AdminHelpPanel, { AdminActionHint } from "../components/AdminHelpPanel.tsx";
import { ADMIN_SEED_GUIDE } from "../content/adminGuides.ts";

const SESSION_TOKEN_KEY = "demo_jwt_token";

function getAuthHeaders(): Record<string, string> {
  const token =
    typeof globalThis.sessionStorage !== "undefined"
      ? sessionStorage.getItem(SESSION_TOKEN_KEY)
      : null;
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (token) headers["Authorization"] = `Bearer ${token}`;
  return headers;
}

async function dispatchSeedOp(layer: string, action: string, payload?: unknown) {
  const res = await fetch("/api/dispatch", {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify({
      operationType: "admin",
      target: "admin",
      layer,
      action,
      payload: payload ?? null,
    }),
  });
  return await res.json();
}

type SeedValidationError = { code: string; message: string };
type SeedRuntime = { name: string; target: string; layer: string; action: string };

export default function SeedAdmin(): JSX.Element {
  const [seedContent, setSeedContent] = useState<string>("");
  const [loadedContent, setLoadedContent] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [errors, setErrors] = useState<SeedValidationError[]>([]);
  const [previewRuntimes, setPreviewRuntimes] = useState<SeedRuntime[]>([]);
  const [loading, setLoading] = useState(false);

  const clearState = () => {
    setStatus(null);
    setErrors([]);
    setPreviewRuntimes([]);
  };

  const handleLoad = async () => {
    clearState();
    setLoading(true);
    try {
      const body = await dispatchSeedOp("seed_runtime", "load");
      if (body?.emission?.data?.content) {
        setLoadedContent(body.emission.data.content);
        setStatus("/storage から seed.json をロードしました。");
      } else if (body?.errors?.length) {
        setErrors(body.errors);
        setStatus("ロードに失敗しました。");
      } else {
        setStatus("seed.json が見つからないか、バックエンドが未設定です。");
      }
    } catch (e) {
      setStatus(`エラー: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    clearState();
    if (!seedContent.trim()) {
      setStatus("内容が空です。保存前に seed JSON を入力してください。");
      return;
    }
    setLoading(true);
    try {
      const body = await dispatchSeedOp("seed_runtime", "save", { content: seedContent });
      if (body?.success) {
        setStatus("/storage に seed.json を保存しました。");
      } else {
        setErrors(body?.errors ?? []);
        setStatus("保存に失敗しました。");
      }
    } catch (e) {
      setStatus(`エラー: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handleValidate = async () => {
    clearState();
    setLoading(true);
    try {
      const body = await dispatchSeedOp("seed_runtime", "validate");
      if (body?.emission?.data?.valid) {
        setStatus("バリデーション成功。seed.json は構造的に有効です。");
      } else {
        setErrors(body?.emission?.data?.errors ?? body?.errors ?? []);
        setStatus("バリデーションに失敗しました。");
      }
    } catch (e) {
      setStatus(`エラー: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handlePreview = async () => {
    clearState();
    setLoading(true);
    try {
      const body = await dispatchSeedOp("seed_runtime", "preview");
      const data = body?.emission?.data;
      if (data?.runtimes) {
        setPreviewRuntimes(data.runtimes);
        setStatus(`プレビュー: ${data.runtimeCount} 件のランタイムが宣言されています。`);
      } else {
        setErrors(body?.errors ?? []);
        setStatus("プレビューに失敗しました。");
      }
    } catch (e) {
      setStatus(`エラー: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handleImport = async () => {
    clearState();
    setLoading(true);
    try {
      const body = await dispatchSeedOp("seed_runtime", "import");
      if (body?.errors?.length) {
        setErrors(body.errors);
        setStatus("インポートに失敗しました。");
      } else if (body?.emission?.data?.importedCount !== undefined) {
        setStatus(`インポート成功: ${body.emission.data.importedCount} 件のランタイム宣言を適用しました。`);
      } else {
        setStatus("インポートが完了しました。");
      }
    } catch (e) {
      setStatus(`エラー: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <main class="page-main font-mono">
      <h1 class="page-title">topolactor — 管理 / シード</h1>
      <p class="mb-4"><a href="/admin" class="link">&larr; 管理インデックス</a></p>

      <AdminHowTo
        steps={ADMIN_SEED_GUIDE.howToSteps}
        prerequisites={ADMIN_SEED_GUIDE.prerequisites}
      />
      <AdminHelpPanel {...ADMIN_SEED_GUIDE} />

      <hr class="mb-6 border-gray-200" />

      <section class="mb-6">
        <h2 class="section-title">Seed JSON エディター</h2>
        <textarea
          value={seedContent}
          onInput={(e) => setSeedContent((e.target as HTMLTextAreaElement).value)}
          rows={12}
          placeholder={`{\n  "version": "1",\n  "runtimes": [\n    { "name": "example", "target": "default", "layer": "entity", "action": "search" }\n  ]\n}`}
          class="input-mono mb-3 w-full"
        />
        <div class="flex flex-wrap gap-2">
          <div>
            <button onClick={handleSave} disabled={loading} class="btn-secondary">/storage に保存</button>
            <AdminActionHint>ディスク上の seed.json のみ。DB・runtime は未変更。</AdminActionHint>
          </div>
          <div>
            <button onClick={handleLoad} disabled={loading} class="btn-secondary">/storage からロード</button>
            <AdminActionHint>既存ファイルを読み取り表示。エディターへコピー可能。</AdminActionHint>
          </div>
          <div>
            <button onClick={handleValidate} disabled={loading} class="btn-secondary">バリデート</button>
            <AdminActionHint>構造・必須フィールドを backend 検証。永続化なし。</AdminActionHint>
          </div>
          <div>
            <button onClick={handlePreview} disabled={loading} class="btn-secondary">プレビュー</button>
            <AdminActionHint>宣言 runtime 一覧を read-only 表示。DB 未反映。</AdminActionHint>
          </div>
          <div>
            <button onClick={handleImport} disabled={loading} class="btn-primary">インポート</button>
            <AdminActionHint>validate 後の想定で canonical DB へ反映。先にプレビュー推奨。</AdminActionHint>
          </div>
        </div>
      </section>

      {loading && <p class="text-muted">処理中...</p>}

      {status && (
        <p class={`font-semibold ${errors.length > 0 ? "text-red-600" : "text-green-700"}`}>{status}</p>
      )}

      {errors.length > 0 && (
        <section class="alert-error mt-4">
          <h3 class="mb-2 font-semibold">エラー</h3>
          <ul class="list-inside list-disc text-sm">
            {errors.map((e, i) => (
              <li key={i}><code>{e.code}</code>: {e.message}</li>
            ))}
          </ul>
          <p class="text-muted-xs mt-2">
            JSON 構文・必須フィールドはエディター修正 → 再バリデート。storage 未作成は先に保存。
            import 失敗時はプレビュー表と backend ログで重複・ref を確認。
          </p>
        </section>
      )}

      {loadedContent && (
        <section class="mt-4">
          <h3 class="mb-2 font-semibold">ロード済みコンテンツ</h3>
          <pre class="pre-box mb-2">{loadedContent}</pre>
          <button onClick={() => setSeedContent(loadedContent)} class="btn-secondary text-sm">エディターにコピー</button>
        </section>
      )}

      {previewRuntimes.length > 0 && (
        <section class="mt-4">
          <h3 class="mb-2 font-semibold">プレビュー — 宣言済みランタイム</h3>
          <div class="table-wrap">
            <table class="table">
              <thead>
                <tr>
                  {["名前", "ターゲット", "レイヤー", "アクション"].map((h) => (
                    <th key={h}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {previewRuntimes.map((r, i) => (
                  <tr key={i}>
                    <td>{r.name}</td>
                    <td>{r.target}</td>
                    <td>{r.layer}</td>
                    <td>{r.action}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </main>
  );
}
