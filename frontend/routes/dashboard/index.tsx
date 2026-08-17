import { JSX } from "preact";
import TeamDashboardViewer from "../../islands/TeamDashboardViewer.tsx";

/**
 * /dashboard — Normal (read-only) team-dashboard surface.
 * SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.normal.surfaces.
 * dashboard.team_dashboard_canonical_shared_contract. No AdminAuthGate — any authenticated session.
 * Reads the SAME topology.team_dashboard_note row the admin edit surface (/admin/team-dashboard)
 * writes; no edit/save control exists on this route at all.
 */
export default function DashboardRoute(): JSX.Element {
  return (
    <main class="page-main-wide">
      <TeamDashboardViewer />
    </main>
  );
}
