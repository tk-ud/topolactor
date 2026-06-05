import { JSX } from "preact";
import { Modal } from "./Modal.tsx";
import { PatchPreviewPanel } from "./PatchPreviewPanel.tsx";
import { LayoutVisualAuditCanvas } from "./LayoutVisualAuditCanvas.tsx";
import type { LayoutPatchPreviewAudit } from "../runtime/layoutPatchPreviewUtils.ts";
import type { LayoutPreviewNodeInput } from "../runtime/layoutComponentPreview.ts";

export type LayoutPatchPreviewModalProps = {
  open: boolean;
  audit: LayoutPatchPreviewAudit | null;
  previewNodes: LayoutPreviewNodeInput[];
  layoutClassRefs: string[];
  onClose: () => void;
  onProceedValidate?: () => void;
};

/**
 * SSOT visual audit modal — primary surface is rendered component layout;
 * text diff / resolver output stays in developer disclosure only.
 */
export function LayoutPatchPreviewModal({
  open,
  audit,
  previewNodes,
  layoutClassRefs,
  onClose,
  onProceedValidate,
}: LayoutPatchPreviewModalProps): JSX.Element | null {
  if (!audit) return null;

  return (
    <Modal
      open={open}
      title="レイアウトプレビュー — 保存前の視覚監査"
      description="配置されたコンポーネントを実際の見た目で確認してください（DB への書き込みはありません）。"
      onClose={onClose}
      design={{ style: "max-width:980px;width:100%;font-family:inherit" }}
      footer={
        <div class="flex flex-wrap justify-end gap-2">
          <button type="button" onClick={onClose} class="btn-secondary text-sm">
            閉じる
          </button>
          {onProceedValidate && (
            <button
              type="button"
              onClick={onProceedValidate}
              class="btn border border-blue-600 text-blue-600 hover:bg-blue-50 text-sm"
            >
              問題なければバリデートへ
            </button>
          )}
        </div>
      }
    >
      <div class="space-y-4 text-sm text-slate-800">
        <LayoutVisualAuditCanvas
          nodes={previewNodes}
          layoutClassRefs={layoutClassRefs}
          title="保存前レイアウト（visual preview）"
          minHeight={360}
        />

        <p class="m-0 text-xs text-slate-600">
          canvas と同じ runtime primitive preview を使用しています。
          {audit.backendMessage ? ` backend: ${audit.backendMessage}` : ""}
        </p>

        <details class="rounded border border-slate-200 px-3 py-2">
          <summary class="cursor-pointer text-xs font-medium text-slate-700">
            技術情報（className 解決・差分・JSON）
          </summary>
          <div class="mt-3 space-y-3">
            <section>
              <h4 class="mb-1 text-xs font-semibold text-slate-800">layoutClassRefs → className</h4>
              {audit.classResolutions.length === 0
                ? <p class="text-xs text-slate-500 m-0">layoutClassRefs 未選択</p>
                : (
                  <ul class="my-0 space-y-1 pl-4 text-xs">
                    {audit.classResolutions.map((row) => (
                      <li key={row.classRef}>
                        <code>{row.classRef}</code>
                        {" → "}
                        {row.ok
                          ? <code class="text-green-800">{row.className}</code>
                          : <span class="text-red-700">{row.message ?? "解決失敗"}</span>}
                      </li>
                    ))}
                  </ul>
                )}
            </section>
            <PatchPreviewPanel
              title="配置パッチ差分（送信前 → backend 正規化後）"
              fields={audit.fields}
            />
            <pre class="pre-box max-h-40 overflow-y-auto text-xs m-0">{audit.afterJson}</pre>
          </div>
        </details>
      </div>
    </Modal>
  );
}
