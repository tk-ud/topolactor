import { JSX } from "preact";
import AuthenticatedGate from "../../islands/AuthenticatedGate.tsx";
import NormalDashboardHome from "../../islands/NormalDashboardHome.tsx";

/** /dashboard — authenticated (Normal or admin) landing dashboard. */
export default function DashboardRoute(): JSX.Element {
  return (
    <AuthenticatedGate>
      <main class="page-main-wide">
        <NormalDashboardHome />
      </main>
    </AuthenticatedGate>
  );
}
