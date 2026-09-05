import { JSX } from "preact";
import { Modal } from "./Modal.tsx";
import type { ValidationError } from "../api/dispatch.ts";

export type LayoutPatchApplyModalPhase =
  | "validating"
  | "validated"
  | "applying"
  | "success";

export type LayoutPatchApplySummary = {
  valid: boolean;
  message: string;
  nodeCount: number;
  draftOnlyCount: number;
  routeKey: string;
  layoutId: string;
  layoutKey?: string;
  errors: ValidationError[];
};

export type LayoutPatchApplyModalProps = {
  open: boolean;
  phase: LayoutPatchApplyModalPhase;
  summary: LayoutPatchApplySummary | null;
  routeKey: string;
  layoutId: string;
  layoutLabel: string;
  loading: boolean;
  onClose: () => void;
  onConfirmApply: () => void;
  onGoDesign: () => void;
};

function ValidationList({
  errors,
  title,
}: {
  errors: ValidationError[];
  title: string;
}): JSX.Element | null {
  if (errors.length === 0) return null;
  return (
    <div class="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
      <p class="m-0 mb-2 font-semibold">{title}</p>
      <ul class="my-0 space-y-1 pl-4">
        {errors.map((err, i) => (
          <li key={`${err.code ?? "err"}-${i}`}>
            {err.message}
            {err.code && (
              <details class="ml-1 inline text-xs text-red-600">
                <summary class="inline cursor-pointer">技術情報</summary>
                <span class="ml-1 font-mono">{err.code}</span>
              </details>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * Apply flow: validate inside modal → confirm save → post-apply handoff (no canvas).
 * Normal-view label boundary: this modal is the primary, routinely-used Apply confirmation
 * surface for /admin/ui-builder — raw property names (routeKey/layoutId/layoutKey），internal
 * table/module names (layout_patch_json/component_style_design/renderEmission), and internal
 * field names (inlineText/cssTokenRefs/initialDataRows) must never be the PRIMARY visible text;
 * they stay reachable behind explicit 技術情報 disclosures for diagnostics.
 */
export function LayoutPatchApplyModal({
  open,
  phase,
  summary,
  routeKey,
  layoutId,
  layoutLabel,
  loading,
  onClose,
  onConfirmApply,
  onGoDesign,
}: LayoutPatchApplyModalProps): JSX.Element | null {
  if (!open) return null;

  // Read-only projection inspection panel on the same /admin/ui-builder page.
  const inspectionHref = "#projection-inspection";
  const isSuccess = phase === "success";
  const canConfirm = phase === "validated" && summary?.valid === true && !loading;
  const friendlyTarget = layoutLabel.trim() || "（名称未設定のレイアウト）";

  return (
    <Modal
      open={open}
      title={isSuccess ? "配置を保存しました" : "配置を保存"}
      description={isSuccess
        ? "配置の保存が完了しました。"
        : "保存前に内容を確認します。問題がなければそのまま保存できます。"}
      onClose={onClose}
      design={{ style: "max-width:640px;width:100%;font-family:inherit" }}
      footer={
        <div class="flex flex-wrap justify-end gap-2">
          {!isSuccess && (
            <button type="button" onClick={onClose} class="btn-secondary text-sm">
              キャンセル
            </button>
          )}
          {isSuccess
            ? (
              <button type="button" onClick={onClose} class="btn-secondary text-sm">
                この画面で続ける
              </button>
            )
            : (
              <button
                type="button"
                onClick={onConfirmApply}
                disabled={!canConfirm}
                class="btn-success text-sm"
              >
                {phase === "applying" ? "保存中..." : "保存する"}
              </button>
            )}
        </div>
      }
    >
      <div class="space-y-4 text-sm text-slate-800">
        <div class="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs">
          対象: <span class="font-medium">{friendlyTarget}</span>
          <details class="mt-1 text-[0.7rem] text-slate-500">
            <summary class="cursor-pointer">技術情報（開発者向け）</summary>
            <p class="mt-1 font-mono">
              routeKey <code>{routeKey || "—"}</code>
              {" · "}
              layoutId <code>{layoutId || "—"}</code>
              {layoutLabel && (
                <>
                  {" · "}
                  layoutKey <code>{layoutLabel}</code>
                </>
              )}
            </p>
          </details>
        </div>

        {!isSuccess && (
          <>
            {phase === "validating" && (
              <p class="m-0 text-slate-600" aria-live="polite">
                内容を確認中...
              </p>
            )}

            {summary && phase !== "validating" && (
              <div
                class={`rounded-lg border px-4 py-3 ${
                  summary.valid
                    ? "border-green-300 bg-green-50"
                    : "border-red-300 bg-red-50"
                }`}
              >
                <p
                  class={`m-0 font-semibold ${
                    summary.valid ? "text-green-900" : "text-red-900"
                  }`}
                >
                  {summary.valid ? "✓ 確認完了" : "✗ 修正が必要です"}
                </p>
                <p class="mb-0 mt-1 text-xs text-slate-700">
                  部品 {summary.nodeCount} 件
                  {summary.draftOnlyCount > 0
                    ? ` · まだ使えない部品 ${summary.draftOnlyCount} 件`
                    : ""}
                  {" · "}
                  {summary.message}
                </p>
              </div>
            )}

            <ValidationList
              errors={summary?.errors ?? []}
              title="修正が必要なエラー"
            />

            {summary?.valid && phase === "validated" && (
              <div class="text-xs text-slate-500">
                <p class="m-0">
                  この配置は保存するとすぐに公開されます。デザイン（色・文言など）は別途右パネルから保存してください。
                </p>
                <details class="mt-1">
                  <summary class="cursor-pointer">技術情報（開発者向け）</summary>
                  <p class="mt-1">
                    canvas の配置はそのまま layout_patch_json へ昇格します。デザイン（inlineText / cssTokenRefs）は別途保存が必要です。
                  </p>
                </details>
              </div>
            )}
          </>
        )}

        {isSuccess && summary && (
          <>
            <div class="rounded-lg border border-green-300 bg-green-50 px-4 py-3">
              <p class="m-0 font-semibold text-green-900">
                ✓ 配置を保存しました（{summary.nodeCount} 件）
              </p>
            </div>

            <section aria-label="次のステップ">
              <h3 class="mb-2 text-sm font-semibold text-slate-900">次に進む</h3>
              <ol class="my-0 space-y-2 pl-0 list-none">
                <li>
                  <button
                    type="button"
                    onClick={onGoDesign}
                    class="btn-success w-full text-left px-4 py-3"
                  >
                    <span class="block font-semibold">1. デザインを保存（右パネル）</span>
                    <span class="block text-xs font-normal opacity-90 mt-0.5">
                      色・文言などの見た目を別途保存します
                    </span>
                  </button>
                </li>
                <li>
                  <a
                    href={inspectionHref}
                    onClick={onClose}
                    class="btn-secondary block w-full text-left px-4 py-3 no-underline"
                  >
                    <span class="block font-semibold">
                      2. 表示を確認（読み取り専用）
                    </span>
                    <span class="block text-xs font-normal text-slate-600 mt-0.5">
                      canvas のプレビューと実際の表示を見比べて確認します（このページ下部）
                    </span>
                  </a>
                </li>
                <li>
                  <a
                    href="/admin/contents"
                    class="btn-secondary block w-full text-left px-4 py-3 no-underline"
                  >
                    <span class="block font-semibold">3. サンプルデータを追加（任意）</span>
                    <span class="block text-xs font-normal text-slate-600 mt-0.5">
                      確認画面に表示するデータを追加できます
                    </span>
                  </a>
                </li>
              </ol>
              <details class="mt-2 text-xs text-slate-500">
                <summary class="cursor-pointer">技術情報（開発者向け）</summary>
                <p class="mt-1">
                  デザイン保存は component_style_design として別保存されます。表示確認は renderEmission
                  経路で canvas プレビューと applied projection を比較します。サンプルデータは Contents
                  Step 3 の initialDataRows がインスペクションのコンテンツ投影に使われます。
                </p>
              </details>
            </section>
          </>
        )}
      </div>
    </Modal>
  );
}
