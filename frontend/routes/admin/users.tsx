import { JSX } from "preact";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import AdminUsersRoster from "../../islands/AdminUsersRoster.tsx";

export default function AdminUsersRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <AdminUsersRoster />
    </AdminAuthGate>
  );
}
