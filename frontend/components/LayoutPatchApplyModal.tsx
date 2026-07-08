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
              <span class="ml-1 font-mono text-xs text-red-600">({err.code})</span>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * Apply flow: validate inside modal → confirm DB write → post-apply handoff (no canvas).
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

  return (
    <Modal
      open={open}
      title={isSuccess ? "配置を保存しました" : "配置を DB に反映"}
      description={isSuccess
        ? "layout_patch の Apply が完了しました。配置は DB 正本（layout_patch_json）に反映されています。"
        : "保存前にバリデーションを実行します。問題がなければ DB へ反映できます。"}
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
                {phase === "applying" ? "反映中..." : "DB に反映する"}
              </button>
            )}
        </div>
      }
    >
      <div class="space-y-4 text-sm text-slate-800">
        <div class="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs font-mono">
          routeKey <code>{routeKey || "—"}</code>
          {" · "}
          layoutId <code>{layoutId || "—"}</code>
          {layoutLabel && (
            <>
              {" · "}
              layoutKey <code>{layoutLabel}</code>
            </>
          )}
        </div>

        {!isSuccess && (
          <>
            {phase === "validating" && (
              <p class="m-0 text-slate-600" aria-live="polite">
                バリデーションを実行中...
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
                  {summary.valid ? "✓ バリデーション通過" : "✗ バリデーションエラー"}
                </p>
                <p class="mb-0 mt-1 text-xs text-slate-700">
                  ノード {summary.nodeCount} 件
                  {summary.draftOnlyCount > 0
                    ? ` · 未登録部品 ${summary.draftOnlyCount} 件`
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
              <p class="m-0 text-xs text-slate-500">
                canvas の配置はそのまま DB（layout_patch_json）へ昇格します。
                デザイン（inlineText / cssTokenRefs）は別途右パネルから保存してください。
              </p>
            )}
          </>
        )}

        {isSuccess && summary && (
          <>
            <div class="rounded-lg border border-green-300 bg-green-50 px-4 py-3">
              <p class="m-0 font-semibold text-green-900">
                ✓ DB に配置を反映しました（{summary.nodeCount} ノード）
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
                      inlineText / cssTokenRefs は component_style_design として別保存
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
                      2. 投影インスペクションで確認（読み取り専用）
                    </span>
                    <span class="block text-xs font-normal text-slate-600 mt-0.5">
                      renderEmission 経路で canvas プレビューと applied projection を比較確認（このページ下部）
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
                      Contents Step 3 の initialDataRows がインスペクションのコンテンツ投影に使われます
                    </span>
                  </a>
                </li>
              </ol>
            </section>
          </>
        )}
      </div>
    </Modal>
  );
}
