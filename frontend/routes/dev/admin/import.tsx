import { JSX } from "preact";
import AdminAuthGate from "../../../islands/AdminAuthGate.tsx";
import AdminImport from "../../../islands/AdminImport.tsx";

export default function AdminImportRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <AdminImport />
    </AdminAuthGate>
  );
}
