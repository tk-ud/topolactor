import { JSX } from "preact";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import ManifestsAdmin from "../../islands/ManifestsAdmin.tsx";

export default function ManifestsAdminRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <ManifestsAdmin />
    </AdminAuthGate>
  );
}
