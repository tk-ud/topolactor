import { JSX } from "preact";
import AdminAuthGate from "../../../islands/AdminAuthGate.tsx";
import ProjectionShell from "../../../islands/ProjectionShell.tsx";

// team-dashboard subBundle (admin-surface-topology-seed-conversion): same URL (/admin/team-dashboard),
// no longer mounting TeamMarkdownAuthoring (moved to /admin/team-markdown, see
// docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.normal.surfaces.dashboard.
// superseding_authority_note) — a thin ProjectionShell wrapper pinned to the new
// team_dashboard.admin.projection manifest (db/seed_empty.sql), the SAME generic production runtime
// every other projection surface uses (matching /admin/enums' own thin-wrapper shape). This manifest
// reads/writes the shared topology.team_dashboard_note row via team_dashboard:get/update; the SAME
// row is read read-only at /dashboard (surface_axes.normal.surfaces.dashboard.
// team_dashboard_canonical_shared_contract) — no duplicated data identity between the two routes.
const TEAM_DASHBOARD_ADMIN_MANIFEST_ID = "00000000-0000-0000-0000-0000000dd010";

export default function AdminTeamDashboardRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <ProjectionShell manifestId={TEAM_DASHBOARD_ADMIN_MANIFEST_ID} />
    </AdminAuthGate>
  );
}
