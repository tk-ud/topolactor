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
 *
 * Generic route→Manifest identity resolution round (2026-08-17): pins by manifest_key, not a raw
 * UUID constant — resolved backend-side via the generic
 * manifest_key_target_ref_resolution_contract (ManifestDispatcher.cs, "exactly one active row"
 * fail-close over hubs.topology_manifests.manifest_key, the same stable identity string
 * db/seed_empty.sql already registers this manifest under). See
 * docs/design/runtime-orchestration-ssot.yaml dispatcher_contract for the full contract.
 */
const TEAM_DASHBOARD_NORMAL_MANIFEST_KEY = "team_dashboard.normal.projection";

export default function DashboardRoute(): JSX.Element {
  return (
    <main class="page-main-wide">
      <ProjectionShell manifestKey={TEAM_DASHBOARD_NORMAL_MANIFEST_KEY} />
    </main>
  );
}
