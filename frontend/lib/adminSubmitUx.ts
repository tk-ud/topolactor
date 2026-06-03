/** Shared admin submit UX: confirm dialog, loading, success/error status. */

import type { ConfirmFn } from "../hooks/useConfirm.tsx";

export type AdminSubmitOutcome = "idle" | "loading" | "success" | "error";

export type AdminSubmitStatusState = {
  outcome: AdminSubmitOutcome;
  message: string | null;
};

export const initialAdminSubmitStatus = (): AdminSubmitStatusState => ({
  outcome: "idle",
  message: null,
});

export function adminSubmitStatusClass(outcome: AdminSubmitOutcome): string {
  switch (outcome) {
    case "success":
      return "alert-success";
    case "error":
      return "alert-error";
    case "loading":
      return "alert-info";
    default:
      return "text-muted-xs";
  }
}

export type AdminSubmitConfirmKind = "save" | "update" | "delete" | "create";

const CONFIRM_MESSAGES: Record<AdminSubmitConfirmKind, string> = {
  create: "新規作成を実行します。よろしいですか？",
  save: "保存します。よろしいですか？",
  update: "更新します。よろしいですか？",
  delete: "削除します。この操作は取り消せない場合があります。よろしいですか？",
};

export function adminSubmitConfirmMessage(
  kind: AdminSubmitConfirmKind,
  customMessage?: string,
): string {
  return customMessage ?? CONFIRM_MESSAGES[kind];
}

export async function adminSubmitConfirm(
  confirmFn: ConfirmFn,
  kind: AdminSubmitConfirmKind,
  customMessage?: string,
): Promise<boolean> {
  const message = adminSubmitConfirmMessage(kind, customMessage);
  return confirmFn(message, {
    title: "確認",
    variant: kind === "delete" ? "danger" : "default",
    confirmLabel: kind === "delete" ? "削除する" : "実行する",
  });
}

export type RunAdminSubmitOptions<T> = {
  confirmFn?: ConfirmFn;
  confirmKind?: AdminSubmitConfirmKind;
  confirmMessage?: string;
  skipConfirm?: boolean;
  successMessage: string;
  errorMessage?: string;
  onSuccess?: (result: T) => void | Promise<void>;
  run: () => Promise<T>;
};

export async function runAdminSubmit<T>(
  setStatus: (s: AdminSubmitStatusState) => void,
  setLoading: (v: boolean) => void,
  options: RunAdminSubmitOptions<T>,
): Promise<T | null> {
  if (options.confirmKind && !options.skipConfirm) {
    if (!options.confirmFn) {
      throw new Error(
        "runAdminSubmit: confirmFn is required when confirmKind is set",
      );
    }
    if (
      !(await adminSubmitConfirm(
        options.confirmFn,
        options.confirmKind,
        options.confirmMessage,
      ))
    ) {
      return null;
    }
  }
  setLoading(true);
  setStatus({ outcome: "loading", message: "処理中…" });
  try {
    const result = await options.run();
    setStatus({ outcome: "success", message: options.successMessage });
    if (options.onSuccess) await options.onSuccess(result);
    return result;
  } catch (e) {
    console.error("ADMIN_SUBMIT_FAILED", e);
    setStatus({
      outcome: "error",
      message: options.errorMessage ??
        "操作に失敗しました。接続状態と入力内容を確認してください。",
    });
    return null;
  } finally {
    setLoading(false);
  }
}
