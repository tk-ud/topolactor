import { JSX } from "preact";
import AdminAuthGate from "../../../islands/AdminAuthGate.tsx";
import SeedAdmin from "../../../islands/SeedAdmin.tsx";

export default function SeedAdminRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <SeedAdmin />
    </AdminAuthGate>
  );
}
