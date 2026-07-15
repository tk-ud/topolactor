import { JSX } from "preact";
import AuthenticatedGate from "../../islands/AuthenticatedGate.tsx";
import PersonalPage from "../../islands/PersonalPage.tsx";

/**
 * /account — Personal Page. Authenticated (Normal or admin); shows only the caller's own
 * account/session/password data. See docs/design/admin-normal-surface-projection-seed-ssot.yaml
 * surface_axes.normal.surfaces.personal_page.
 */
export default function AccountRoute(): JSX.Element {
  return (
    <AuthenticatedGate>
      <PersonalPage />
    </AuthenticatedGate>
  );
}
