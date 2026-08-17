import { JSX } from "preact";
import ProjectionShell from "../../islands/ProjectionShell.tsx";

/**
 * /dashboard — Normal (read-only) team-dashboard surface: a thin ProjectionShell wrapper pinned to
 * team_dashboard.normal.projection (db/seed_empty.sql dd020), the SAME generic production runtime
 * every other projection surface uses (matching /admin/team-dashboard's own thin-wrapper shape).
 * No AdminAuthGate — ProjectionShell itself accepts any authenticated realm and falls back to an
 * unauthenticated prompt when no valid session token is present; the backend's own capability gate
 * (docs/design/auth-db-session-credential-ssot.yaml manifest_capability_requirement.
 * layer_action_scoped_override) is what actually enforces "no admin requirement" — this route makes
 * no client-side role decision of its own.
 *
 * Reads the SAME topology.team_dashboard_note row the admin edit surface (/admin/team-dashboard)
 * writes, dispatched through the real ManifestDispatcher/Emission/Projection pipeline (previously a
 * REST bypass, GET /team-dashboard/note — retired, see
 * docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.normal.surfaces.dashboard.
 * team_dashboard_canonical_shared_contract). No edit/save control exists on this route at all.
 */
const TEAM_DASHBOARD_NORMAL_MANIFEST_ID = "00000000-0000-0000-0000-0000000dd020";

export default function DashboardRoute(): JSX.Element {
  return (
    <main class="page-main-wide">
      <ProjectionShell manifestId={TEAM_DASHBOARD_NORMAL_MANIFEST_ID} />
    </main>
  );
}
