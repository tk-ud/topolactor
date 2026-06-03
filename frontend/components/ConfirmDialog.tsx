import { JSX } from "preact";

export type ConfirmDialogVariant = "default" | "danger";

export type ConfirmDialogProps = {
  open: boolean;
  message: string;
  title?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: ConfirmDialogVariant;
  onConfirm: () => void;
  onCancel: () => void;
};

export function ConfirmDialog({
  open,
  message,
  title = "確認",
  confirmLabel = "実行する",
  cancelLabel = "キャンセル",
  variant = "default",
  onConfirm,
  onCancel,
}: ConfirmDialogProps): JSX.Element | null {
  if (!open) return null;

  const confirmClass = variant === "danger"
    ? "btn-danger"
    : "btn-primary";

  return (
    <div
      role="alertdialog"
      aria-modal="true"
      aria-labelledby="confirm-dialog-title"
      aria-describedby="confirm-dialog-message"
      class="fixed inset-0 z-[1100] flex items-center justify-center bg-black/40 p-4"
      onClick={(e) => {
        if (e.target === e.currentTarget) onCancel();
      }}
    >
      <div class="card w-full max-w-md shadow-lg">
        <h2 id="confirm-dialog-title" class="mb-2 text-base font-semibold text-gray-900">
          {title}
        </h2>
        <p id="confirm-dialog-message" class="whitespace-pre-wrap text-sm text-gray-700">
          {message}
        </p>
        <div class="mt-4 flex flex-wrap justify-end gap-2">
          <button type="button" class="btn-secondary" onClick={onCancel}>
            {cancelLabel}
          </button>
          <button type="button" class={confirmClass} onClick={onConfirm}>
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
