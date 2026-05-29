import { JSX } from "preact";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import ContentsAdmin from "../../islands/ContentsAdmin.tsx";

export default function ContentsAdminRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <ContentsAdmin />
    </AdminAuthGate>
  );
}
