import { useEffect, useMemo, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  type AdminImportApplyResult,
  type AdminImportManifestItem,
  type AdminImportPreviewResult,
  type AdminImportSchemaItem,
  applyImport,
  listImportManifests,
  listImportSchemas,
  uploadImportPreview,
} from "../api/adminApi.ts";
import { AdminActionHint } from "../components/AdminHelpPanel.tsx";
import {
  UX_CONTENTS,
  UX_CONTENTS_PAGE,
  UX_DATA_SHAPE,
  UX_RUNTIME_CHECK,
  UX_UI_BUILDER,
} from "../content/adminUxTerms.ts";

export type AdminImportPanelProps = {
  /** When true, omit page chrome (for /admin/contents data-input section). */
  embedded?: boolean;
  /** Pre-select manifest when authoring on contents step 3. */
  defaultManifestId?: string;
  /** Lock manifest selector to the shared manifest id. */
  lockManifestId?: boolean;
  /** Merge preview rows into contents Step3 unified editor (non-destructive append). */
  onMergePreviewToEditor?: (preview: AdminImportPreviewResult) => void;
  /** After apply — parent may reload snapshot records into the editor. */
  onApplied?: (result: AdminImportApplyResult) => void;
};

export function AdminImportPanel({
  embedded = false,
  defaultManifestId = "",
  lockManifestId = false,
  onMergePreviewToEditor,
  onApplied,
}: AdminImportPanelProps): JSX.Element {
  const [manifests, setManifests] = useState<AdminImportManifestItem[]>([]);
  const [schemas, setSchemas] = useState<AdminImportSchemaItem[]>([]);
  const [selectedManifestId, setSelectedManifestId] = useState<string>(
    defaultManifestId,
  );
  const [selectedSchemaId, setSelectedSchemaId] = useState<string>("");
  const [sourceType, setSourceType] = useState<"csv" | "json">("csv");
  const [fileName, setFileName] = useState<string>("");
  const [fileContent, setFileContent] = useState<string>("");
  const [preview, setPreview] = useState<AdminImportPreviewResult | null>(null);
  const [applyResult, setApplyResult] = useState<AdminImportApplyResult | null>(
    null,
  );
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [loadingSelectors, setLoadingSelectors] = useState(true);

  useEffect(() => {
    if (defaultManifestId) {
      setSelectedManifestId(defaultManifestId);
    }
  }, [defaultManifestId]);

  useEffect(() => {
    (async () => {
      try {
        const [m, s] = await Promise.all([
          listImportManifests(),
          listImportSchemas(),
        ]);
        setManifests(m ?? []);
        setSchemas(s ?? []);
      } catch {
        // empty selectors
      } finally {
        setLoadingSelectors(false);
      }
    })();
  }, []);

  const manifestOptions = useMemo(() => {
    if (!embedded || !defaultManifestId) return manifests;
    if (manifests.some((m) => m.manifestId === defaultManifestId)) return manifests;
    return [
      {
        manifestId: defaultManifestId,
        status: "draft",
        createdAt: "",
      },
      ...manifests,
    ];
  }, [manifests, embedded, defaultManifestId]);

  const handleFileChange = (e: Event) => {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    setFileName(file.name);
    const detectedType = file.name.toLowerCase().endsWith(".json")
      ? "json"
      : "csv";
    setSourceType(detectedType);
    const reader = new FileReader();
    reader.onload = (ev) => {
      setFileContent((ev.target?.result as string) ?? "");
    };
    reader.readAsText(file);
  };

  const manifestsEmpty = !loadingSelectors && manifestOptions.length === 0;
  const schemasEmpty = !loadingSelectors && schemas.length === 0;
  const canSelectInputs = !manifestsEmpty && !schemasEmpty;

  const handlePreview = async () => {
    setError(null);
    setPreview(null);
    setApplyResult(null);

    if (manifestsEmpty) {
      setError(
        `インポートには先に${UX_CONTENTS}が必要です。${UX_CONTENTS_PAGE}で データの形を設定してください。`,
      );
      return;
    }
    if (schemasEmpty) {
      setError(
        `取り込み用の${UX_DATA_SHAPE}が登録されていません。${UX_CONTENTS_PAGE}で前提を整えてください。`,
      );
      return;
    }
    if (!selectedManifestId) {
      setError("取り込み先の画面を選択してください。");
      return;
    }
    if (!selectedSchemaId) {
      setError(`${UX_DATA_SHAPE}を選択してください。`);
      return;
    }
    if (!fileContent) {
      setError("ファイルが選択されていません。");
      return;
    }

    setLoading(true);
    try {
      const result = await uploadImportPreview(
        sourceType,
        fileName || "upload",
        selectedManifestId,
        selectedSchemaId,
        fileContent,
      );
      setPreview(result);
    } catch (e) {
      console.error("IMPORT_PREVIEW_FAILED", e);
      setError(
        "プレビューを作成できませんでした。ファイル内容と接続状態を確認してください。",
      );
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
      if (result.ok) onApplied?.(result);
    } catch (e) {
      console.error("IMPORT_APPLY_FAILED", e);
      setError(
        "取り込みを完了できませんでした。接続状態を確認して再度お試しください。",
      );
    } finally {
      setLoading(false);
    }
  };

  const sectionTitleClass = embedded ? "text-xs font-semibold mt-3" : "section-title";
  const Wrapper = embedded ? "div" : "main";
  const wrapperClass = embedded ? "rounded border border-slate-200 bg-white p-3 text-sm" : "page-main font-mono";

  return (
    <Wrapper class={wrapperClass}>
      {!embedded && (
        <>
          <h1 class="page-title">topolactor — 管理 / インポート</h1>
          <p class="mb-4">
            <a href="/admin" class="link">&larr; 管理インデックス</a>
          </p>
          <hr class="mb-6 border-gray-200" />
        </>
      )}

      <section class={embedded ? "mb-4" : "mb-6"}>
        <h2 class={sectionTitleClass}>
          {embedded ? "画面と取り込みルール" : `1. 画面と${UX_DATA_SHAPE}を選択`}
        </h2>
        <AdminActionHint>
          {UX_CONTENTS}で定義した画面（取り込み先）と、{UX_DATA_SHAPE}（各行の項目）を選びます。
          未登録の場合は下の案内に従ってください。
        </AdminActionHint>
        {loadingSelectors
          ? <p class="text-muted">選択肢をロード中...</p>
          : manifestsEmpty
          ? (
            <div class="alert-info">
              <p class="text-sm font-medium">
                取り込み先の画面がありません。まず step 1–2 で画面の内容と
                データの形を設定してください。
              </p>
            </div>
          )
          : schemasEmpty
          ? (
            <div class="alert-info">
              <p class="text-sm font-medium">
                取り込み用の{UX_DATA_SHAPE}がまだありません。step 2 でデータの形を整えてください。
              </p>
            </div>
          )
          : (
            <div class="flex flex-wrap items-end gap-4">
              <label class="text-sm">
                画面
                <select
                  value={selectedManifestId}
                  disabled={lockManifestId && !!defaultManifestId}
                  onChange={(e) =>
                    setSelectedManifestId(
                      (e.target as HTMLSelectElement).value,
                    )}
                  class="input-mono mt-1 min-w-[260px]"
                >
                  <option value="">— 画面を選択 —</option>
                  {manifestOptions.map((m, index) => (
                    <option key={m.manifestId} value={m.manifestId}>
                      {m.manifestId === defaultManifestId && lockManifestId
                        ? "このページ（編集中）"
                        : `取り込み先 ${index + 1}`}
                    </option>
                  ))}
                </select>
              </label>
              <label class="text-sm">
                {UX_DATA_SHAPE}
                <select
                  value={selectedSchemaId}
                  onChange={(e) =>
                    setSelectedSchemaId((e.target as HTMLSelectElement).value)}
                  class="input-mono mt-1 min-w-[220px]"
                >
                  <option value="">— {UX_DATA_SHAPE}を選択 —</option>
                  {schemas.map((s, index) => (
                    <option key={s.schemaId} value={s.schemaId}>
                      {s.name || `データの形 ${index + 1}`}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          )}
      </section>

      <section class={embedded ? "mb-4" : "mb-6"}>
        <h2 class={sectionTitleClass}>
          {embedded ? "CSV または JSON ファイル" : "2. CSV または JSON ファイルをアップロード"}
        </h2>
        {!canSelectInputs
          ? (
            <p class="text-muted text-sm">
              画面と{UX_DATA_SHAPE}を用意してからファイルを選べます。
            </p>
          )
          : (
            <div class="flex flex-wrap items-center gap-3">
              <input
                type="file"
                accept=".csv,.json"
                onChange={handleFileChange}
                class="text-sm"
              />
              {fileName && (
                <span class="text-muted">
                  {fileName} ({sourceType.toUpperCase()})
                </span>
              )}
            </div>
          )}
      </section>

      <section class={embedded ? "mb-4" : "mb-6"}>
        <h2 class={sectionTitleClass}>
          {embedded ? "プレビュー（内容確認）" : "3. プレビュー（内容確認）"}
        </h2>
        <button
          onClick={handlePreview}
          disabled={loading || !canSelectInputs}
          class="btn-secondary mr-2"
        >
          プレビュー
        </button>
        <AdminActionHint>
          まだ保存はしません。{UX_DATA_SHAPE}に沿って各行を確認し、有効/無効の件数を表示します。
        </AdminActionHint>
        {!canSelectInputs && (
          <p class="text-muted-xs mt-2">
            プレビューできない理由: {manifestsEmpty
              ? `${UX_CONTENTS}が未整備`
              : `${UX_DATA_SHAPE}が未登録`}です。
          </p>
        )}
        {loading && <span class="text-muted">処理中...</span>}
      </section>

      {error && (
        <div class="alert-error mb-4">
          <p>
            <strong>エラー:</strong> {error}
          </p>
        </div>
      )}

      {preview && (
        <section class={embedded ? "mb-4" : "mb-6"}>
          <h2 class={sectionTitleClass}>プレビュー結果</h2>
          <p class="mb-3 text-sm">
            <span class="text-green-700">有効: {preview.validCount}</span>
            {" | "}
            <span
              class={preview.invalidCount > 0
                ? "text-red-600"
                : "text-green-700"}
            >
              無効: {preview.invalidCount}
            </span>
            {" | "}合計: {preview.records.length}
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
                  <tr
                    key={r.rowIndex}
                    class={r.status === "invalid" ? "bg-red-50" : ""}
                  >
                    <td>{r.rowIndex + 1}</td>
                    <td
                      class={r.status === "valid"
                        ? "text-green-700"
                        : "text-red-600"}
                    >
                      {r.status === "valid" ? "有効" : "要修正"}
                    </td>
                    <td class="max-w-md">
                      <details>
                        <summary class="cursor-pointer">入力内容を表示</summary>
                        <code>{JSON.stringify(r.records)}</code>
                      </details>
                    </td>
                    <td class="text-red-600">
                      {r.validationErrors.join("; ")}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {embedded && onMergePreviewToEditor && (
            <div class="mt-3">
              <button
                type="button"
                onClick={() => onMergePreviewToEditor(preview)}
                class="btn-secondary mr-2"
              >
                編集グリッドへ追加（全 {preview.records.length} 行）
              </button>
              <AdminActionHint>
                プレビュー行を Step3 の同一グリッドへ追加します（既存行は保持）。型式診断はグリッド上で表示されます。
              </AdminActionHint>
            </div>
          )}

          <div class="mt-4">
            <h2 class={sectionTitleClass}>
              {embedded ? "適用（明示操作）" : "4. 適用"}
            </h2>
            <button
              onClick={handleApply}
              disabled={loading || preview.validCount === 0 ||
                applyResult !== null}
              class="btn-primary"
            >
              適用 ({preview.validCount} 件有効)
            </button>
            <AdminActionHint>
              プレビューで問題がなかった行だけを取り込みます。適用後はスナップショットから再読込できます。
            </AdminActionHint>
          </div>
        </section>
      )}

      {applyResult && (
        <section class="alert-success mb-4">
          <h2 class="mb-1 font-semibold text-sm">取り込み完了</h2>
          {!embedded && (
            <p class="mt-2 text-xs text-muted-xs">
              次のステップ:{" "}
              <a href="/admin/ui-builder" class="link">{UX_UI_BUILDER}</a>{" "}
              で画面を準備する、または{" "}
              <a href="/demo/debug" class="link">{UX_RUNTIME_CHECK}</a>{" "}
              で動作を確認してください。
            </p>
          )}
        </section>
      )}
    </Wrapper>
  );
}

/** Standalone island (legacy mount surface; canonical path is /admin/contents embedded panel). */
export default function AdminImport(): JSX.Element {
  return <AdminImportPanel />;
}
