import { JSX } from "preact";
import {
  adminSubmitStatusClass,
  type AdminSubmitStatusState,
} from "../lib/adminSubmitUx.ts";

export function AdminSubmitStatus({
  status,
}: {
  status: AdminSubmitStatusState;
}): JSX.Element | null {
  if (!status.message) return null;
  const cls = adminSubmitStatusClass(status.outcome);
  const role = status.outcome === "error" ? "alert" : "status";
  return (
    <p class={`mb-3 text-sm ${cls}`} role={role} aria-live="polite">
      {status.message}
    </p>
  );
}
