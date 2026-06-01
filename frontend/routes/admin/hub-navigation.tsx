import { JSX } from "preact";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import HubNavigationAdmin from "../../islands/HubNavigationAdmin.tsx";

export default function HubNavigationAdminRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <HubNavigationAdmin />
    </AdminAuthGate>
  );
}
