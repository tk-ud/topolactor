import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  listImportManifests,
  listImportSchemas,
  uploadImportPreview,
  applyImport,
  type AdminImportManifestItem,
  type AdminImportSchemaItem,
  type AdminImportPreviewResult,
  type AdminImportApplyResult,
} from "../api/adminApi.ts";
import AdminHowTo from "../components/AdminHowTo.tsx";
import AdminHelpPanel, { AdminActionHint } from "../components/AdminHelpPanel.tsx";
import { ADMIN_IMPORT_GUIDE } from "../content/adminGuides.ts";
import {
  UX_DATA_SHAPE,
  UX_IMPORT_SETTINGS,
  UX_IMPORT_SETTINGS_PAGE,
  UX_RUNTIME_CHECK,
  UX_UI_BUILDER,
} from "../content/adminUxTerms.ts";

export default function AdminImport(): JSX.Element {
  const [manifests, setManifests] = useState<AdminImportManifestItem[]>([]);
  const [schemas, setSchemas] = useState<AdminImportSchemaItem[]>([]);
  const [selectedManifestId, setSelectedManifestId] = useState<string>("");
  const [selectedSchemaId, setSelectedSchemaId] = useState<string>("");
  const [sourceType, setSourceType] = useState<"csv" | "json">("csv");
  const [fileName, setFileName] = useState<string>("");
  const [fileContent, setFileContent] = useState<string>("");
  const [preview, setPreview] = useState<AdminImportPreviewResult | null>(null);
  const [applyResult, setApplyResult] = useState<AdminImportApplyResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [loadingSelectors, setLoadingSelectors] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const [m, s] = await Promise.all([listImportManifests(), listImportSchemas()]);
        setManifests(m ?? []);
        setSchemas(s ?? []);
      } catch {
        // empty selectors
      } finally {
        setLoadingSelectors(false);
      }
    })();
  }, []);

  const handleFileChange = (e: Event) => {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    setFileName(file.name);
    const detectedType = file.name.toLowerCase().endsWith(".json") ? "json" : "csv";
    setSourceType(detectedType);
    const reader = new FileReader();
    reader.onload = (ev) => {
      setFileContent((ev.target?.result as string) ?? "");
    };
    reader.readAsText(file);
  };

  const manifestsEmpty = !loadingSelectors && manifests.length === 0;
  const schemasEmpty = !loadingSelectors && schemas.length === 0;
  const canSelectInputs = !manifestsEmpty && !schemasEmpty;

  const handlePreview = async () => {
    setError(null);
    setPreview(null);
    setApplyResult(null);

    if (manifestsEmpty) {
      setError(`インポートには先に${UX_IMPORT_SETTINGS}が必要です。${UX_IMPORT_SETTINGS_PAGE}で作成してください。`);
      return;
    }
    if (schemasEmpty) {
      setError(`取り込み用の${UX_DATA_SHAPE}が登録されていません。${UX_IMPORT_SETTINGS_PAGE}で前提を整えてください。`);
      return;
    }
    if (!selectedManifestId) { setError(`${UX_IMPORT_SETTINGS}を選択してください。`); return; }
    if (!selectedSchemaId) { setError(`${UX_DATA_SHAPE}を選択してください。`); return; }
    if (!fileContent) { setError("ファイルが選択されていません。"); return; }

    setLoading(true);
    try {
      const result = await uploadImportPreview(
        sourceType, fileName || "upload", selectedManifestId, selectedSchemaId, fileContent,
      );
      setPreview(result);
    } catch (e) {
      setError(String(e));
    } finally {
      setLoading(false);
    }
  };

  const handleApply = async () => {
    if (!preview?.snapshotId) return;
    setError(null);
    setApplyResult(null);
    setLoading(true);
    try {
      const result = await applyImport(preview.snapshotId);
      setApplyResult(result);
    } catch (e) {
      setError(String(e));
    } finally {
      setLoading(false);
    }
  };

  return (
    <main class="page-main font-mono">
      <h1 class="page-title">topolactor — 管理 / インポート</h1>
      <p class="mb-4"><a href="/admin" class="link">&larr; 管理インデックス</a></p>

      <AdminHowTo
        steps={ADMIN_IMPORT_GUIDE.howToSteps}
        prerequisites={ADMIN_IMPORT_GUIDE.prerequisites}
      />
      <AdminHelpPanel {...ADMIN_IMPORT_GUIDE} />

      <hr class="mb-6 border-gray-200" />

      <section class="mb-6">
        <h2 class="section-title">1. {UX_IMPORT_SETTINGS}と{UX_DATA_SHAPE}を選択</h2>
        <AdminActionHint>
          {UX_IMPORT_SETTINGS}は「何をどこへ取り込むか」、{UX_DATA_SHAPE}は「各行の項目」です。
          どちらも先に登録されている必要があります（未登録の場合は下の案内に従ってください）。
        </AdminActionHint>
        {loadingSelectors ? (
          <p class="text-muted">選択肢をロード中...</p>
        ) : manifestsEmpty ? (
          <div class="alert-info">
            <p class="text-sm font-medium">
              インポートには先に{UX_IMPORT_SETTINGS}が必要です。まず{UX_IMPORT_SETTINGS_PAGE}で作成してください。
            </p>
            <p class="text-muted-xs mt-2">
              取り込みルールと表示・実行先の定義です。有効化後、この画面に戻ると選択できるようになります。
            </p>
            <a href="/admin/manifests" class="btn-primary mt-3 inline-block">
              {UX_IMPORT_SETTINGS_PAGE}へ
            </a>
          </div>
        ) : schemasEmpty ? (
          <div class="alert-info">
            <p class="text-sm font-medium">
              取り込み用の{UX_DATA_SHAPE}がまだ登録されていません。{UX_IMPORT_SETTINGS_PAGE}で前提を整えてください。
            </p>
            <p class="text-muted-xs mt-2">
              CSV/JSON の各行に必要な項目を決めます。プレビューには{UX_IMPORT_SETTINGS}と{UX_DATA_SHAPE}の両方が必要です。
            </p>
            <a href="/admin/manifests" class="btn-primary mt-3 inline-block">
              {UX_IMPORT_SETTINGS_PAGE}へ
            </a>
          </div>
        ) : (
          <div class="flex flex-wrap items-end gap-4">
            <label class="text-sm">
              {UX_IMPORT_SETTINGS}
              <select
                value={selectedManifestId}
                onChange={(e) => setSelectedManifestId((e.target as HTMLSelectElement).value)}
                class="input-mono mt-1 min-w-[260px]"
              >
                <option value="">— {UX_IMPORT_SETTINGS}を選択 —</option>
                {manifests.map((m) => (
                  <option key={m.manifestId} value={m.manifestId}>
                    {m.manifestId.slice(0, 8)}… [{m.status}]
                  </option>
                ))}
              </select>
            </label>
            <label class="text-sm">
              {UX_DATA_SHAPE}
              <select
                value={selectedSchemaId}
                onChange={(e) => setSelectedSchemaId((e.target as HTMLSelectElement).value)}
                class="input-mono mt-1 min-w-[220px]"
              >
                <option value="">— {UX_DATA_SHAPE}を選択 —</option>
                {schemas.map((s) => (
                  <option key={s.schemaId} value={s.schemaId}>
                    {s.name} ({s.schemaId.slice(0, 8)}…)
                  </option>
                ))}
              </select>
            </label>
          </div>
        )}
      </section>

      <section class="mb-6">
        <h2 class="section-title">2. CSV または JSON ファイルをアップロード</h2>
        {!canSelectInputs ? (
          <p class="text-muted text-sm">{UX_IMPORT_SETTINGS}と{UX_DATA_SHAPE}を用意してからファイルを選べます。</p>
        ) : (
          <div class="flex flex-wrap items-center gap-3">
            <input type="file" accept=".csv,.json" onChange={handleFileChange} class="text-sm" />
            {fileName && <span class="text-muted">{fileName} ({sourceType.toUpperCase()})</span>}
          </div>
        )}
      </section>

      <section class="mb-6">
        <h2 class="section-title">3. プレビュー（内容確認）</h2>
        <button
          onClick={handlePreview}
          disabled={loading || !canSelectInputs}
          class="btn-secondary mr-2"
        >
          プレビュー
        </button>
        <AdminActionHint>
          まだ保存はしません。{UX_IMPORT_SETTINGS}と{UX_DATA_SHAPE}に沿って各行を確認し、有効/無効の件数を表示します。
        </AdminActionHint>
        {!canSelectInputs && (
          <p class="text-muted-xs mt-2">
            プレビューできない理由: {manifestsEmpty ? `${UX_IMPORT_SETTINGS}が未登録` : `${UX_DATA_SHAPE}が未登録`}です。
          </p>
        )}
        {loading && <span class="text-muted">処理中...</span>}
      </section>

      {error && (
        <div class="alert-error mb-4">
          <p><strong>エラー:</strong> {error}</p>
          <p class="text-muted-xs mt-2">
            ファイル形式の誤り・{UX_IMPORT_SETTINGS}/{UX_DATA_SHAPE}の未選択はここに表示されます。
            無効行はプレビュー表を修正してから再プレビューしてください。
          </p>
          <details class="text-muted-xs mt-1">
            <summary class="cursor-pointer">技術情報</summary>
            <p class="mt-1">サーバー未接続時は接続設定と認証を確認してください。</p>
          </details>
        </div>
      )}

      {preview && (
        <section class="mb-6">
          <h2 class="section-title">プレビュー結果</h2>
          <p class="mb-3 text-sm">
            <span class="text-green-700">有効: {preview.validCount}</span>
            {" | "}
            <span class={preview.invalidCount > 0 ? "text-red-600" : "text-green-700"}>
              無効: {preview.invalidCount}
            </span>
            {" | "}合計: {preview.records.length}
            {" "}
            <details class="mt-1 inline-block">
              <summary class="cursor-pointer text-xs text-muted-xs">技術情報</summary>
              <code class="text-xs">{preview.snapshotId}</code>
            </details>
          </p>

          <div class="table-wrap max-h-96 overflow-y-auto">
            <table class="table text-xs">
              <thead>
                <tr>
                  <th>#</th>
                  <th>ステータス</th>
                  <th>レコード</th>
                  <th>エラー</th>
                </tr>
              </thead>
              <tbody>
                {preview.records.map((r) => (
                  <tr key={r.rowIndex} class={r.status === "invalid" ? "bg-red-50" : ""}>
                    <td>{r.rowIndex + 1}</td>
                    <td class={r.status === "valid" ? "text-green-700" : "text-red-600"}>{r.status}</td>
                    <td class="max-w-md truncate"><code>{JSON.stringify(r.records)}</code></td>
                    <td class="text-red-600">{r.validationErrors.join("; ")}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div class="mt-4">
            <h2 class="section-title">4. 適用</h2>
            <button
              onClick={handleApply}
              disabled={loading || preview.validCount === 0 || applyResult !== null}
              class="btn-primary"
            >
              適用 ({preview.validCount} 件有効)
            </button>
            <AdminActionHint>
              プレビュー済み snapshot の valid レコードのみ canonical DB へ明示反映。取り消しは別運用。
            </AdminActionHint>
          </div>
        </section>
      )}

      {applyResult && (
        <section class="alert-success mb-6">
          <h2 class="mb-1 font-semibold">取り込み完了</h2>
          <p class="mt-2 text-xs text-muted-xs">
            次のステップ:{" "}
            <a href="/admin/ui-builder" class="link">{UX_UI_BUILDER}</a> で画面を準備する、または{" "}
            <a href="/admin/runtime" class="link">{UX_RUNTIME_CHECK}</a> で動作を確認してください。
          </p>
          <details class="mt-1">
            <summary class="cursor-pointer text-xs text-green-700">技術情報</summary>
            <code class="text-xs">{applyResult.applyLogId}</code>
          </details>
        </section>
      )}
    </main>
  );
}
