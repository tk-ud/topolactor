import { JSX } from "preact";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import AdminEnumsRoster from "../../islands/AdminEnumsRoster.tsx";

export default function AdminEnumsRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <AdminEnumsRoster />
    </AdminAuthGate>
  );
}
