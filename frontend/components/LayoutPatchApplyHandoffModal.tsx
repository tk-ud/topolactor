import { JSX } from "preact";
import { Modal } from "./Modal.tsx";
import { LayoutVisualAuditCanvas } from "./LayoutVisualAuditCanvas.tsx";
import type { LayoutPreviewNodeInput } from "../runtime/layoutComponentPreview.ts";
import { UX_UI_BUILDER_TAB_LABELS } from "../content/adminUxTerms.ts";

export type LayoutPatchApplyHandoffProps = {
  open: boolean;
  routeKey: string;
  layoutLabel: string;
  nodeCount: number;
  previewNodes: LayoutPreviewNodeInput[];
  layoutClassRefs: string[];
  onClose: () => void;
  onGoDesign: () => void;
};

/**
 * Post-apply handoff — SSOT step 4.2 layout child saved; guide to design / demo.
 */
export function LayoutPatchApplyHandoffModal({
  open,
  routeKey,
  layoutLabel,
  nodeCount,
  previewNodes,
  layoutClassRefs,
  onClose,
  onGoDesign,
}: LayoutPatchApplyHandoffProps): JSX.Element | null {
  if (!open) return null;

  const designLabel = UX_UI_BUILDER_TAB_LABELS.design ?? "デザイン設定";

  return (
    <Modal
      open={open}
      title="配置を保存しました"
      description="layout child の保存が完了しました。次に進む操作を選んでください。"
      onClose={onClose}
      design={{ style: "max-width:920px;width:100%;font-family:inherit" }}
      footer={
        <div class="flex flex-wrap justify-end gap-2">
          <button type="button" onClick={onClose} class="btn-secondary text-sm">
            この画面で続ける
          </button>
        </div>
      }
    >
      <div class="space-y-4 text-sm text-slate-800">
        <div class="rounded-lg border border-green-300 bg-green-50 px-4 py-3">
          <p class="m-0 font-semibold text-green-900">
            ✓ DB に配置を反映しました（{nodeCount} ノード）
          </p>
          <p class="mb-0 mt-1 text-xs text-green-800">
            ルート <code>{routeKey || "—"}</code>
            {" / "}
            レイアウト <code>{layoutLabel || "—"}</code>
          </p>
        </div>

        <LayoutVisualAuditCanvas
          nodes={previewNodes}
          layoutClassRefs={layoutClassRefs}
          title="保存済み配置（visual preview）"
          minHeight={280}
        />

        <section aria-label="次のステップ">
          <h3 class="mb-2 text-sm font-semibold text-slate-900">次に進む</h3>
          <ol class="my-0 space-y-2 pl-0 list-none">
            <li>
              <button
                type="button"
                onClick={onGoDesign}
                class="btn-success w-full text-left px-4 py-3"
              >
                <span class="block font-semibold">1. {designLabel}へ</span>
                <span class="block text-xs font-normal opacity-90 mt-0.5">
                  色・形・CSS トークン（cssTokenRefs）をパッケージ単位で保存（step 4.2 component_design）
                </span>
              </button>
            </li>
            <li>
              <a
                href="/demo"
                class="btn-secondary block w-full text-left px-4 py-3 no-underline"
              >
                <span class="block font-semibold">2. デモで表示を確認</span>
                <span class="block text-xs font-normal text-slate-600 mt-0.5">
                  保存した配置が runtime 投影でどう見えるか確認します。
                  ドラフトコンテンツが未登録の場合は管理画面で先に作成してください。
                </span>
              </a>
            </li>
          </ol>
        </section>
      </div>
    </Modal>
  );
}
