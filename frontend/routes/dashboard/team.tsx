import { JSX } from "preact";
import AuthenticatedGate from "../../islands/AuthenticatedGate.tsx";
import TeamDashboardRoleSurface from "../../islands/TeamDashboardRoleSurface.tsx";

/**
 * /dashboard/team — canonical Team Dashboard route, authenticated (Normal or admin).
 * Replaces /admin/team-dashboard as the primary route; that path now redirects here
 * (see routes/admin/team-dashboard/index.tsx).
 */
export default function TeamDashboardRoute(): JSX.Element {
  return (
    <AuthenticatedGate>
      <main class="page-main-wide">
        <TeamDashboardRoleSurface />
      </main>
    </AuthenticatedGate>
  );
}
