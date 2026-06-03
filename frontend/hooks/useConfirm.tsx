import { useCallback, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  ConfirmDialog,
  type ConfirmDialogVariant,
} from "../components/ConfirmDialog.tsx";

export type ConfirmOptions = {
  title?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: ConfirmDialogVariant;
};

export type ConfirmFn = (
  message: string,
  options?: ConfirmOptions,
) => Promise<boolean>;

type PendingConfirm = {
  message: string;
  options: ConfirmOptions;
  resolve: (accepted: boolean) => void;
};

export function useConfirm(): {
  confirm: ConfirmFn;
  ConfirmDialogHost: () => JSX.Element | null;
} {
  const [pending, setPending] = useState<PendingConfirm | null>(null);

  const confirm = useCallback<ConfirmFn>((message, options) => {
    return new Promise<boolean>((resolve) => {
      setPending({
        message,
        options: options ?? {},
        resolve,
      });
    });
  }, []);

  const close = (accepted: boolean) => {
    pending?.resolve(accepted);
    setPending(null);
  };

  const ConfirmDialogHost = (): JSX.Element | null => (
    <ConfirmDialog
      open={pending !== null}
      message={pending?.message ?? ""}
      title={pending?.options.title}
      confirmLabel={pending?.options.confirmLabel}
      cancelLabel={pending?.options.cancelLabel}
      variant={pending?.options.variant}
      onConfirm={() => close(true)}
      onCancel={() => close(false)}
    />
  );

  return { confirm, ConfirmDialogHost };
}
