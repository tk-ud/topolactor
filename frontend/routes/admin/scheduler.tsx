import { JSX } from "preact";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import SchedulerJobSettingsPanel from "../../islands/SchedulerJobSettingsPanel.tsx";

export default function AdminSchedulerRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <SchedulerJobSettingsPanel />
    </AdminAuthGate>
  );
}
