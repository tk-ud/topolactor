import { JSX } from "preact";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import ProjectionShell from "../../islands/ProjectionShell.tsx";

// Route retirement (admin-enum subBundle closure round): same URL (/admin/enums), no longer
// AdminEnumsRoster -- a thin ProjectionShell wrapper pinned to the admin.enum.management.projection
// manifest (db/seed_empty.sql ae200), the SAME generic production runtime every other projection
// surface uses. UX parity (selected-group prefill, enum_search filter, all 7 write operations) is
// reached via the generic admin_runtime substrate, not a dedicated island.
const ADMIN_ENUM_MANAGEMENT_MANIFEST_ID = "00000000-0000-0000-0000-0000000ae200";

export default function AdminEnumsRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <ProjectionShell manifestId={ADMIN_ENUM_MANAGEMENT_MANIFEST_ID} />
    </AdminAuthGate>
  );
}
