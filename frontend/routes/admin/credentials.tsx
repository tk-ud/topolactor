import { JSX } from "preact";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import AdminCredentialPanel from "../../islands/AdminCredentialPanel.tsx";

export default function AdminCredentialsRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <AdminCredentialPanel />
    </AdminAuthGate>
  );
}
