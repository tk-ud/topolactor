import { useState } from "preact/hooks";
import { JSX } from "preact";
import { AdminActionHint } from "../components/AdminHelpPanel.tsx";
import { queueAdminClientCommand } from "../runtime/frontendScheduler.ts";

const SESSION_TOKEN_KEY = "demo_jwt_token";

// deno-lint-ignore no-explicit-any
async function dispatchSeedOp(layer: string, action: string, payload?: unknown): Promise<any> {
  const token = typeof globalThis.sessionStorage !== "undefined"
    ? sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined : undefined;
  return queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer,
    action,
    payload: payload != null ? payload as Record<string, unknown> : undefined,
  }, token);
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
      <h1 class="page-title">topolactor — 管理 / 初期データ設定</h1>
      <p class="mb-4"><a href="/admin" class="link">&larr; 管理インデックス</a></p>

      <hr class="mb-6 border-gray-200" />

      <section class="mb-6">
        <h2 class="section-title">初期データ エディター</h2>
        <textarea
          value={seedContent}
          onInput={(e) => setSeedContent((e.target as HTMLTextAreaElement).value)}
          rows={12}
          placeholder={`{\n  "version": "1",\n  "runtimes": [\n    { "name": "example", "target": "default", "layer": "entity", "action": "search" }\n  ]\n}`}
          class="input-mono mb-3 w-full"
        />
        <div class="flex flex-wrap gap-2">
          <div>
            <button onClick={handleSave} disabled={loading} class="btn-secondary">一時保存（DBに反映しない）</button>
            <AdminActionHint>ファイルとして保存するだけです。データベースは変更されません。</AdminActionHint>
          </div>
          <div>
            <button onClick={handleLoad} disabled={loading} class="btn-secondary">保存済みデータを読み込む</button>
            <AdminActionHint>保存済みのデータをエディターに読み込みます。</AdminActionHint>
          </div>
          <div>
            <button onClick={handleValidate} disabled={loading} class="btn-secondary">内容を確認</button>
            <AdminActionHint>構造と必須項目をサーバーで検証します。データベースは変更されません。</AdminActionHint>
          </div>
          <div>
            <button onClick={handlePreview} disabled={loading} class="btn-secondary">プレビュー</button>
            <AdminActionHint>設定される動作の一覧を表示します。データベースは変更されません。</AdminActionHint>
          </div>
          <div>
            <button onClick={handleImport} disabled={loading} class="btn-primary">取り込む</button>
            <AdminActionHint>内容確認・プレビュー後に実行してください。データベースに反映されます。</AdminActionHint>
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
            JSONの書式・必須項目はエディターを修正して再度「内容を確認」してください。
            先にファイルを保存してから読み込み・取り込みを行ってください。
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
          <h3 class="mb-2 font-semibold">プレビュー — 設定済み動作一覧</h3>
          <div class="table-wrap">
            <table class="table">
              <thead>
                <tr>
                  {["名前", "対象", "層", "操作"].map((h) => (
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
